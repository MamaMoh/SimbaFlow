using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Infrastructure.Persistence;

/// <summary>
/// EF Core connection interceptor that sets PostgreSQL search_path
/// to the current tenant's schema on every connection open.
/// This ensures all queries are scoped to the tenant's data.
/// </summary>
public class TenantConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantSchemaResolver _schemaResolver;
    private readonly ILogger<TenantConnectionInterceptor> _logger;

    public TenantConnectionInterceptor(
        ICurrentUserService currentUser,
        ITenantSchemaResolver schemaResolver,
        ILogger<TenantConnectionInterceptor> logger)
    {
        _currentUser = currentUser;
        _schemaResolver = schemaResolver;
        _logger = logger;
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentUser.TenantId;
        string? schemaName = null;

        if (tenantId.HasValue)
        {
            schemaName = await _schemaResolver.ResolveSchemaAsync(tenantId.Value, cancellationToken);
        }
        else if (_currentUser.IsSuperAdmin)
        {
            // Platform admins have no JWT tenant_id — default to the seeded agency schema
            // so tenant DbSets (workflow, candidates) resolve instead of public.
            schemaName = await _schemaResolver.ResolveDefaultSchemaAsync(cancellationToken)
                ?? "tenant_default_agency";
        }

        if (!string.IsNullOrWhiteSpace(schemaName))
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SET search_path TO \"{schemaName}\", \"public\"";
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            _logger.LogDebug(
                "Set search_path to {Schema} for tenant {TenantId} (superAdmin={IsSuperAdmin})",
                schemaName,
                tenantId,
                _currentUser.IsSuperAdmin);
        }

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }
}
