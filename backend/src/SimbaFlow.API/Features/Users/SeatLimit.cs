using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.API.Features.Users;

/// <summary>
/// The number of active accounts an agency's plan allows (<c>TenantInfo.MaxUsers</c>).
///
/// Checked when an account is created or switched back on. Disabled and deleted accounts do not
/// count, so an agency at its limit frees a seat by disabling someone who has left.
/// </summary>
public static class SeatLimit
{
    /// <summary>Why one more active account would exceed the agency's limit, or null when it fits.</summary>
    public static async Task<string?> RefusalForOneMoreAsync(
        IPlatformDbContext db, Guid tenantId, CancellationToken ct)
    {
        var max = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => (int?)t.MaxUsers)
            .FirstOrDefaultAsync(ct);
        if (max is not int limit || limit <= 0)
            return null;

        var active = await db.ApplicationUsers.AsNoTracking()
            .CountAsync(u => u.TenantId == tenantId && u.IsActive && !u.IsDeleted, ct);

        return active >= limit
            ? $"This agency has reached its limit of {limit} active users. Disable an account that is no longer needed, or ask the platform team to raise the limit."
            : null;
    }
}
