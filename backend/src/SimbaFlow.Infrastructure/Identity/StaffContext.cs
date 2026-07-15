using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Infrastructure.Identity;

/// <summary>
/// Scoped implementation of IStaffContext, populated per-request by StaffContextMiddleware.
/// Holds the resolved clinical context for the current authenticated user.
/// </summary>
public class StaffContext : IStaffContext
{
    public Guid? UserId { get; set; }
    public Guid? StaffProfileId { get; set; }
    public bool HasStaffProfile => StaffProfileId.HasValue;
    public EmploymentStatus? EmploymentStatus { get; set; }
    public IReadOnlyList<StaffAffiliationContext> ActiveAffiliations { get; set; } = [];
    public StaffAffiliationContext? PrimaryAffiliation =>
        ActiveAffiliations.FirstOrDefault(a => a.IsPrimary)
        ?? ActiveAffiliations.FirstOrDefault();
    public Guid? PrimaryDepartmentId => PrimaryAffiliation?.DepartmentId;
    public bool IsImpersonated { get; set; }
    public Guid? ImpersonatedByUserId { get; set; }
    public bool RequiresCoSignature { get; set; }
    public bool IsSuperAdmin { get; set; }
    public IReadOnlySet<string> Permissions { get; set; } = new HashSet<string>();

    public bool HasPermission(string permissionCode) =>
        IsSuperAdmin || Permissions.Contains(permissionCode);

    public bool IsAffiliatedWith(Guid departmentId) =>
        ActiveAffiliations.Any(a => a.DepartmentId == departmentId);
}
