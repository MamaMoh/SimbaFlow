using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Identity;

/// <summary>
/// Organizational department forming a hierarchical tree.
/// Used for routing, reporting, and access scoping.
/// </summary>
public class Department : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ParentDepartmentId { get; set; }
    public Department? ParentDepartment { get; set; }
    public Guid? HeadUserId { get; set; }
    public ApplicationUser? HeadUser { get; set; }
    public bool IsActive { get; set; } = true;

    // Multi-tenancy (future-ready)
    public Guid? TenantId { get; set; }

    // Navigation
    public ICollection<Department> ChildDepartments { get; set; } = [];
    public ICollection<ApplicationUser> Users { get; set; } = [];
}
