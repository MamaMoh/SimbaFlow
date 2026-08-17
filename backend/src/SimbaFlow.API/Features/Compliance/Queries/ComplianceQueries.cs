using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Compliance.Queries;

public record ComplianceAlertDto(
    Guid CandidateId,
    string CandidateName,
    string? Passport,
    string Category,
    string Detail,
    DateOnly? ExpiryDate,
    int? DaysRemaining,
    string Bucket);

public record ComplianceAlertsResult(
    int ExpiredCount,
    int Within30Count,
    int Within90Count,
    List<ComplianceAlertDto> Alerts);

public record GetComplianceAlertsQuery
    : IRequest<Result<ComplianceAlertsResult>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public class GetComplianceAlertsHandler
    : IRequestHandler<GetComplianceAlertsQuery, Result<ComplianceAlertsResult>>
{
    private readonly ITenantDbContext _context;
    private readonly IPlatformDbContext _platform;
    private readonly ICurrentUserService _currentUser;

    public GetComplianceAlertsHandler(
        ITenantDbContext context,
        IPlatformDbContext platform,
        ICurrentUserService currentUser)
    {
        _context = context;
        _platform = platform;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Partner agreements and the agency's own MoLS licence expire too — and a lapsed agreement
    /// blocks placements, so it belongs in the same "what needs attention" list as documents.
    /// </summary>
    private async Task<List<ComplianceAlertDto>> BuildPartnerAndLicenceAlertsAsync(
        DateOnly today, DateOnly horizon, CancellationToken ct)
    {
        var alerts = new List<ComplianceAlertDto>();
        if (_currentUser.TenantId is not Guid tid || tid == Guid.Empty) return alerts;

        // Partner agreements
        var links = await (
            from link in _platform.PartnerLinks.AsNoTracking()
            join p in _platform.PartnerAgencies.AsNoTracking() on link.PartnerAgencyId equals p.Id
            where link.TenantId == tid && !link.IsDeleted && !p.IsDeleted
            select new { p.Name, p.CountryName, link.AgreementStart, link.AgreementEnd, link.Status }
        ).ToListAsync(ct);

        foreach (var l in links)
        {
            var state = PartnerAgreementRules.Evaluate(l.AgreementStart, l.AgreementEnd, l.Status, today);
            if (state is AgreementState.Active or AgreementState.NotStarted) continue;
            if (l.AgreementEnd > horizon) continue;

            var days = PartnerAgreementRules.DaysRemaining(l.AgreementEnd, today);
            alerts.Add(new ComplianceAlertDto(
                Guid.Empty, l.Name, l.CountryName, "Partner agreement",
                PartnerAgreementRules.Describe(state, days),
                l.AgreementEnd, days, BucketFor(days)));
        }

        // The agency's own licence
        var tenant = await _platform.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tid, ct);
        if (tenant?.LicenseExpiresAt is DateOnly licenceEnd && licenceEnd <= horizon)
        {
            var days = licenceEnd.DayNumber - today.DayNumber;
            alerts.Add(new ComplianceAlertDto(
                Guid.Empty, tenant.Name, tenant.LicenseNumber, "Agency licence",
                days < 0 ? $"Licence expired {Math.Abs(days)} day(s) ago" : $"Licence expires in {days} day(s)",
                licenceEnd, days, BucketFor(days)));
        }

        return alerts;
    }

    public async Task<Result<ComplianceAlertsResult>> Handle(
        GetComplianceAlertsQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(90);

        // Bound the scan: only candidates with a passport expiring within the horizon
        // (SQL-filtered) or carrying denormalized status values (for Tasheer checks).
        // Ordered by soonest expiry and capped to keep memory/latency bounded.
        const int MaxScan = 5000;
        var candidates = await _context.Candidates.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active
                && ((c.PassportExpiryDate != null && c.PassportExpiryDate <= horizon)
                    || c.CurrentStatusValues != null))
            .OrderBy(c => c.PassportExpiryDate)
            .Select(c => new
            {
                c.Id, c.FirstName, c.LastName, c.PassportNumber,
                c.PassportExpiryDate, c.CurrentStatusValues
            })
            .Take(MaxScan)
            .ToListAsync(ct);

        var alerts = new List<ComplianceAlertDto>();

        foreach (var c in candidates)
        {
            var name = $"{c.FirstName} {c.LastName}".Trim();

            // Passport expiry (has real dates)
            if (c.PassportExpiryDate is { } expiry && expiry <= horizon)
            {
                var days = expiry.DayNumber - today.DayNumber;
                alerts.Add(new ComplianceAlertDto(
                    c.Id, name, c.PassportNumber, "Passport",
                    days < 0 ? $"Passport expired {-days} day(s) ago" : $"Passport expires in {days} day(s)",
                    expiry, days, BucketFor(days)));
            }

            // Tasheer marked Expired in denormalized status values
            if (c.CurrentStatusValues is not null && IsTasheerExpired(c.CurrentStatusValues))
            {
                alerts.Add(new ComplianceAlertDto(
                    c.Id, name, c.PassportNumber, "Tasheer",
                    "Tasheer booking has expired", null, null, "expired"));
            }
        }

        alerts.AddRange(await BuildPartnerAndLicenceAlertsAsync(today, horizon, ct));

        var ordered = alerts
            .OrderBy(a => a.DaysRemaining ?? int.MinValue)
            .ToList();

        return Result<ComplianceAlertsResult>.Success(new ComplianceAlertsResult(
            ordered.Count(a => a.Bucket == "expired"),
            ordered.Count(a => a.Bucket == "within30"),
            ordered.Count(a => a.Bucket == "within90"),
            ordered));
    }

    private static string BucketFor(int days) =>
        days < 0 ? "expired" : days <= 30 ? "within30" : "within90";

    private static bool IsTasheerExpired(JsonDocument doc)
    {
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
        if (!doc.RootElement.TryGetProperty("tasheer", out var el)) return false;
        return el.ValueKind == JsonValueKind.String
            && string.Equals(el.GetString(), "Expired", StringComparison.OrdinalIgnoreCase);
    }
}
