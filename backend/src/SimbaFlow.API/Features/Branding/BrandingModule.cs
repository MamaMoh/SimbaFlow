using Carter;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.API.Features.Branding;

/// <summary>
/// Letterhead logos for the agency and for partner agencies.
///
/// Documents are headed with the partner's logo when a candidate is placed with one, and with the
/// agency's own otherwise — so both need somewhere to put theirs.
/// </summary>
public class BrandingModule : ICarterModule
{
    private const long MaxUploadBytes = 2 * 1024 * 1024;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/branding")
            .WithTags("Branding")
            .RequireAuthorization();

        // ──── The agency's own letterhead ────

        group.MapGet("/agency", async (
            IPlatformDbContext context,
            ICurrentUserService user,
            CancellationToken ct) =>
        {
            if (user.TenantId is not Guid tenantId)
                return Results.Ok(new { isSuccess = true, data = new { logoPath = (string?)null } });

            var path = await context.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId && !t.IsDeleted)
                .Select(t => t.LogoPath)
                .FirstOrDefaultAsync(ct);

            return Results.Ok(new { isSuccess = true, data = new { logoPath = path } });
        });

        group.MapPost("/agency", async (
            HttpRequest request,
            IPlatformDbContext context,
            IFileStorageService storage,
            ICurrentUserService user,
            CancellationToken ct) =>
        {
            if (!user.IsSuperAdmin && !user.HasPermission("settings.write"))
                return Forbidden();

            if (user.TenantId is not Guid tenantId)
                return Results.Json(
                    new { isSuccess = false, error = "This account is not attached to an agency." },
                    statusCode: 400);

            var file = await ReadFileAsync(request);
            if (file is null) return NoFile();

            var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct);
            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Agency not found." }, statusCode: 404);

            try
            {
                await using var stream = file.OpenReadStream();
                tenant.LogoPath = await storage.UploadLogoAsync(
                    "agency", tenantId, file.FileName, file.ContentType, stream, ct);
                await context.SaveChangesAsync(ct);
                return Results.Ok(new { isSuccess = true, data = new { logoPath = tenant.LogoPath } });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { isSuccess = false, error = ex.Message }, statusCode: 400);
            }
        }).DisableAntiforgery();

        group.MapDelete("/agency", async (
            IPlatformDbContext context,
            ICurrentUserService user,
            CancellationToken ct) =>
        {
            if (!user.IsSuperAdmin && !user.HasPermission("settings.write"))
                return Forbidden();
            if (user.TenantId is not Guid tenantId)
                return Results.Ok(new { isSuccess = true });

            var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct);
            if (tenant is not null)
            {
                tenant.LogoPath = null;
                await context.SaveChangesAsync(ct);
            }
            return Results.Ok(new { isSuccess = true });
        });

        // ──── A partner's letterhead ────

        group.MapPost("/partners/{id:guid}", async (
            Guid id,
            HttpRequest request,
            IPlatformDbContext context,
            IFileStorageService storage,
            ICurrentUserService user,
            CancellationToken ct) =>
        {
            if (!user.IsSuperAdmin && !user.HasPermission("partner.update"))
                return Forbidden();

            var file = await ReadFileAsync(request);
            if (file is null) return NoFile();

            var partner = await context.PartnerAgencies.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);
            if (partner is null)
                return Results.Json(new { isSuccess = false, error = "Partner not found." }, statusCode: 404);

            try
            {
                await using var stream = file.OpenReadStream();
                partner.LogoPath = await storage.UploadLogoAsync(
                    "partner", id, file.FileName, file.ContentType, stream, ct);
                await context.SaveChangesAsync(ct);
                return Results.Ok(new { isSuccess = true, data = new { logoPath = partner.LogoPath } });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { isSuccess = false, error = ex.Message }, statusCode: 400);
            }
        }).DisableAntiforgery();

        group.MapDelete("/partners/{id:guid}", async (
            Guid id,
            IPlatformDbContext context,
            ICurrentUserService user,
            CancellationToken ct) =>
        {
            if (!user.IsSuperAdmin && !user.HasPermission("partner.update"))
                return Forbidden();

            var partner = await context.PartnerAgencies.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);
            if (partner is not null)
            {
                partner.LogoPath = null;
                await context.SaveChangesAsync(ct);
            }
            return Results.Ok(new { isSuccess = true });
        });

        // ──── Serving a stored logo ────
        //
        // Only paths under branding/ are servable, so this endpoint cannot be walked into the
        // candidate documents that share the same storage root.
        group.MapGet("/file", async (
            string path,
            IFileStorageService storage,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(path)
                || path.Contains("..", StringComparison.Ordinal)
                || !path.Replace('\\', '/').StartsWith("branding/", StringComparison.Ordinal))
            {
                return Results.NotFound();
            }

            var stream = await storage.DownloadAsync(path, ct);
            if (stream is null) return Results.NotFound();

            var contentType = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? "image/png"
                : "image/jpeg";
            return Results.Stream(stream, contentType);
        });
    }

    private static IResult Forbidden() =>
        Results.Json(new { isSuccess = false, error = "Forbidden" }, statusCode: 403);

    private static IResult NoFile() =>
        Results.Json(new { isSuccess = false, error = "Choose an image to upload." }, statusCode: 400);

    private static async Task<IFormFile?> ReadFileAsync(HttpRequest request)
    {
        if (!request.HasFormContentType) return null;
        var form = await request.ReadFormAsync();
        var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        return file is { Length: > 0 and <= MaxUploadBytes } ? file : null;
    }
}
