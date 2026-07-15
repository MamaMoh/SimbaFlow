using System.Text.Json;
using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Candidates;

/// <summary>
/// Aggregate root representing a labour export candidate throughout their lifecycle.
/// Contains denormalized workflow state for efficient view queries.
/// </summary>
public class Candidate : BaseEntity
{
    // ──── Identity ────
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string PassportNumber { get; set; } = string.Empty;
    public string? LabourId { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? Nationality { get; set; }

    // ──── Contact ────
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    // ──── Travel & Contract ────
    public string? CountryOfTravel { get; set; }
    public string? OfficeName { get; set; }
    public DateOnly? ContractDate { get; set; }

    // ──── Organization ────
    public Guid OfficeId { get; set; }

    // ──── Documents ────
    public string? PhotoPath { get; set; }

    // ──── Status ────
    public CandidateStatus Status { get; set; } = CandidateStatus.Active;

    // ──── Denormalized Workflow State (updated in same transaction as event append) ────
    /// <summary>Current primary stage the candidate is in.</summary>
    public Guid? CurrentStageId { get; set; }

    /// <summary>Human-readable name of current stage (denormalized for display).</summary>
    public string? CurrentStageName { get; set; }

    /// <summary>
    /// Current status values per track/field, stored as JSONB.
    /// Example: {"medical": "Fit", "tasheer": "BookDone", "visa": "Issued"}
    /// </summary>
    public JsonDocument? CurrentStatusValues { get; set; }

    /// <summary>
    /// Stage IDs where this candidate is also visible (mirror views).
    /// Stored as UUID[] for GIN index support.
    /// </summary>
    public Guid[] VisibleInStages { get; set; } = [];

    // ──── Registration ────
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public string? RegisteredBy { get; set; }

    // ──── Navigation ────
    public ICollection<CandidateDocument> Documents { get; set; } = [];

    // ──── Computed ────
    public string FullName => string.IsNullOrEmpty(MiddleName)
        ? $"{FirstName} {LastName}"
        : $"{FirstName} {MiddleName} {LastName}";
}
