using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Auth;

/// <summary>
/// Whether this user's agency is in a state that allows them to sign in.
///
/// Checked at every point a token is handed out — first factor, second factor, and refresh —
/// because a suspension that only took effect at the next fresh login would leave everyone already
/// signed in working for as long as they kept the tab open.
/// </summary>
public static class TenantSignInGuard
{
    /// <summary>
    /// The reason to refuse this user, or null to let them through.
    ///
    /// Call this only after the password has been verified. The answer says something true about
    /// the agency, and saying it to someone who has not proved who they are would let an
    /// unauthenticated caller sort usernames by which agencies exist and which are suspended.
    /// </summary>
    public static async Task<string?> RefusalFor(
        ApplicationUser user, IPlatformDbContext context, CancellationToken ct)
    {
        // Platform staff belong to no agency, and are usually the people who have to lift the
        // suspension — locking them out would leave nobody able to.
        if (user.IsSuperAdmin || user.TenantId is not Guid tenantId)
            return null;

        var tenant = await context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId && !t.IsDeleted)
            .Select(t => new { t.SubscriptionStatus })
            .FirstOrDefaultAsync(ct);

        return tenant is null
            ? SubscriptionRules.SignInRefusalForMissingAgency
            : SubscriptionRules.SignInRefusalFor(tenant.SubscriptionStatus);
    }
}
