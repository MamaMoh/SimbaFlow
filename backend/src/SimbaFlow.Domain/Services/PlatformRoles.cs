namespace SimbaFlow.Domain.Services;

/// <summary>
/// The roles that run the platform rather than an agency.
///
/// PlatformAdmin began as a lesser role: user accounts and system configuration, deliberately
/// without candidate access, so support staff could administer the platform without reading any
/// agency's people. That separation was removed on request — the two are now the same authority.
///
/// They are equal in three places, and all three used to spell "SuperAdmin" as a literal: the
/// authorization policy guarding agencies and subscriptions, the IsSuperAdmin flag that waives
/// permission checks and allows working inside a chosen agency, and the seeded permission set.
/// Naming them here is what keeps those three from drifting apart again.
/// </summary>
public static class PlatformRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string PlatformAdmin = "PlatformAdmin";

    /// <summary>Every role that carries full platform authority.</summary>
    public static readonly string[] All = [SuperAdmin, PlatformAdmin];

    public static bool Includes(string? roleName) =>
        roleName is not null && All.Contains(roleName, StringComparer.Ordinal);
}
