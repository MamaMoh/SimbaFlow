using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Services;

/// <summary>
/// The order an agency leaves in: offboard, export, then — only if asked — purge.
///
/// Offboarding is reversible. The agency's people can no longer sign in and nothing runs for it,
/// but its schema and files are untouched, so bringing it back is a status change. Purging is
/// not reversible: the schema is dropped and the files are deleted. These rules sit between the
/// two so that a purge cannot happen by a misclick on an agency that is still working, or to
/// data nobody has a copy of.
/// </summary>
public static class OffboardingRules
{
    /// <summary>Why this agency cannot be offboarded now, or null when it can.</summary>
    public static string? OffboardRefusalFor(TenantInfo tenant)
    {
        if (tenant.PurgedAt is not null)
            return "This agency has already been purged.";
        if (tenant.SubscriptionStatus == TenantStatus.Deactivated && tenant.OffboardedAt is not null)
            return "This agency has already been offboarded.";
        return null;
    }

    /// <summary>Why this agency cannot be brought back, or null when it can.</summary>
    public static string? ReactivateRefusalFor(TenantInfo tenant)
    {
        if (tenant.PurgedAt is not null)
            return "This agency's data has been purged, so it cannot be brought back. Create it again instead.";
        if (tenant.SubscriptionStatus == TenantStatus.Active)
            return "This agency is already active.";
        return null;
    }

    /// <summary>
    /// Why this agency's data cannot be destroyed now, or null when it can.
    ///
    /// The person purging types the agency's URL identifier back, as with deleting a repository:
    /// it proves they are looking at the agency they mean, not the row above it.
    /// </summary>
    public static string? PurgeRefusalFor(TenantInfo tenant, string? confirmSlug)
    {
        if (tenant.PurgedAt is not null)
            return "This agency has already been purged.";

        if (tenant.SubscriptionStatus != TenantStatus.Deactivated || tenant.OffboardedAt is not DateTime offboarded)
            return "Offboard the agency before purging it.";

        if (tenant.LastExportedAt is not DateTime exported || exported < offboarded)
            return "Export the agency's data after offboarding it, then purge.";

        if (!string.Equals(confirmSlug?.Trim(), tenant.Slug, StringComparison.Ordinal))
            return $"Type '{tenant.Slug}' to confirm.";

        return null;
    }

    /// <summary>
    /// The slug and schema name a purged agency keeps, so the originals are free to be used again.
    ///
    /// An agency that leaves and later returns should get its old URL identifier back. The
    /// tombstone keeps a recognisable trace of the original and the agency's id, which is unique.
    /// </summary>
    public static (string Slug, string SchemaName) TombstoneNames(TenantInfo tenant)
    {
        var suffix = $"purged-{tenant.Id:N}"[..15];
        return ($"{tenant.Slug}~{suffix}", $"{tenant.SchemaName}~{suffix}");
    }
}
