using Microsoft.AspNetCore.Identity;

namespace SimbaFlow.Domain.Entities.Identity;

/// <summary>
/// Application role extending ASP.NET Core Identity with system flags and audit fields.
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public bool IsActive { get; set; } = true;

    // Multi-tenancy (future-ready)
    public Guid? TenantId { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }

    // Navigation
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
