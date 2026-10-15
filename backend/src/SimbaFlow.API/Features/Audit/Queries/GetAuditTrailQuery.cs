using System.Text.RegularExpressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Features.Audit.Queries;

/// <summary>Which of the two logs to read: what people changed, or what they looked at.</summary>
public enum AuditTrailKind
{
    Changes,
    Views,
}

public record AuditTrailEntryDto(
    Guid Id,
    DateTime At,
    string? UserId,
    string UserName,
    string? UserEmail,
    string Action,
    string RawAction,
    string? EntityType,
    string? EntityId,
    string? EntityLabel,
    bool Success,
    string? IpAddress,
    string? UserAgent,
    int? DurationMs);

public record AuditTrailOption(string Value, string Label);

public record AuditTrailPage(
    List<AuditTrailEntryDto> Items,
    int Total,
    int Page,
    int PageSize,
    List<AuditTrailOption> Users,
    List<AuditTrailOption> Actions);

/// <summary>
/// One agency's audit trail, newest first. Owners and auditors hold <c>audit.read</c>; nobody
/// sees another agency's rows, because both logs are filtered on the agency they were written in.
/// </summary>
public record GetAuditTrailQuery(
    AuditTrailKind Kind = AuditTrailKind.Changes,
    DateOnly? From = null,
    DateOnly? To = null,
    string? UserId = null,
    string? Action = null,
    string? EntityId = null,
    bool FailedOnly = false,
    int Page = 1,
    int PageSize = 50) : IRequest<Result<AuditTrailPage>>, IRequirePermission
{
    public string RequiredPermission => "audit.read";
}

public class GetAuditTrailHandler : IRequestHandler<GetAuditTrailQuery, Result<AuditTrailPage>>
{
    private const int MaxPageSize = 100;

    private readonly PlatformDbContext _platform;
    private readonly ITenantDbContext _tenant;
    private readonly ICurrentUserService _currentUser;

    public GetAuditTrailHandler(PlatformDbContext platform, ITenantDbContext tenant, ICurrentUserService currentUser)
    {
        _platform = platform;
        _tenant = tenant;
        _currentUser = currentUser;
    }

    public async Task<Result<AuditTrailPage>> Handle(GetAuditTrailQuery request, CancellationToken ct)
    {
        if (_currentUser.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return Result<AuditTrailPage>.Failure("Choose an agency to see its audit trail.", 400);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        // Dates are whole days in UTC; "to" includes the whole of that day.
        DateTime? from = request.From?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime? to = request.To?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var rows = request.Kind == AuditTrailKind.Views
            ? ViewRows(tenantId, from, to)
            : ChangeRows(tenantId, from, to, request.FailedOnly);

        var actions = await rows.Select(r => r.Action).Distinct().ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(request.UserId))
            rows = rows.Where(r => r.UserId == request.UserId);
        if (!string.IsNullOrWhiteSpace(request.Action))
            rows = rows.Where(r => r.Action == request.Action);
        if (!string.IsNullOrWhiteSpace(request.EntityId))
            rows = rows.Where(r => r.EntityId == request.EntityId);

        var total = await rows.CountAsync(ct);
        var pageRows = await rows
            .OrderByDescending(r => r.At)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var people = await _platform.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync(ct);

        // Rows can also come from people outside the agency's own staff — a platform administrator
        // working inside it — so names are looked up for the page's authors, not only the staff list.
        var authorIds = pageRows
            .Select(r => Guid.TryParse(r.UserId, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var authors = await _platform.Users.AsNoTracking()
            .Where(u => authorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToDictionaryAsync(u => u.Id.ToString(), ct);

        var candidateLabels = await CandidateLabelsAsync(pageRows, ct);

        var items = pageRows.Select(r =>
        {
            authors.TryGetValue(r.UserId ?? "", out var author);
            string? label = null;
            if (r.EntityId is not null && r.EntityType == "Candidate")
                candidateLabels.TryGetValue(r.EntityId, out label);

            return new AuditTrailEntryDto(
                r.Id,
                DateTime.SpecifyKind(r.At, DateTimeKind.Utc),
                r.UserId,
                author is null ? (r.UserId is null ? "System" : "Unknown user") : FullName(author.FirstName, author.LastName, author.Email),
                author?.Email,
                AuditActionNames.Describe(r.Action),
                r.Action,
                r.EntityType,
                r.EntityId,
                label,
                r.Success,
                r.IpAddress,
                r.UserAgent,
                r.DurationMs);
        }).ToList();

        var userOptions = people
            .Select(u => new AuditTrailOption(u.Id.ToString(), FullName(u.FirstName, u.LastName, u.Email)))
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var actionOptions = actions
            .Select(a => new AuditTrailOption(a, AuditActionNames.Describe(a)))
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Result<AuditTrailPage>.Success(
            new AuditTrailPage(items, total, page, pageSize, userOptions, actionOptions));
    }

    // Initialised by member, not by constructor: EF can keep filtering and ordering on a
    // member-init projection, but not on one built through a constructor.
    private sealed class Row
    {
        public Guid Id { get; init; }
        public DateTime At { get; init; }
        public string? UserId { get; init; }
        public string Action { get; init; } = string.Empty;
        public string? EntityType { get; init; }
        public string? EntityId { get; init; }
        public bool Success { get; init; }
        public string? IpAddress { get; init; }
        public string? UserAgent { get; init; }
        public int? DurationMs { get; init; }
    }

    private IQueryable<Row> ChangeRows(Guid tenantId, DateTime? from, DateTime? to, bool failedOnly)
    {
        var q = _platform.AuditLogs.AsNoTracking().Where(a => a.TenantId == tenantId);
        if (from is not null) q = q.Where(a => a.Timestamp >= from);
        if (to is not null) q = q.Where(a => a.Timestamp < to);
        if (failedOnly) q = q.Where(a => !a.Success);
        return q.Select(a => new Row
        {
            Id = a.Id, At = a.Timestamp, UserId = a.UserId, Action = a.Action,
            EntityType = a.EntityType, EntityId = a.EntityId, Success = a.Success,
            IpAddress = a.IpAddress, UserAgent = a.UserAgent, DurationMs = a.DurationMs,
        });
    }

    private IQueryable<Row> ViewRows(Guid tenantId, DateTime? from, DateTime? to)
    {
        var q = _platform.ReadAuditLogs.AsNoTracking().Where(r => r.TenantId == tenantId);
        if (from is not null) q = q.Where(r => r.AccessedAt >= from);
        if (to is not null) q = q.Where(r => r.AccessedAt < to);
        return q.Select(r => new Row
        {
            Id = r.Id, At = r.AccessedAt, UserId = r.UserId, Action = r.Action,
            EntityType = r.EntityType, EntityId = r.EntityId, Success = true,
            IpAddress = r.IpAddress, UserAgent = r.UserAgent, DurationMs = null,
        });
    }

    private async Task<Dictionary<string, string>> CandidateLabelsAsync(List<Row> rows, CancellationToken ct)
    {
        var ids = rows
            .Where(r => r.EntityType == "Candidate")
            .Select(r => Guid.TryParse(r.EntityId, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (ids.Count == 0) return [];

        // Removed candidates keep their name here: the trail is most needed after a removal.
        var found = await _tenant.Candidates.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.FirstName, c.LastName })
            .ToListAsync(ct);

        return found.ToDictionary(
            c => c.Id.ToString(),
            c => $"{c.FirstName} {c.LastName}".Trim());
    }

    private static string FullName(string first, string last, string? email)
    {
        var name = $"{first} {last}".Trim();
        return name.Length > 0 ? name : email ?? "Unknown user";
    }
}

/// <summary>
/// Turns the request type the behaviors record ("UpdateCandidateCommand") into words an owner
/// reads ("Update candidate"). The audited reads get their own phrasing, since "Get candidate by
/// id query" says nothing about what the person actually did.
/// </summary>
public static partial class AuditActionNames
{
    private static readonly Dictionary<string, string> Known = new(StringComparer.Ordinal)
    {
        ["GetCandidateByIdQuery"] = "Opened candidate",
        ["GetCandidatesQuery"] = "Listed candidates",
        ["GetCandidateTimelineQuery"] = "Viewed candidate history",
        ["GetCandidateDocumentsQuery"] = "Viewed candidate documents",
        ["GetCandidateDocumentQuery"] = "Opened candidate document",
        ["GetCandidateMediaQuery"] = "Viewed candidate photo",
        ["DownloadCandidateDocumentsCommand"] = "Downloaded candidate documents",
        ["GenerateCVCommand"] = "Printed CV",
        ["GenerateBulkCvCommand"] = "Printed CVs in bulk",
        ["GenerateVisaFormCommand"] = "Printed visa form",
        ["GenerateBulkVisaFormsCommand"] = "Printed visa forms in bulk",
        ["GenerateContractCommand"] = "Printed contract",
    };

    public static string Describe(string action)
    {
        if (string.IsNullOrWhiteSpace(action)) return "Unknown action";
        if (Known.TryGetValue(action, out var known)) return known;

        var name = action;
        foreach (var suffix in new[] { "Command", "Query" })
            if (name.EndsWith(suffix, StringComparison.Ordinal) && name.Length > suffix.Length)
                name = name[..^suffix.Length];

        var words = WordBoundary().Split(name).Where(w => w.Length > 0).ToList();
        if (words.Count == 0) return action;

        // Acronyms stay as written (CV, LMIS); everything else after the first word is lowercase.
        return string.Join(' ', words.Select((w, i) =>
            i == 0 ? w : w.All(char.IsUpper) && w.Length > 1 ? w : w.ToLowerInvariant()));
    }

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex WordBoundary();
}
