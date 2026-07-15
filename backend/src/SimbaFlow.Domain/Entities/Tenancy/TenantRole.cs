using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Tenancy;

/// <summary>
/// A custom role defined by an agency within their tenant schema.
/// Each agency can create their own roles and assign system-defined permissions to them.
/// </summary>
public class TenantRole : BaseEntity
{
    /// <summary>Display name of the role (agency can name it anything).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short code/slug for the role (unique within tenant).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Description of what this role can do.</summary>
    public string? Description { get; set; }

    /// <summary>Whether this is a system-created default role (cannot be deleted by agency).</summary>
    public bool IsSystemRole { get; set; }

    /// <summary>Whether this role is currently active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Sort order for display.</summary>
    public int SortOrder { get; set; }

    // Navigation
    public ICollection<TenantRolePermission> Permissions { get; set; } = [];
    public ICollection<TenantUserRole> UserRoles { get; set; } = [];
}
