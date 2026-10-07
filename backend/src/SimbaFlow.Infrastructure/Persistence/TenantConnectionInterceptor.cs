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
            // Platform admins have no JWT tenant_id — a tenant-scoped query from one means "the
            // agency I am working inside", which is whichever the switcher picked, or the default
            // when it picked none.
            schemaName = await _schemaResolver.ResolveDefaultSchemaAsync(cancellationToken);

            // This used to fall back to the literal "tenant_default_agency". Postgres accepts a
            // search_path naming a schema that does not exist, so the SET succeeded and nothing
            // failed until the first query — which came back as `relation "candidates" does not
            // exist`, once per dashboard panel, filling the error log with a database fault that
            // said nothing about the cause. It is also why the seeded "Default Agency", whose row
            // was created without ever building its schema, broke every page it touched.
            //
            // When no agency resolves there is no schema to point at, and saying so is the only
            // honest answer. The handler turns this into a 403 and a warning rather than a 500.
            if (string.IsNullOrWhiteSpace(schemaName))
            {
                throw new Application.Common.Exceptions.TenantUnavailableException(
                    "No agency is selected. Choose one in the agency switcher, or create an agency "
                    + "first.");
            }
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
