using System.Text.Json;
using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Entities.Agency;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Candidates;

/// <summary>
/// Aggregate root representing a labour export candidate throughout their lifecycle.
/// Contains denormalized workflow state for efficient view queries.
/// </summary>
public class Candidate : BaseEntity
{
    // ──── Identity ────
    public string ApplicationNo { get; set; } = string.Empty;
    public string? FileNumber { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }

    public string PassportNumber { get; set; } = string.Empty;
    public string? PassportType { get; set; }
    public DateOnly? PassportIssueDate { get; set; }
    public DateOnly? PassportExpiryDate { get; set; }
    public string? PlaceOfIssue { get; set; }
    public string? PlaceOfBirth { get; set; }

    public string? LabourId { get; set; }
    public string? BiometricId { get; set; }
    public string? NationalId { get; set; }

    public DateOnly DateOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? Nationality { get; set; }
    public string? Religion { get; set; }
    public string? MaritalStatus { get; set; }
    public string? Occupation { get; set; }
    public string? Qualification { get; set; }

    // ──── Contact ────
    public string? PhoneNumber { get; set; }
    public string? Phone2 { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Region { get; set; }
    public string? Subcity { get; set; }
    public string? Woreda { get; set; }
    public string? HouseNo { get; set; }

    // ──── Travel & Contract (denormalized display; full details in Placement) ────
    public string? CountryOfTravel { get; set; }
    public string? OfficeName { get; set; }
    public DateOnly? ContractDate { get; set; }

    // ──── Organization ────
    public Guid OfficeId { get; set; }
    public Office? Office { get; set; }

    // ──── Documents ────
    public string? PhotoPath { get; set; }

    // ──── Status ────
    public CandidateStatus Status { get; set; } = CandidateStatus.Active;

    // ──── Denormalized Workflow State ────
    public Guid? CurrentStageId { get; set; }
    public string? CurrentStageName { get; set; }
    public JsonDocument? CurrentStatusValues { get; set; }
    public Guid[] VisibleInStages { get; set; } = [];

    // ──── Timing denorm ────
    public DateTime? CurrentStageEnteredAt { get; set; }
    public DateTime? LastActionAt { get; set; }
    public string? LastActionLabel { get; set; }
    public DateTime? FlightDate { get; set; }
    public bool IsOverdue { get; set; }

    // ──── Registration ────
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public string? RegisteredBy { get; set; }

    // ──── Navigation ────
    public CandidatePlacement? Placement { get; set; }
    public CandidateSkills? Skills { get; set; }
    public ICollection<CandidateRelative> Relatives { get; set; } = [];
    public ICollection<CandidateDocument> Documents { get; set; } = [];
    public ICollection<CandidateStageStay> StageStays { get; set; } = [];
    public ICollection<CandidateStepStay> StepStays { get; set; } = [];
    public ICollection<CandidateReturned> ReturnedRecords { get; set; } = [];
    public ICollection<CandidateComplaint> Complaints { get; set; } = [];
    public CandidateCommission? Commission { get; set; }

    public string FullName => string.IsNullOrEmpty(MiddleName)
        ? $"{FirstName} {LastName}"
        : $"{FirstName} {MiddleName} {LastName}";
}
