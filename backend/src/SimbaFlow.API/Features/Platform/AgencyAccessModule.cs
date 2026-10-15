using Carter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SimbaFlow.API.Middleware;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Platform;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Platform;

public record EnterAgencyBody(Guid TenantId, string Reason, int? Hours);

public record LeaveAgencyBody(Guid TenantId);

/// <summary>
/// Entering an agency as a platform administrator: with a reason, for a limited time, on the
/// agency's record. See <see cref="AgencyAccessMiddleware"/> for where it is enforced.
/// </summary>
public class AgencyAccessModule : ICarterModule
{
    public const int DefaultHours = 4;
    public const int MaxHours = 12;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/platform/access")
            .WithTags("Agency access")
            .RequireAuthorization("SuperAdmin");

        admin.MapPost("/", async (
            EnterAgencyBody body, IPlatformDbContext db, IMemoryCache cache, IAuditService audit,
            ICurrentUserService currentUser, CancellationToken ct) =>
        {
            if (!Guid.TryParse(currentUser.UserId, out var userId))
                return Results.Unauthorized();

            var reason = body.Reason?.Trim() ?? "";
            if (reason.Length < 5)
                return Results.Json(new { isSuccess = false, error = "Say why you need to enter this agency." }, statusCode: 400);
            if (reason.Length > 500)
                return Results.Json(new { isSuccess = false, error = "Keep the reason under 500 characters." }, statusCode: 400);

            var tenant = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == body.TenantId && !t.IsDeleted, ct);
            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Agency not found." }, statusCode: 404);

            var hours = Math.Clamp(body.Hours ?? DefaultHours, 1, MaxHours);
            var now = DateTime.UtcNow;

            // One live grant per administrator and agency: entering again replaces the old one.
            var live = await db.PlatformAccessGrants
                .Where(g => g.UserId == userId && g.TenantId == tenant.Id && g.EndedAt == null && g.ExpiresAt > now)
                .ToListAsync(ct);
            foreach (var g in live) g.EndedAt = now;

            var grant = new PlatformAccessGrant
            {
                UserId = userId,
                TenantId = tenant.Id,
                Reason = reason,
                StartedAt = now,
                ExpiresAt = now.AddHours(hours),
            };
            db.PlatformAccessGrants.Add(grant);
            await db.SaveChangesAsync(ct);
            cache.Remove(AgencyAccessMiddleware.CacheKey(userId, tenant.Id));

            await PlatformAudit.LogAsync(audit, currentUser, "EnterAgency", tenant.Id, "Tenant", tenant.Id.ToString(),
                new { reason, hours }, ct);

            return Results.Ok(new { isSuccess = true, data = new { grant.Id, tenant.Name, grant.ExpiresAt } });
        });

        admin.MapPost("/end", async (
            LeaveAgencyBody body, IPlatformDbContext db, IMemoryCache cache, IAuditService audit,
            ICurrentUserService currentUser, CancellationToken ct) =>
        {
            if (!Guid.TryParse(currentUser.UserId, out var userId))
                return Results.Unauthorized();

            var now = DateTime.UtcNow;
            var live = await db.PlatformAccessGrants
                .Where(g => g.UserId == userId && g.TenantId == body.TenantId && g.EndedAt == null && g.ExpiresAt > now)
                .ToListAsync(ct);
            foreach (var g in live) g.EndedAt = now;
            await db.SaveChangesAsync(ct);
            cache.Remove(AgencyAccessMiddleware.CacheKey(userId, body.TenantId));

            if (live.Count > 0)
                await PlatformAudit.LogAsync(audit, currentUser, "LeaveAgency", body.TenantId, "Tenant",
                    body.TenantId.ToString(), ct: ct);
            return Results.Ok(new { isSuccess = true });
        });

        // The administrator's live grants, so the switcher can show how long is left.
        admin.MapGet("/mine", async (IPlatformDbContext db, ICurrentUserService currentUser, CancellationToken ct) =>
        {
            if (!Guid.TryParse(currentUser.UserId, out var userId))
                return Results.Unauthorized();
            var now = DateTime.UtcNow;
            var data = await db.PlatformAccessGrants.AsNoTracking()
                .Where(g => g.UserId == userId && g.EndedAt == null && g.ExpiresAt > now)
                .Select(g => new { g.TenantId, g.Reason, g.StartedAt, g.ExpiresAt })
                .ToListAsync(ct);
            return Results.Ok(new { isSuccess = true, data });
        });

        // ──── The agency's side: who from the platform came in, when, and why ────
        app.MapGet("/api/agency-access", async (
            IPlatformDbContext db, ICurrentUserService currentUser, CancellationToken ct) =>
        {
            if (currentUser.TenantId is not Guid tenantId)
                return Results.Json(new { isSuccess = false, error = "Choose an agency first." }, statusCode: 400);
            if (!currentUser.HasPermission("audit.read") && !AgencyRoles.Owns(currentUser.Roles))
                return Results.Json(new { isSuccess = false, error = "Only the owner and auditors can see this." }, statusCode: 403);

            var grants = await db.PlatformAccessGrants.AsNoTracking()
                .Where(g => g.TenantId == tenantId && !g.IsDeleted)
                .OrderByDescending(g => g.StartedAt)
                .Take(100)
                .ToListAsync(ct);
            var ids = grants.Select(g => g.UserId).Distinct().ToList();
            var people = await db.ApplicationUsers.AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);

            var now = DateTime.UtcNow;
            var data = grants.Select(g => new
            {
                g.Id,
                Who = people.GetValueOrDefault(g.UserId) is { Length: > 0 } n ? n : "Platform team",
                g.Reason,
                StartedAt = DateTime.SpecifyKind(g.StartedAt, DateTimeKind.Utc),
                // When it ended, or when it will if it is still live.
                EndsAt = DateTime.SpecifyKind(g.EndedAt ?? g.ExpiresAt, DateTimeKind.Utc),
                IsLive = g.EndedAt == null && g.ExpiresAt > now,
            });
            return Results.Ok(new { isSuccess = true, data });
        }).WithTags("Agency access").RequireAuthorization();
    }
}
