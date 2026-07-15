using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Scoped service providing the current user's clinical/staff context.
/// Resolved per-request from JWT claims + database/cache lookup.
/// 
/// CQRS handlers inject this to access the active staff profile,
/// current department affiliations, and clinical role without bloating the JWT.
/// 
/// JWT only carries: UserId, StaffProfileId, TenantId, IT Roles.
/// Everything else is resolved here with short TTL caching.
/// </summary>
public interface IStaffContext
{
    /// <summary>The current user's ApplicationUser ID. Always available if authenticated.</summary>
    Guid? UserId { get; }

    /// <summary>The linked StaffProfile ID. Null for non-clinical users (IT, auditors).</summary>
    Guid? StaffProfileId { get; }

    /// <summary>Whether this user has a linked clinical/staff profile.</summary>
    bool HasStaffProfile { get; }

    /// <summary>The staff member's current employment status.</summary>
    EmploymentStatus? EmploymentStatus { get; }

    /// <summary>Active department affiliations (not ended, within effective dates).</summary>
    IReadOnlyList<StaffAffiliationContext> ActiveAffiliations { get; }

    /// <summary>The primary department affiliation (for default routing of labs, messages, etc.).</summary>
    StaffAffiliationContext? PrimaryAffiliation { get; }

    /// <summary>The ID of the primary department (convenience accessor).</summary>
    Guid? PrimaryDepartmentId { get; }

    /// <summary>Whether this request is an impersonated session.</summary>
    bool IsImpersonated { get; }

    /// <summary>The admin user ID who initiated the impersonation (null if not impersonated).</summary>
    Guid? ImpersonatedByUserId { get; }

    /// <summary>Whether the staff member requires co-signature on clinical notes.</summary>
    bool RequiresCoSignature { get; }

    /// <summary>Whether the current user is a SuperAdmin (bypasses clinical authorization).</summary>
    bool IsSuperAdmin { get; }

    /// <summary>The user's granted permissions (resolved from roles).</summary>
    IReadOnlySet<string> Permissions { get; }

    /// <summary>Check if the user has a specific permission code.</summary>
    bool HasPermission(string permissionCode);

    /// <summary>Check if the staff member is currently affiliated with a specific department.</summary>
    bool IsAffiliatedWith(Guid departmentId);
}

/// <summary>
/// Represents a single active department affiliation for the current user.
/// </summary>
public record StaffAffiliationContext(
    Guid AffiliationId,
    Guid DepartmentId,
    string DepartmentName,
    string ClinicalRole,
    bool IsPrimary,
    DateOnly EffectiveStartDate,
    DateOnly? EffectiveEndDate);
