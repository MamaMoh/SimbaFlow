using System.Data.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Features.Tenants.Queries;

/// <summary>
/// Every agency, with enough about how it is being used for the platform team to spot one that
/// is stuck, idle or at its limit. Open to the support desk as well as the administrators.
/// </summary>
public record GetTenantsQuery : IRequest<Result<List<TenantListDto>>>, IRequirePlatformStaff;

public record TenantListDto(
    Guid Id,
    string Name,
    string Slug,
    string SchemaName,
    string ContactEmail,
    TenantStatus Status,
    DateTime ProvisionedAt,
    int AgencyLevel,
    int MaxPartnersPerCountry,
    int? MaxCountries,
    string? LicenseNumber,
    int LicensedCountryCount,
    DateTime? OffboardedAt,
    string? OffboardingReason,
    DateTime? LastExportedAt,
    // ──── Usage ────
    int ActiveUsers,
    int MaxUsers,
    DateTime? LastSignInAt,
    DateTime? LastActivityAt,
    int? Candidates,
    int? ActiveCandidates,
    int? CandidatesAddedLast30Days,
    DateOnly? LicenseExpiresAt,
    DateTime? AutoSuspendedAt,
    int OpenSupportTickets);

public class GetTenantsHandler : IRequestHandler<GetTenantsQuery, Result<List<TenantListDto>>>
{
    private readonly PlatformDbContext _context;
    private readonly ILogger<GetTenantsHandler> _logger;

    public GetTenantsHandler(PlatformDbContext context, ILogger<GetTenantsHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<List<TenantListDto>>> Handle(GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var rows = await _context.Tenants
            .AsNoTracking()
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var users = await _context.Users.AsNoTracking()
            .Where(u => u.TenantId != null && !u.IsDeleted)
            .GroupBy(u => u.TenantId!.Value)
            .Select(g => new
            {
                TenantId = g.Key,
                Active = g.Count(u => u.IsActive),
                LastSignIn = g.Max(u => u.LastLoginAt),
            })
            .ToDictionaryAsync(x => x.TenantId, cancellationToken);

        var activity = await _context.AuditLogs.AsNoTracking()
            .Where(a => a.TenantId != null)
            .GroupBy(a => a.TenantId!.Value)
            .Select(g => new { TenantId = g.Key, Last = g.Max(a => a.Timestamp) })
            .ToDictionaryAsync(x => x.TenantId, x => x.Last, cancellationToken);

        var openTickets = await _context.SupportTickets.AsNoTracking()
            .Where(t => !t.IsDeleted && t.Status == Domain.Entities.Platform.SupportTicketStatus.Open)
            .GroupBy(t => t.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        var candidates = await CandidateCountsAsync(
            rows.Where(t => t.PurgedAt == null).Select(t => (t.Id, t.SchemaName)).ToList(),
            cancellationToken);

        var tenants = rows.Select(t =>
        {
            var (perCountry, maxCountries) = AgencyLevelRules.GetCaps(t.AgencyLevel);
            users.TryGetValue(t.Id, out var u);
            candidates.TryGetValue(t.Id, out var c);
            return new TenantListDto(
                t.Id, t.Name, t.Slug, t.SchemaName,
                t.ContactEmail, t.SubscriptionStatus, t.ProvisionedAt,
                t.AgencyLevel, perCountry, maxCountries,
                t.LicenseNumber, t.LicensedCountries?.Count ?? 0,
                t.OffboardedAt, t.OffboardingReason, t.LastExportedAt,
                u?.Active ?? 0,
                t.MaxUsers,
                Utc(u?.LastSignIn),
                activity.TryGetValue(t.Id, out var last) ? Utc(last) : null,
                c?.Total, c?.Active, c?.Recent,
                t.LicenseExpiresAt,
                t.AutoSuspendedAt,
                openTickets.GetValueOrDefault(t.Id));
        }).ToList();

        return Result<List<TenantListDto>>.Success(tenants);
    }

    private static DateTime? Utc(DateTime? value) =>
        value is DateTime v ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : null;

    private sealed record CandidateCounts(int Total, int Active, int Recent);

    /// <summary>
    /// Candidate counts from each agency's own schema, one small query per agency on one
    /// connection. An agency whose schema cannot be read (still provisioning, damaged) is left
    /// without numbers rather than failing the whole list.
    /// </summary>
    private async Task<Dictionary<Guid, CandidateCounts>> CandidateCountsAsync(
        List<(Guid Id, string Schema)> agencies, CancellationToken ct)
    {
        var result = new Dictionary<Guid, CandidateCounts>();
        if (agencies.Count == 0 || !_context.Database.IsRelational())
            return result;

        var conn = _context.Database.GetDbConnection();
        var opened = false;
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
            opened = true;
        }

        try
        {
            var since = DateTime.UtcNow.AddDays(-30);
            foreach (var (id, schema) in agencies)
            {
                if (string.IsNullOrWhiteSpace(schema)) continue;
                try
                {
                    await using DbCommand cmd = conn.CreateCommand();
                    cmd.CommandText =
                        "SELECT count(*) FILTER (WHERE NOT is_deleted), "
                        + "count(*) FILTER (WHERE NOT is_deleted AND status = 0), "
                        + "count(*) FILTER (WHERE NOT is_deleted AND created_at >= @since) "
                        + $"FROM {Ident(schema)}.candidates";
                    var p = cmd.CreateParameter();
                    p.ParameterName = "since";
                    p.Value = since;
                    cmd.Parameters.Add(p);

                    await using var reader = await cmd.ExecuteReaderAsync(ct);
                    if (await reader.ReadAsync(ct))
                        result[id] = new CandidateCounts(
                            (int)reader.GetInt64(0), (int)reader.GetInt64(1), (int)reader.GetInt64(2));
                }
                catch (DbException ex)
                {
                    _logger.LogInformation(ex, "Could not count candidates for agency {TenantId}", id);
                }
            }
        }
        finally
        {
            if (opened) await conn.CloseAsync();
        }

        return result;
    }

    private static string Ident(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
}
