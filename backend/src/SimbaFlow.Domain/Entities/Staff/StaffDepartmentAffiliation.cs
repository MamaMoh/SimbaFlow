using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.Domain.Entities.Staff;

/// <summary>
/// Temporal mapping of a staff member to a department with a specific clinical role.
/// Supports multiple concurrent affiliations (e.g., ER Attending + Pediatrics Consultant)
/// and historical tracking for billing accuracy and audit compliance.
/// </summary>
public class StaffDepartmentAffiliation : BaseEntity
{
    public Guid StaffProfileId { get; set; }
    public StaffProfile StaffProfile { get; set; } = null!;

    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;

    /// <summary>When this affiliation became active.</summary>
    public DateOnly EffectiveStartDate { get; set; }

    /// <summary>When this affiliation ended. Null if currently active.</summary>
    public DateOnly? EffectiveEndDate { get; set; }

    /// <summary>Whether this is the staff member's primary department for routing (labs, messages, etc.).</summary>
    public bool IsPrimary { get; set; }

    /// <summary>The clinical/administrative capacity in this department (e.g., "Attending", "Consulting", "Director").</summary>
    public string ClinicalRole { get; set; } = string.Empty;

    /// <summary>Free-text notes (e.g., "Temporary coverage for Dr. Smith's leave").</summary>
    public string? Notes { get; set; }

    /// <summary>Active if within effective date range and not soft-deleted.</summary>
    public bool IsCurrentlyActive =>
        !IsDeleted
        && EffectiveStartDate <= DateOnly.FromDateTime(DateTime.UtcNow)
        && (EffectiveEndDate == null || EffectiveEndDate >= DateOnly.FromDateTime(DateTime.UtcNow));
}
