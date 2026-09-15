using Carter;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.API.Features.Branding;

/// <summary>
/// Branding for the agency and for partner agencies: a logo (the mark) and a letterhead (the
/// banner printed across the top of a document).
///
/// Documents are headed with the partner's branding when a candidate is placed with one, and with
/// the agency's own otherwise — so both need somewhere to put theirs.
/// </summary>
public class BrandingModule : ICarterModule
{
    private const long MaxUploadBytes = 2 * 1024 * 1024;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/branding")
            .WithTags("Branding")
            .RequireAuthorization();

        // ──── The agency's own branding ────

        group.MapGet("/agency", async (
            IPlatformDbContext context,
            ICurrentUserService user,
            CancellationToken ct) =>
        {
            if (user.TenantId is not Guid tenantId)
                return Results.Ok(new { isSuccess = true, data = new { logoPath = (string?)null, letterheadPath = (string?)null } });

            var paths = await context.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId && !t.IsDeleted)
                .Select(t => new { t.LogoPath, t.LetterheadPath })
                .FirstOrDefaultAsync(ct);

            return Results.Ok(new
            {
                isSuccess = true,
                data = new { logoPath = paths?.LogoPath, letterheadPath = paths?.LetterheadPath },
            });
        });

        group.MapPost("/agency/{kind}", async (
            string kind,
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

            if (!IsKnownKind(kind)) return UnknownKind();

            var file = await ReadFileAsync(request);
            if (file is null) return NoFile();

            var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct);
            if (tenant is null)
                return Results.Json(new { isSuccess = false, error = "Agency not found." }, statusCode: 404);

            try
            {
                await using var stream = file.OpenReadStream();
                var path = await storage.UploadLogoAsync(
                    "agency", tenantId, file.FileName, file.ContentType, stream, ct);
                if (kind == "letterhead") tenant.LetterheadPath = path; else tenant.LogoPath = path;
                await context.SaveChangesAsync(ct);
                return Results.Ok(new { isSuccess = true, data = new { logoPath = path } });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { isSuccess = false, error = ex.Message }, statusCode: 400);
            }
        }).DisableAntiforgery();

        group.MapDelete("/agency/{kind}", async (
            string kind,
            IPlatformDbContext context,
            ICurrentUserService user,
            CancellationToken ct) =>
        {
            if (!user.IsSuperAdmin && !user.HasPermission("settings.write"))
                return Forbidden();
            if (!IsKnownKind(kind)) return UnknownKind();
            if (user.TenantId is not Guid tenantId)
                return Results.Ok(new { isSuccess = true });

            var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId && !t.IsDeleted, ct);
            if (tenant is not null)
            {
                if (kind == "letterhead") tenant.LetterheadPath = null; else tenant.LogoPath = null;
                await context.SaveChangesAsync(ct);
            }
            return Results.Ok(new { isSuccess = true });
        });

        // ──── A partner's branding ────

        group.MapPost("/partners/{id:guid}/{kind}", async (
            Guid id,
            string kind,
            HttpRequest request,
            IPlatformDbContext context,
            IFileStorageService storage,
            ICurrentUserService user,
            CancellationToken ct) =>
        {
            if (!user.IsSuperAdmin && !user.HasPermission("partner.update"))
                return Forbidden();

            if (!IsKnownKind(kind)) return UnknownKind();

            var file = await ReadFileAsync(request);
            if (file is null) return NoFile();

            var partner = await context.PartnerAgencies.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);
            if (partner is null)
                return Results.Json(new { isSuccess = false, error = "Partner not found." }, statusCode: 404);

            try
            {
                await using var stream = file.OpenReadStream();
                var path = await storage.UploadLogoAsync(
                    "partner", id, file.FileName, file.ContentType, stream, ct);
                if (kind == "letterhead") partner.LetterheadPath = path; else partner.LogoPath = path;
                await context.SaveChangesAsync(ct);
                return Results.Ok(new { isSuccess = true, data = new { logoPath = path } });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { isSuccess = false, error = ex.Message }, statusCode: 400);
            }
        }).DisableAntiforgery();

        group.MapDelete("/partners/{id:guid}/{kind}", async (
            Guid id,
            string kind,
            IPlatformDbContext context,
            ICurrentUserService user,
            CancellationToken ct) =>
        {
            if (!user.IsSuperAdmin && !user.HasPermission("partner.update"))
                return Forbidden();
            if (!IsKnownKind(kind)) return UnknownKind();

            var partner = await context.PartnerAgencies.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);
            if (partner is not null)
            {
                if (kind == "letterhead") partner.LetterheadPath = null; else partner.LogoPath = null;
                await context.SaveChangesAsync(ct);
            }
            return Results.Ok(new { isSuccess = true });
        });

        // ──── Serving a stored image ────
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

    /// <summary>An organisation has two images: a mark, and the banner printed on documents.</summary>
    private static bool IsKnownKind(string kind) => kind is "logo" or "letterhead";

    private static IResult UnknownKind() =>
        Results.Json(new { isSuccess = false, error = "Expected 'logo' or 'letterhead'." }, statusCode: 400);

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
