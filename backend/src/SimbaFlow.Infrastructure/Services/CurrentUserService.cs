using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Infrastructure.Services;

/// <summary>
/// Resolves the current authenticated user's context from the HTTP request.
/// Extracts claims from JWT token and headers for location/tenant context.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? Context => _httpContextAccessor.HttpContext;
    private ClaimsPrincipal? User => Context?.User;

    public string? UserId =>
        User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName =>
        User?.FindFirstValue(ClaimTypes.Name);

    public string? Email =>
        User?.FindFirstValue(ClaimTypes.Email);

    /// <summary>
    /// Active location from X-Current-Location header or JWT claim.
    /// Frontend sends this header after user selects their working location.
    /// </summary>
    public Guid? ActiveLocationId
    {
        get
        {
            var header = Context?.Request.Headers["X-Current-Location"].FirstOrDefault();
            if (!string.IsNullOrEmpty(header) && Guid.TryParse(header, out var locationId))
                return locationId;

            var claim = User?.FindFirstValue("active_location_id");
            if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var claimLocationId))
                return claimLocationId;

            return null;
        }
    }

    public Guid? DepartmentId
    {
        get
        {
            var claim = User?.FindFirstValue("department_id");
            return !string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public Guid? TenantId
    {
        get
        {
            // SECURITY: only platform admins (SuperAdmin) may target a tenant via the
            // X-Tenant-Id header. For everyone else the tenant is bound to the JWT claim,
            // so a tenant user can never reach another tenant's data by forging the header.
            if (IsSuperAdmin)
            {
                var header = Context?.Request.Headers["X-Tenant-Id"].FirstOrDefault();
                if (!string.IsNullOrEmpty(header) && Guid.TryParse(header, out var headerTenantId))
                    return headerTenantId;
            }

            var claim = User?.FindFirstValue("tenant_id");
            return !string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public IReadOnlyList<string> Permissions =>
        User?.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToList() ?? [];

    public IReadOnlyList<string> Roles =>
        User?.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value)
            .ToList() ?? [];

    public bool IsSuperAdmin =>
        User?.Claims.Any(c =>
            (c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role) &&
            c.Value == "SuperAdmin") ?? false;

    public bool HasPermission(string permission) =>
        IsSuperAdmin || Permissions.Contains(permission);

    public string? IpAddress =>
        Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent =>
        Context?.Request.Headers.UserAgent.FirstOrDefault();
}
