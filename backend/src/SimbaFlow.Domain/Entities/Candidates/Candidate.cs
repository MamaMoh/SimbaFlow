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
    /// <summary>Local-script full name (e.g. Amharic).</summary>
    public string? LocalFullName { get; set; }
    public string PassportNumber { get; set; } = string.Empty;
    public string? LabourId { get; set; }
    public string? BiometricId { get; set; }
    public string? NationalId { get; set; }
    public DateOnly DateOfBirth { get; set; }
    public string? PlaceOfBirth { get; set; }
    public Gender Gender { get; set; }
    public string? Nationality { get; set; }
    public string? Religion { get; set; }
    public string? MaritalStatus { get; set; }
    public int? NumberOfChildren { get; set; }
    public string? Height { get; set; }
    public string? Weight { get; set; }

    // ──── Passport ────
    public string? PassportType { get; set; }
    public string? PassportPlaceOfIssue { get; set; }
    public DateOnly? PassportIssueDate { get; set; }
    public DateOnly? PassportExpiryDate { get; set; }

    // ──── Contact ────
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Region { get; set; }
    public string? Subcity { get; set; }
    public string? Woreda { get; set; }
    public string? HouseNo { get; set; }

    // ──── Job / CV ────
    public string? Occupation { get; set; }
    public string? Qualification { get; set; }
    public string? MonthlySalary { get; set; }
    public string? ContractPeriod { get; set; }
    public string? EnglishLevel { get; set; }
    public string? ArabicLevel { get; set; }
    /// <summary>Other languages as "Amharic: Good; French: Fair".</summary>
    public string? OtherLanguages { get; set; }
    public int? ExperienceAbroadYears { get; set; }
    public string? WorksIn { get; set; }
    public string? ReferenceNo { get; set; }
    public string? Remark { get; set; }
    public string? CookingLevel { get; set; }
    public bool SkillCleaning { get; set; }
    public bool SkillWashing { get; set; }
    public bool SkillCooking { get; set; }
    public bool SkillIroning { get; set; }
    public bool SkillSewing { get; set; }
    public bool SkillBabysitting { get; set; }
    public bool SkillChildCare { get; set; }

    // ──── Travel & Contract ────
    public string? CountryOfTravel { get; set; }
    public string? OfficeName { get; set; }
    /// <summary>Selected foreign partner from platform catalog (snapshot name kept in OfficeName).</summary>
    public Guid? PartnerAgencyId { get; set; }
    public DateOnly? ContractDate { get; set; }

    // ──── Organization ────
    public Guid OfficeId { get; set; }

    // ──── Documents ────
    public string? PhotoPath { get; set; }
    /// <summary>Full-body / full-size photo used for agency forms (distinct from portrait PhotoPath).</summary>
    public string? FullPhotoPath { get; set; }

    // ──── Sponsor & Visa (intake) ────
    public string? VisaNumber { get; set; }
    public string? VisaType { get; set; }
    public string? SponsorName { get; set; }
    public string? SponsorIdNumber { get; set; }
    public string? SponsorPhone { get; set; }
    public string? SponsorAddress { get; set; }
    public string? SponsorArabicName { get; set; }
    public string? AgentName { get; set; }

    // ──── Admin references ────
    public string? ApplicationNo { get; set; }
    public string? FileNo { get; set; }
    public string? WakalaNo { get; set; }
    public string? ContractNo { get; set; }
    public string? StickerVisaNo { get; set; }
    public DateOnly? SignedOn { get; set; }

    // ──── Relative / emergency contact ────
    public string? RelativeName { get; set; }
    public string? RelativePhone { get; set; }
    public string? RelativeKinship { get; set; }
    public string? RelativeGender { get; set; }
    public DateOnly? RelativeBirthDate { get; set; }
    public string? RelativeCity { get; set; }
    public string? RelativeRegion { get; set; }
    public string? RelativeSubcity { get; set; }
    public string? RelativeWoreda { get; set; }
    public string? RelativeHouseNo { get; set; }

    // ──── Other / COC ────
    public string? ContactPerson2 { get; set; }
    public string? ContactPhone2 { get; set; }
    public string? CocCenterName { get; set; }
    public string? CertificateNo { get; set; }
    public DateOnly? CertifiedDate { get; set; }
    public string? MedicalPlace { get; set; }

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

    /// <summary>
    /// UTC timestamp when the candidate entered the current primary stage.
    /// Used for days-in-stage on Embassy/LMIS boards.
    /// </summary>
    public DateTime? StageEnteredAt { get; set; }

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
