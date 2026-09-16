using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Users;

/// <summary>
/// Tenant/privilege boundary checks for user-management operations.
///
/// Two separate questions, and both have to be asked. <see cref="CanManage"/> answers "may the
/// caller touch this user at all", which is about the tenant boundary. <see cref="CanGrantRole"/>
/// answers "may the caller hand out this role", which is about privilege — and is the one that was
/// missing: an agency owner ships with users.write, so without it they could assign themselves a
/// platform role and reach every other agency's data.
/// </summary>
public static class UserAccessGuard
{
    /// <summary>
    /// Roles that confer power over the platform rather than over one agency, and so may only be
    /// granted by someone who already holds the platform.
    ///
    /// SuperAdmin is the obvious one. PlatformAdmin belongs here too: it carries users.write and
    /// role.write across every tenant, which is a different thing from administering one agency.
    /// </summary>
    private static readonly HashSet<string> PlatformRoles =
        new(StringComparer.OrdinalIgnoreCase) { "SuperAdmin", "PlatformAdmin" };

    /// <summary>True if the caller may view/modify the target user.</summary>
    public static bool CanManage(ICurrentUserService caller, ApplicationUser target)
    {
        if (caller.IsSuperAdmin) return true;

        // A platform administrator has no tenant of their own by design, so the tenant comparison
        // below can never succeed for them. They administer accounts across the platform, but are
        // not allowed near a platform-level account — that is what keeps this short of SuperAdmin.
        if (IsTenantlessPlatformAdmin(caller))
            return !target.IsSuperAdmin;

        // Tenant admins may only touch users in their own (non-null) tenant,
        // and never a platform SuperAdmin.
        return caller.TenantId.HasValue
            && target.TenantId.HasValue
            && target.TenantId == caller.TenantId
            && !target.IsSuperAdmin;
    }

    /// <summary>
    /// True if the caller may grant the named role.
    ///
    /// Note this is asked about the role name, not about the target user: granting SuperAdmin to
    /// someone inside your own tenant is exactly the escalation being prevented, and CanManage
    /// would happily allow it.
    /// </summary>
    public static bool CanGrantRole(ICurrentUserService caller, string roleName) =>
        caller.IsSuperAdmin || !PlatformRoles.Contains(roleName);

    /// <summary>The roles in <paramref name="roleNames"/> that the caller is not allowed to grant.</summary>
    public static IReadOnlyList<string> RolesCallerMayNotGrant(
        ICurrentUserService caller, IEnumerable<string> roleNames) =>
        roleNames.Where(r => !CanGrantRole(caller, r)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>
    /// A platform administrator: deliberately belongs to no agency, and administers accounts across
    /// all of them.
    ///
    /// Keyed on the role rather than on "has users.write and no tenant", so that an ordinary agency
    /// user who arrives without a tenant claim — a malformed or truncated token — is treated as
    /// having no access rather than as having platform-wide access.
    /// </summary>
    private static bool IsTenantlessPlatformAdmin(ICurrentUserService caller) =>
        !caller.TenantId.HasValue
        && caller.Roles.Contains("PlatformAdmin", StringComparer.OrdinalIgnoreCase);
}
