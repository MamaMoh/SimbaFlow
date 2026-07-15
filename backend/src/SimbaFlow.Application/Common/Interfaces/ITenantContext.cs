namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Provides the current tenant context resolved from the authenticated user's JWT.
/// </summary>
public interface ITenantContext
{
    /// <summary>The current tenant's ID. Null for system administrators.</summary>
    Guid? TenantId { get; }

    /// <summary>The current tenant's database schema name. Null for system administrators.</summary>
    string? SchemaName { get; }

    /// <summary>The current user's office ID within the tenant.</summary>
    Guid? OfficeId { get; }

    /// <summary>True if the current user is a system administrator (no tenant scope).</summary>
    bool IsSystemAdmin { get; }
}
