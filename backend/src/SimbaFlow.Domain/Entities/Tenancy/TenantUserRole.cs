namespace SimbaFlow.Domain.Entities.Tenancy;

/// <summary>
/// Assigns a user to a role within the tenant.
/// A user can have multiple roles within their agency.
/// </summary>
public class TenantUserRole
{
    public Guid UserId { get; set; }
    public Guid TenantRoleId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public string? AssignedBy { get; set; }

    // Navigation
    public TenantRole? TenantRole { get; set; }
}
