namespace SimbaFlow.Domain.Entities.Tenancy;

/// <summary>
/// Maps a tenant role to a system-defined permission code.
/// This is how agencies assign permissions to their custom roles.
/// </summary>
public class TenantRolePermission
{
    public Guid TenantRoleId { get; set; }
    public string PermissionCode { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public string? GrantedBy { get; set; }

    // Navigation
    public TenantRole? TenantRole { get; set; }
}
