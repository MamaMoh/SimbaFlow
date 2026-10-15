using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Infrastructure.Persistence.Seeds;

namespace SimbaFlow.Infrastructure.Persistence;

/// <summary>
/// The parts of an agency's life that touch its schema and its files as a whole: building the
/// schema, exporting everything in it, and — at the very end — destroying it.
/// </summary>
public interface ITenantLifecycleService
{
    /// <summary>
    /// Create or bring the agency's schema up to date, and seed what every agency starts with.
    ///
    /// Safe to repeat. Provisioning calls it for a new agency; reactivation calls it again because
    /// startup only migrates active agencies, so one that was away may have missed migrations.
    /// </summary>
    Task PrepareSchemaAsync(string schemaName, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Write a zip of everything the platform holds for this agency to <paramref name="output"/>:
    /// every table in its schema as CSV, its platform records as JSON, and every file its records
    /// point to.
    /// </summary>
    Task<TenantExportSummary> WriteExportAsync(TenantInfo tenant, Stream output, CancellationToken ct = default);

    /// <summary>
    /// Destroy the agency's files and drop its schema. Not reversible. Returns how many files went.
    /// </summary>
    Task<int> DestroyDataAsync(TenantInfo tenant, CancellationToken ct = default);
}

public record TenantExportSummary(int Tables, long Rows, int Files, int MissingFiles);

public class TenantLifecycleService : ITenantLifecycleService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly IPlatformDbContext _platform;
    private readonly ITenantSchemaMigrator _migrator;
    private readonly IWorkflowDefinitionUpgrader _workflowUpgrader;
    private readonly IFinanceSeedService _financeSeed;
    private readonly IFileStorageService _storage;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantLifecycleService> _logger;

    public TenantLifecycleService(
        IPlatformDbContext platform,
        ITenantSchemaMigrator migrator,
        IWorkflowDefinitionUpgrader workflowUpgrader,
        IFinanceSeedService financeSeed,
        IFileStorageService storage,
        IConfiguration configuration,
        ILogger<TenantLifecycleService> logger)
    {
        _platform = platform;
        _migrator = migrator;
        _workflowUpgrader = workflowUpgrader;
        _financeSeed = financeSeed;
        _storage = storage;
        _configuration = configuration;
        _logger = logger;
    }

    private string ConnectionString =>
        _configuration.GetConnectionString("DefaultConnection") is { Length: > 0 } cs
            ? cs
            // Carrying on without one left an agency with tables and no workflow, which fails later
            // as "no stages" rather than here as "not configured".
            : throw new InvalidOperationException(
                "No DefaultConnection is configured, so the agency's schema cannot be reached.");

    public async Task PrepareSchemaAsync(string schemaName, Guid tenantId, CancellationToken ct = default)
    {
        await _migrator.EnsureSchemaAndMigrateAsync(schemaName, ct);

        var cs = ConnectionString;
        await WorkflowSeeder.SeedDefaultWorkflowIntoSchemaAsync(cs, schemaName, tenantId, ct);
        await _workflowUpgrader.EnsureUnit3ArtifactsIntoSchemaAsync(cs, schemaName, tenantId, ct);
        await _workflowUpgrader.EnsureUnit4ArtifactsIntoSchemaAsync(cs, schemaName, tenantId, ct);
        await _financeSeed.EnsureUnit5ArtifactsIntoSchemaAsync(cs, schemaName, tenantId, ct);
    }

    public async Task<TenantExportSummary> WriteExportAsync(
        TenantInfo tenant, Stream output, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync(ct);

        using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        // ── The agency's own schema: every table, whole, as the database holds it. ──
        // COPY rather than EF so that nothing is left out for want of a mapped entity, and so a
        // table added next month is exported without anyone remembering to add it here.
        var tables = await TablesInAsync(conn, tenant.SchemaName, ct);
        long rows = 0;
        foreach (var table in tables)
        {
            var entry = zip.CreateEntry($"database/{table}.csv", CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await using var writer = new StreamWriter(entryStream);
            using var reader = await conn.BeginTextExportAsync(
                $"COPY {Ident(tenant.SchemaName)}.{Ident(table)} TO STDOUT WITH (FORMAT csv, HEADER)", ct);

            // Copied as characters, not lines: a quoted value may hold a line break of its own.
            var buffer = new char[16 * 1024];
            int read;
            while ((read = await reader.ReadAsync(buffer, ct)) > 0)
                await writer.WriteAsync(buffer.AsMemory(0, read), ct);
        }

        foreach (var table in tables)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT count(*) FROM {Ident(tenant.SchemaName)}.{Ident(table)}";
            rows += (long)(await cmd.ExecuteScalarAsync(ct))!;
        }

        // ── What the platform holds about the agency, outside its schema. ──
        var tenantId = tenant.Id;
        await WriteJsonAsync(zip, "platform/agency.json", new
        {
            tenant.Id, tenant.Name, tenant.Slug, tenant.SchemaName,
            tenant.ContactEmail, tenant.ContactPhone, tenant.Address, tenant.City, tenant.Country,
            Status = tenant.SubscriptionStatus.ToString(),
            BillingCycle = tenant.BillingCycle.ToString(),
            tenant.SubscriptionAmount, tenant.SubscriptionCurrency, tenant.NextPaymentDue,
            tenant.MaxUsers, tenant.ProvisionedAt, tenant.ProvisionedBy,
            tenant.AgencyLevel, tenant.LicenseNumber, tenant.LicenseIssuedAt, tenant.LicenseExpiresAt,
            LicenseStatus = tenant.LicenseStatus.ToString(), tenant.LicensedCountries,
            tenant.CapitalEtb, tenant.BondUsd,
            tenant.Settings,
            tenant.OffboardedAt, tenant.OffboardedBy, tenant.OffboardingReason,
        }, ct);

        // Users without password hashes, security stamps or MFA secrets: an export is a copy of
        // the agency's records, not of anyone's credentials.
        var users = await _platform.ApplicationUsers.AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.CreatedAt)
            .Select(u => new
            {
                u.Id, u.UserName, u.Email, u.FirstName, u.MiddleName, u.LastName, u.PhoneNumber,
                u.IsActive, u.IsDeleted, u.CreatedAt,
            })
            .ToListAsync(ct);
        await WriteJsonAsync(zip, "platform/users.json", users, ct);

        var invoices = await _platform.SubscriptionInvoices.AsNoTracking()
            .Where(i => i.TenantId == tenantId)
            .ToListAsync(ct);
        await WriteJsonAsync(zip, "platform/invoices.json", invoices, ct);

        var partnerLinks = await _platform.PartnerLinks.AsNoTracking()
            .Where(l => l.TenantId == tenantId)
            .Select(l => new { l.Id, l.PartnerAgencyId, PartnerName = l.PartnerAgency!.Name, l.AgreementStart, l.AgreementEnd, Status = l.Status.ToString(), l.CreatedAt, l.IsDeleted })
            .ToListAsync(ct);
        await WriteJsonAsync(zip, "platform/partner-links.json", partnerLinks, ct);

        var agreements = await _platform.PartnerAgreementDocuments.AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .Select(d => new { d.Id, d.PartnerLinkId, d.Title, d.OriginalFileName, d.FilePath, d.UploadedAt, d.UploadedBy, d.IsDeleted })
            .ToListAsync(ct);
        await WriteJsonAsync(zip, "platform/partner-agreements.json", agreements, ct);

        // ── Files: whatever any of the above points to, under the path it is stored at. ──
        var paths = await FilePathsAsync(conn, tenant, ct);
        paths.UnionWith(agreements.Select(a => a.FilePath).Where(p => !string.IsNullOrWhiteSpace(p)));

        int files = 0, missing = 0;
        foreach (var path in paths.Order(StringComparer.Ordinal))
        {
            Stream? source;
            try { source = await _storage.DownloadAsync(path, ct); }
            catch (InvalidOperationException) { source = null; } // a path outside storage is not ours to read
            if (source is null) { missing++; continue; }

            await using (source)
            {
                var entry = zip.CreateEntry($"files/{path.Replace('\\', '/')}", CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await source.CopyToAsync(entryStream, ct);
            }
            files++;
        }

        var summary = new TenantExportSummary(tables.Count, rows, files, missing);
        await WriteJsonAsync(zip, "manifest.json", new
        {
            ExportedAt = DateTime.UtcNow,
            Agency = tenant.Name,
            tenant.Slug,
            tenant.SchemaName,
            summary.Tables,
            summary.Rows,
            summary.Files,
            summary.MissingFiles,
            Layout = new
            {
                Database = "database/<table>.csv — every table in the agency's schema, with a header row",
                Platform = "platform/*.json — the agency's record, users (no credentials), invoices and partner links",
                Files = "files/<stored path> — every document, photo and logo the records point to",
            },
        }, ct);

        _logger.LogInformation(
            "Exported agency {Slug}: {Tables} tables, {Rows} rows, {Files} files ({Missing} missing)",
            tenant.Slug, summary.Tables, summary.Rows, summary.Files, summary.MissingFiles);

        return summary;
    }

    public async Task<int> DestroyDataAsync(TenantInfo tenant, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync(ct);

        // Files first: their paths live in the schema, so once the schema is gone nothing says
        // where they were.
        var paths = await FilePathsAsync(conn, tenant, ct);
        var agreementPaths = await _platform.PartnerAgreementDocuments.AsNoTracking()
            .Where(d => d.TenantId == tenant.Id)
            .Select(d => d.FilePath)
            .ToListAsync(ct);
        paths.UnionWith(agreementPaths.Where(p => !string.IsNullOrWhiteSpace(p)));

        var destroyed = 0;
        foreach (var path in paths)
        {
            try
            {
                await _storage.DestroyAsync(path, ct);
                destroyed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Could not destroy {Path} while purging {Slug}", path, tenant.Slug);
            }
        }

        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"DROP SCHEMA IF EXISTS {Ident(tenant.SchemaName)} CASCADE";
            await cmd.ExecuteNonQueryAsync(ct);
        }

        _logger.LogWarning("Purged agency {Slug}: dropped schema {Schema}, destroyed {Files} files",
            tenant.Slug, tenant.SchemaName, destroyed);

        return destroyed;
    }

    private static async Task<List<string>> TablesInAsync(NpgsqlConnection conn, string schema, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT table_name FROM information_schema.tables
            WHERE table_schema = @schema AND table_type = 'BASE TABLE'
            ORDER BY table_name
            """;
        cmd.Parameters.AddWithValue("schema", schema);

        var tables = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            tables.Add(reader.GetString(0));
        return tables;
    }

    /// <summary>
    /// Every stored file the agency's records point to.
    ///
    /// Found by column name — any text column ending in "_path" — rather than by a list of
    /// entities, so that a new kind of upload is exported and purged without a change here.
    /// </summary>
    private static async Task<HashSet<string>> FilePathsAsync(
        NpgsqlConnection conn, TenantInfo tenant, CancellationToken ct)
    {
        var columns = new List<(string Table, string Column)>();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT c.table_name, c.column_name
                FROM information_schema.columns c
                JOIN information_schema.tables t
                  ON t.table_schema = c.table_schema AND t.table_name = c.table_name
                WHERE c.table_schema = @schema
                  AND t.table_type = 'BASE TABLE'
                  AND c.column_name LIKE '%\_path'
                  AND c.data_type IN ('text', 'character varying')
                """;
            cmd.Parameters.AddWithValue("schema", tenant.SchemaName);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                columns.Add((reader.GetString(0), reader.GetString(1)));
        }

        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (table, column) in columns)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                $"SELECT DISTINCT {Ident(column)} FROM {Ident(tenant.SchemaName)}.{Ident(table)} " +
                $"WHERE {Ident(column)} IS NOT NULL AND {Ident(column)} <> ''";
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                paths.Add(reader.GetString(0));
        }

        if (!string.IsNullOrWhiteSpace(tenant.LogoPath)) paths.Add(tenant.LogoPath);
        if (!string.IsNullOrWhiteSpace(tenant.LetterheadPath)) paths.Add(tenant.LetterheadPath);

        // Only paths into storage. A column ending in _path that holds a URL is not a file we keep.
        paths.RemoveWhere(p => p.Contains("://", StringComparison.Ordinal));
        return paths;
    }

    private static async Task WriteJsonAsync(ZipArchive zip, string name, object value, CancellationToken ct)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, Json, ct);
    }

    private static string Ident(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
}
