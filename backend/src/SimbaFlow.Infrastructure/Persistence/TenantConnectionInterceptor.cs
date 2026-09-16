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

            // Say so here rather than letting every later query fail on a missing table. A user
            // whose agency was removed or suspended keeps a perfectly valid token, so without this
            // they sign in successfully and then meet a 500 on each page they open.
            if (string.IsNullOrWhiteSpace(schemaName))
            {
                throw new Application.Common.Exceptions.TenantUnavailableException(
                    "Your agency is not available — it may have been removed or suspended. "
                    + "Contact your administrator.");
            }
        }
        else if (_currentUser.IsSuperAdmin)
        {
            // Platform admins have no JWT tenant_id — default to the seeded agency schema
            // so tenant DbSets (workflow, candidates) resolve instead of public.
            schemaName = await _schemaResolver.ResolveDefaultSchemaAsync(cancellationToken)
                ?? "tenant_default_agency";
        }
        else
        {
            // Neither tenant-bound nor platform: there is no schema this caller is entitled to.
            //
            // This branch has to exist. Connections come from a pool, so leaving schemaName null
            // and skipping the SET does not mean "no schema" — it means the session keeps whichever
            // schema the last borrower of this connection was pointed at, and the caller reads a
            // tenant at random. "public" is the one choice that holds no tenant's data: a genuinely
            // tenant-scoped query then fails on a missing table, which is the correct outcome for a
            // caller who should not have reached one.
            schemaName = "public";

            _logger.LogWarning(
                "Tenant-scoped query from a caller with no tenant and no platform role (user {UserId}); "
                + "search_path pinned to public.",
                _currentUser.UserId);
        }

        await using var cmd = connection.CreateCommand();
        // The schema name reaches SQL as an identifier and cannot be parameterised, so it is quoted
        // and any embedded quote doubled — matching TenantSchemaMigrator, which has always done this.
        cmd.CommandText = $"SET search_path TO \"{EscapeIdent(schemaName)}\", \"public\"";
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogDebug(
            "Set search_path to {Schema} for tenant {TenantId} (superAdmin={IsSuperAdmin})",
            schemaName,
            tenantId,
            _currentUser.IsSuperAdmin);

        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    /// <summary>Doubles embedded quotes so a schema name cannot close its own quoted identifier.</summary>
    private static string EscapeIdent(string name) => name.Replace("\"", "\"\"");
}
