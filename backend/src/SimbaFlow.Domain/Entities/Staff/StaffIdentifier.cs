using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Staff;

/// <summary>
/// Represents a professional or organizational identifier for a staff member.
/// Staff members may have multiple identifiers (NPI, DEA, state license, etc.)
/// each with their own issuance, expiration, and verification lifecycle.
/// </summary>
public class StaffIdentifier : BaseEntity
{
    public Guid StaffProfileId { get; set; }
    public StaffProfile StaffProfile { get; set; } = null!;

    /// <summary>The category of this identifier.</summary>
    public IdentifierType IdentifierType { get; set; }

    /// <summary>The actual identifier value (e.g., "1234567890" for NPI).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Issuing authority (e.g., "CMS", "State Medical Board of California").</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Date the identifier was issued/granted.</summary>
    public DateOnly IssueDate { get; set; }

    /// <summary>Date the identifier expires. Null if non-expiring.</summary>
    public DateOnly? ExpirationDate { get; set; }

    /// <summary>Whether this is the primary identifier of its type for billing/claims.</summary>
    public bool IsPrimary { get; set; }

    // Verification lifecycle
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedBy { get; set; }

    /// <summary>Active if not deleted, not expired, and verification is not Failed/Revoked.</summary>
    public bool IsActive =>
        !IsDeleted
        && (ExpirationDate == null || ExpirationDate >= DateOnly.FromDateTime(DateTime.UtcNow))
        && VerificationStatus is not VerificationStatus.Failed
        && VerificationStatus is not VerificationStatus.Revoked;
}
