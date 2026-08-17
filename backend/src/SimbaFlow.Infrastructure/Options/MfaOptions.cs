namespace SimbaFlow.Infrastructure.Options;

/// <summary>
/// Multi-factor authentication policy, sourced from the "Mfa" config section.
/// Enforcement is OFF by default: turning it on for a role changes that role's
/// login contract (it must enroll before receiving a full token), so the client
/// must support the enrollment flow first.
/// </summary>
public sealed class MfaOptions
{
    /// <summary>Master switch. When false, MFA is available but never required.</summary>
    public bool Enforce { get; set; } = false;

    /// <summary>Roles that must have MFA enabled (case-insensitive).</summary>
    public string[] RequiredRoles { get; set; } = ["AgencyOwner"];

    /// <summary>Always require MFA for platform SuperAdmins.</summary>
    public bool RequireForSuperAdmin { get; set; } = true;

    /// <summary>Lifetime (minutes) of the limited enrollment token issued at login.</summary>
    public int SetupTokenMinutes { get; set; } = 15;
}
