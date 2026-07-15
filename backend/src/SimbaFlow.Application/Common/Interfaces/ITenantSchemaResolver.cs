namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Resolves a tenant ID to its PostgreSQL schema name.
/// Implementations should cache the mapping for performance.
/// </summary>
public interface ITenantSchemaResolver
{
    /// <summary>
    /// Resolve the database schema name for a given tenant ID.
    /// Returns null if tenant not found or inactive.
    /// </summary>
    Task<string?> ResolveSchemaAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate cached schema mapping for a tenant (e.g., on tenant update).
    /// </summary>
    void InvalidateCache(Guid tenantId);
}
