namespace SimbaFlow.Domain.Enums;

/// <summary>
/// Reasons for administrative impersonation sessions.
/// All impersonation events generate real-time security alerts.
/// </summary>
public enum ImpersonationReason
{
    /// <summary>Tier 3 support debugging a user-reported issue.</summary>
    TechnicalSupport,

    /// <summary>Emergency access when the original user is unavailable (break glass).</summary>
    BreakGlass,

    /// <summary>Compliance or security audit verification.</summary>
    AuditVerification,

    /// <summary>Training or onboarding demonstration.</summary>
    Training
}
