using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Infrastructure.Persistence;

/// <summary>
/// Simple implementation of ITenantContext resolved from the current user's claims.
/// </summary>
public class TenantContext : ITenantContext
{
    public TenantContext(Guid? tenantId, string? schemaName, Guid? officeId, bool isSystemAdmin)
    {
        TenantId = tenantId;
        SchemaName = schemaName;
        OfficeId = officeId;
        IsSystemAdmin = isSystemAdmin;
    }

    public Guid? TenantId { get; }
    public string? SchemaName { get; }
    public Guid? OfficeId { get; }
    public bool IsSystemAdmin { get; }
}
