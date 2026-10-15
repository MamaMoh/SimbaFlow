using Carter;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Features.Tenants;

/// <summary>
/// How an agency leaves: offboard, export, purge.
///
/// Before this, the only way out was Delete, which hid the agency's row and left everything else
/// where it was — its schema, its files, its people's accounts, and its URL identifier taken for
/// good. Nothing could be exported first and nothing could be brought back.
///
/// Offboarding stops the agency (its people cannot sign in, nothing runs for it) and keeps all of
/// its data, so it can be reactivated from the Agencies page. Export hands over everything. Purge
/// destroys the schema and files, and only after an export taken since offboarding.
/// </summary>
public class OffboardingModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants")
            .WithTags("Tenants")
            .RequireAuthorization("SuperAdmin");

        group.MapPost("/{id:guid}/offboard", async (
            Guid id,
            OffboardRequest request,
            IPlatformDbContext db,
            ITenantSchemaResolver schemas,
            ICurrentUserService currentUser,
            IAuditService audit,
            CancellationToken ct) =>
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Agency not found." }, statusCode: 404);

            if (OffboardingRules.OffboardRefusalFor(tenant) is { } refusal)
                return Results.Json(new { isSuccess = false, error = refusal }, statusCode: 409);

            var now = DateTime.UtcNow;
            tenant.SubscriptionStatus = TenantStatus.Deactivated;
            tenant.OffboardedAt = now;
            tenant.OffboardedBy = currentUser.UserName;
            tenant.OffboardingReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

            // The sign-in guard already refuses a deactivated agency at every refresh. Ending the
            // sessions as well means nobody is left holding a token that names it.
            var userIds = await db.ApplicationUsers
                .Where(u => u.TenantId == id)
                .Select(u => u.Id)
                .ToListAsync(ct);

            var tokens = await db.RefreshTokens
                .Where(t => userIds.Contains(t.UserId) && t.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var token in tokens)
            {
                token.RevokedAt = now;
                token.ReasonRevoked = "AgencyOffboarded";
            }

            var sessions = await db.UserSessions
                .Where(s => userIds.Contains(s.UserId) && s.IsActive)
                .ToListAsync(ct);
            foreach (var session in sessions)
            {
                session.IsActive = false;
                session.LogoutAt = now;
            }

            await db.SaveChangesAsync(ct);
            schemas.InvalidateCache(id);

            await Platform.PlatformAudit.LogAsync(audit, currentUser, "OffboardAgency", id, "Tenant", id.ToString(),
                new { tenant.OffboardingReason }, ct);
            return Results.Ok(new { isSuccess = true, data = new { tenant.OffboardedAt, signedOut = sessions.Count } });
        });

        group.MapGet("/{id:guid}/export", async (
            Guid id,
            IPlatformDbContext db,
            ITenantLifecycleService lifecycle,
            IAuditService audit,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Agency not found." }, statusCode: 404);
            if (tenant.PurgedAt is not null)
                return Results.Json(new { isSuccess = false, error = "This agency has been purged; there is nothing left to export." },
                    statusCode: 409);

            // Built on disk first: the zip has to be finished before anyone can rely on it, and
            // an export only counts towards a purge once it has been written in full.
            var path = Path.Combine(Path.GetTempPath(), $"simbaflow-export-{Guid.NewGuid():N}.zip");
            var file = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
                bufferSize: 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
            try
            {
                await lifecycle.WriteExportAsync(tenant, file, ct);
                file.Position = 0;
            }
            catch
            {
                await file.DisposeAsync();
                throw;
            }

            tenant.LastExportedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await Platform.PlatformAudit.LogAsync(audit, currentUser, "ExportAgency", id, "Tenant", id.ToString(), ct: ct);

            var name = $"{tenant.Slug}-export-{DateTime.UtcNow:yyyyMMdd-HHmm}.zip";
            return Results.File(file, "application/zip", name);
        });

        group.MapPost("/{id:guid}/purge", async (
            Guid id,
            PurgeRequest request,
            IPlatformDbContext db,
            ITenantLifecycleService lifecycle,
            ITenantSchemaResolver schemas,
            IAuditService audit,
            ICurrentUserService currentUser,
            CancellationToken ct) =>
        {
            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Agency not found." }, statusCode: 404);

            if (OffboardingRules.PurgeRefusalFor(tenant, request.ConfirmSlug) is { } refusal)
                return Results.Json(new { isSuccess = false, error = refusal }, statusCode: 409);

            var files = await lifecycle.DestroyDataAsync(tenant, ct);

            // The people of a purged agency are gone with it. Their addresses are freed so the
            // same person can be given an account again — at a returning agency, or another one.
            var users = await db.ApplicationUsers.Where(u => u.TenantId == id).ToListAsync(ct);
            foreach (var user in users)
            {
                var gone = $"purged-{user.Id:N}@invalid";
                user.IsActive = false;
                user.IsDeleted = true;
                user.Email = gone;
                user.NormalizedEmail = gone.ToUpperInvariant();
                user.UserName = gone;
                user.NormalizedUserName = gone.ToUpperInvariant();
                user.PhoneNumber = null;
            }

            var links = await db.PartnerLinks.Where(l => l.TenantId == id && !l.IsDeleted).ToListAsync(ct);
            foreach (var link in links) link.IsDeleted = true;

            var agreements = await db.PartnerAgreementDocuments.Where(d => d.TenantId == id && !d.IsDeleted).ToListAsync(ct);
            foreach (var agreement in agreements) agreement.IsDeleted = true;

            // The row stays as the record that the agency existed and was removed. Its slug and
            // schema name are moved aside so a returning agency can have them again.
            (tenant.Slug, tenant.SchemaName) = OffboardingRules.TombstoneNames(tenant);
            tenant.PurgedAt = DateTime.UtcNow;
            tenant.IsDeleted = true;
            tenant.LogoPath = null;
            tenant.LetterheadPath = null;

            await db.SaveChangesAsync(ct);
            schemas.InvalidateCache(id);

            await Platform.PlatformAudit.LogAsync(audit, currentUser, "PurgeAgency", id, "Tenant", id.ToString(),
                new { filesDestroyed = files, usersRemoved = users.Count }, ct);
            return Results.Ok(new { isSuccess = true, data = new { filesDestroyed = files, usersRemoved = users.Count } });
        });
    }
}

public record OffboardRequest(string? Reason);
public record PurgeRequest(string? ConfirmSlug);
