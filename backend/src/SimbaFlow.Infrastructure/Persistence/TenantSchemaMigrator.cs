using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Infrastructure.Persistence;

/// <summary>
/// Applies TenantDbContext EF migrations into each agency schema via search_path.
/// </summary>
public interface ITenantSchemaMigrator
{
    Task EnsureSchemaAndMigrateAsync(string schemaName, CancellationToken cancellationToken = default);
    Task MigrateAllActiveTenantsAsync(CancellationToken cancellationToken = default);
}

public class TenantSchemaMigrator : ITenantSchemaMigrator
{
    private readonly IPlatformDbContext _platform;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantSchemaMigrator> _logger;

    public TenantSchemaMigrator(
        IPlatformDbContext platform,
        IConfiguration configuration,
        ILogger<TenantSchemaMigrator> logger)
    {
        _platform = platform;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task MigrateAllActiveTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await _platform.Tenants
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.SubscriptionStatus == TenantStatus.Active)
            .Select(t => t.SchemaName)
            .ToListAsync(cancellationToken);

        foreach (var schema in tenants.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            try
            {
                await EnsureSchemaAndMigrateAsync(schema, cancellationToken);
            }
            catch (Exception ex)
            {
                // Don't take down the whole API for one bad tenant schema in multi-tenant migrate-all.
                _logger.LogError(ex, "Failed to migrate tenant schema {Schema}; continuing with other tenants", schema);
            }
        }
    }

    public async Task EnsureSchemaAndMigrateAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        await using (var conn = new NpgsqlConnection(connectionString))
        {
            await conn.OpenAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{EscapeIdent(schemaName)}\"";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        var csb = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SearchPath = schemaName
        };

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(csb.ConnectionString, b =>
                b.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName)
                    .MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var context = new TenantDbContext(options, new MigratorCurrentUser());

        try
        {
            await context.Database.MigrateAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P07")
        {
            // Legacy DDL left some tables; only stamp history if the full InitialTenant surface exists.
            if (await TableExistsAsync(csb.ConnectionString, schemaName, "workflow_definitions", cancellationToken))
            {
                await StampAppliedMigrationsAsync(csb.ConnectionString, schemaName, cancellationToken);
                _logger.LogWarning(
                    "Schema {Schema} already had tenant tables; stamped EF migration history",
                    schemaName);
                return;
            }

            _logger.LogWarning(
                "Schema {Schema} is incomplete (candidates without workflow). Resetting schema and re-applying migrations",
                schemaName);
            await ResetSchemaAsync(csb.ConnectionString, schemaName, cancellationToken);
            await context.Database.MigrateAsync(cancellationToken);
        }

        _logger.LogInformation("Applied tenant migrations to schema {Schema}", schemaName);
    }

    private static async Task<bool> TableExistsAsync(
        string connectionString,
        string schemaName,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT EXISTS (
              SELECT 1
              FROM information_schema.tables
              WHERE table_schema = @schema AND table_name = @table
            )
            """;
        cmd.Parameters.AddWithValue("schema", schemaName);
        cmd.Parameters.AddWithValue("table", tableName);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    private static async Task ResetSchemaAsync(
        string connectionString,
        string schemaName,
        CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        // CASCADE drops dependent objects; safe for incomplete sample agency schemas.
        cmd.CommandText = $"""
            DROP SCHEMA IF EXISTS "{EscapeIdent(schemaName)}" CASCADE;
            CREATE SCHEMA "{EscapeIdent(schemaName)}";
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task StampAppliedMigrationsAsync(
        string connectionString,
        string schemaName,
        CancellationToken cancellationToken)
    {
        // Matches Migrations/Tenant/20260721075312_InitialTenant
        const string migrationId = "20260721075312_InitialTenant";
        const string productVersion = "10.0.9";

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            CREATE TABLE IF NOT EXISTS "{EscapeIdent(schemaName)}".__ef_migrations_history (
                migration_id character varying(150) NOT NULL,
                product_version character varying(32) NOT NULL,
                CONSTRAINT pk___ef_migrations_history PRIMARY KEY (migration_id)
            );
            INSERT INTO "{EscapeIdent(schemaName)}".__ef_migrations_history (migration_id, product_version)
            SELECT @id, @ver
            WHERE NOT EXISTS (
                SELECT 1 FROM "{EscapeIdent(schemaName)}".__ef_migrations_history WHERE migration_id = @id
            );
            """;
        cmd.Parameters.AddWithValue("id", migrationId);
        cmd.Parameters.AddWithValue("ver", productVersion);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string EscapeIdent(string name) => name.Replace("\"", "\"\"");

    private sealed class MigratorCurrentUser : ICurrentUserService
    {
        public string? UserId => null;
        public string? UserName => "migrator";
        public string? Email => null;
        public Guid? ActiveLocationId => null;
        public Guid? DepartmentId => null;
        public Guid? TenantId => null;
        public IReadOnlyList<string> Permissions => [];
        public IReadOnlyList<string> Roles => [];
        public bool IsSuperAdmin => true;
        public bool HasPermission(string permission) => true;
        public string? IpAddress => null;
        public string? UserAgent => null;
    }
}
