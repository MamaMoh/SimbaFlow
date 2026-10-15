using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Middleware;

/// <summary>
/// A platform administrator may only work inside an agency while they have a live, stated reason
/// to (<see cref="SimbaFlow.Domain.Entities.Platform.PlatformAccessGrant"/>).
///
/// The agency switch is the X-Tenant-Id header, which the API honours for platform administrators
/// only. This refuses that header when no grant is live for the administrator and that agency,
/// so entering an agency always leaves a reason on its trail and runs out on its own. Answers are
/// cached for a minute; ending or starting a grant clears the cache entry.
/// </summary>
public sealed class AgencyAccessMiddleware
{
    public const string RefusalCode = "agency_access_required";

    private readonly RequestDelegate _next;

    public AgencyAccessMiddleware(RequestDelegate next) => _next = next;

    public static string CacheKey(Guid userId, Guid tenantId) => $"agency-access:{userId:N}:{tenantId:N}";

    public async Task InvokeAsync(HttpContext context, IMemoryCache cache, IPlatformDbContext db)
    {
        var header = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        var path = context.Request.Path;

        if (string.IsNullOrEmpty(header)
            || !Guid.TryParse(header, out var tenantId)
            || context.User.Identity?.IsAuthenticated != true
            || !IsPlatformAdmin(context.User)
            // Signing in and out, and starting or ending the access itself, work from anywhere.
            || path.StartsWithSegments("/api/auth")
            || path.StartsWithSegments("/api/platform"))
        {
            await _next(context);
            return;
        }

        // Their own agency, if they have one, needs no reason.
        var ownTenant = context.User.FindFirstValue("tenant_id");
        if (Guid.TryParse(ownTenant, out var own) && own == tenantId)
        {
            await _next(context);
            return;
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                           ?? context.User.FindFirstValue("sub"), out var userId))
        {
            await _next(context);
            return;
        }

        var allowed = await cache.GetOrCreateAsync(CacheKey(userId, tenantId), async entry =>
        {
            var now = DateTime.UtcNow;
            var expires = await db.PlatformAccessGrants.AsNoTracking()
                .Where(g => g.UserId == userId && g.TenantId == tenantId && !g.IsDeleted
                            && g.EndedAt == null && g.ExpiresAt > now)
                .Select(g => (DateTime?)g.ExpiresAt)
                .MaxAsync();

            // Never cache past the moment the grant runs out.
            var ttl = TimeSpan.FromMinutes(1);
            if (expires is DateTime until && until - now < ttl) ttl = until - now;
            entry.AbsoluteExpirationRelativeToNow = ttl > TimeSpan.Zero ? ttl : TimeSpan.FromSeconds(1);
            return expires is not null;
        });

        if (allowed)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            isSuccess = false,
            code = RefusalCode,
            error = "Your access to this agency has ended. Choose the agency again and give a reason to continue.",
        });
    }

    private static bool IsPlatformAdmin(ClaimsPrincipal user) =>
        user.Claims.Any(c => (c.Type == "role" || c.Type == ClaimTypes.Role) && PlatformRoles.Includes(c.Value));
}
