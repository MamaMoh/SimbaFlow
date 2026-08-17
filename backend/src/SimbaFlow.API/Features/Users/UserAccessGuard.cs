using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Users;

/// <summary>
/// Tenant/privilege boundary checks for user-management operations.
/// Non-SuperAdmins may only act on users within their own tenant, may never act on
/// a platform SuperAdmin, and may never grant SuperAdmin or move users across tenants.
/// </summary>
public static class UserAccessGuard
{
    /// <summary>True if the caller may view/modify the target user.</summary>
    public static bool CanManage(ICurrentUserService caller, ApplicationUser target)
    {
        if (caller.IsSuperAdmin) return true;

        // Tenant admins may only touch users in their own (non-null) tenant,
        // and never a platform SuperAdmin.
        return caller.TenantId.HasValue
            && target.TenantId.HasValue
            && target.TenantId == caller.TenantId
            && !target.IsSuperAdmin;
    }
}
