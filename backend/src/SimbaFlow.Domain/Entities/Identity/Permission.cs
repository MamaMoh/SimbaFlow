using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Identity;

/// <summary>
/// Granular permission representing a single access right (e.g., "candidate.read").
/// Permissions are grouped by module and assigned to roles.
/// </summary>
public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
