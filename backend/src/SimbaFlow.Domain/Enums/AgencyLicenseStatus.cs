namespace SimbaFlow.Domain.Enums;

/// <summary>MoLS agency license status (distinct from SaaS SubscriptionStatus).</summary>
public enum AgencyLicenseStatus
{
    Pending = 0,
    Active = 1,
    Suspended = 2,
    Revoked = 3,
    Expired = 4
}
