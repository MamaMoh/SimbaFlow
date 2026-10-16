using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.API.Features.Embassy;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence.Seeds;

namespace SimbaFlow.API.Features.Tasks.Queries;

public record WorkQueueRowDto(
    /// <summary>Unique within its queue: the candidate, or the case or commission it is about.</summary>
    Guid Id,
    Guid CandidateId,
    string FullName,
    string PassportNumber,
    string? PartnerName,
    string? OfficeName,
    string NextStep,
    bool NeedsAction,
    int DaysInStage,
    bool Overdue,
    /// <summary>A problem outside this desk's step that still belongs at the top: an expired passport, an open case.</summary>
    string? Warning,
    string Href);

public record WorkQueueDto(
    string Key,
    string Label,
    string BoardHref,
    int NeedsActionCount,
    int WaitingCount,
    int OverdueCount,
    List<WorkQueueRowDto> Items);

public record WorkQueueFilterOption(string Value, string Label);

public record MyWorkQueuesResult(
    List<WorkQueueDto> Queues,
    List<WorkQueueFilterOption> Partners,
    List<WorkQueueFilterOption> Offices);

/// <summary>
/// My work: one tab per board the caller's role can open, each listing who is on it and what
/// they are waiting for.
///
/// An embassy officer sees Embassy; somebody who runs embassy and tickets switches between the
/// two. Which tabs a caller gets is the same permission that opens the board in the sidebar, so
/// the inbox never offers work on a board its owner could not open.
/// </summary>
public record GetMyWorkQueuesQuery(
    string? Search = null,
    string? Partner = null,
    Guid? OfficeId = null) : IRequest<Result<MyWorkQueuesResult>>, IRequireAnyPermission
{
    public IReadOnlyList<string> AcceptedPermissions => WorkQueues.AllPermissions;
}

/// <summary>The boards My work can show, in pipeline order, with the permission that opens each.</summary>
public static class WorkQueues
{
    public record Definition(string Key, string Label, string BoardHref, string[] Permissions, string? StageName);

    public static readonly Definition[] All =
    [
        new("intake", "Intake", "/candidates", ["candidate.update"], "Intake"),
        new("new-contracts", "New Contracts", "/workflow/new-contracts", ["workflow.view"], "New Contracts"),
        new("embassy", "Embassy", "/workflow/embassy", ["embassy.read"], WorkflowDefinitionUpgrader.EmbassyStageName),
        new("case-executive", "Case Executive", "/workflow/case-executive", ["embassy.case_view"], WorkflowDefinitionUpgrader.CaseExecutiveStageName),
        new("lmis", "LMIS", "/workflow/lmis", ["lmis.read"], WorkflowDefinitionUpgrader.LmisStageName),
        new("tickets", "Tickets", "/workflow/tickets", ["travel.read"], WorkflowDefinitionUpgrader.TicketStageName),
        new("departures", "Departures", "/workflow/departures", ["travel.read"], WorkflowDefinitionUpgrader.DepartureStageName),
        new("arrivals", "Arrivals", "/workflow/arrivals", ["arrival.read"], WorkflowDefinitionUpgrader.ArrivalStageName),
        new("exceptions", "Exceptions", "/workflow/exceptions", ["arrival.read", "arrival.exception"], null),
        new("commissions", "Commissions", "/workflow/commissions", ["commission.read"], null),
    ];

    public static readonly IReadOnlyList<string> AllPermissions =
        [.. All.SelectMany(d => d.Permissions).Distinct(), "system.admin"];

    public static IEnumerable<Definition> For(ICurrentUserService user) =>
        All.Where(d => user.HasPermission("system.admin") || d.Permissions.Any(user.HasPermission));
}

public class GetMyWorkQueuesHandler : IRequestHandler<GetMyWorkQueuesQuery, Result<MyWorkQueuesResult>>
{
    /// <summary>A board longer than this is shown in part; the counts on the tab stay whole.</summary>
    private const int MaxRowsPerQueue = 300;

    private readonly ITenantDbContext _context;
    private readonly ICurrentUserService _user;

    public GetMyWorkQueuesHandler(ITenantDbContext context, ICurrentUserService user)
    {
        _context = context;
        _user = user;
    }

    private sealed record Person(
        Guid Id,
        string FullName,
        string PassportNumber,
        string? LabourId,
        string? PartnerName,
        Guid? OfficeId,
        Guid? CurrentStageId,
        Guid[] VisibleInStages,
        Dictionary<string, string> Status,
        int DaysInStage,
        DateOnly? PassportExpiry);

    private sealed record Row(WorkQueueRowDto Dto, bool Urgent);

    public async Task<Result<MyWorkQueuesResult>> Handle(GetMyWorkQueuesQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var queues = WorkQueues.For(_user).ToList();

        var stages = await _context.WorkflowStages.AsNoTracking()
            .Where(s => !s.IsDeleted)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync(ct);
        Guid? StageId(string name) =>
            stages.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))?.Id;

        var offices = await _context.Offices.AsNoTracking()
            .Where(o => !o.IsDeleted)
            .Select(o => new { o.Id, o.Name })
            .ToListAsync(ct);
        var officeNames = offices.ToDictionary(o => o.Id, o => o.Name);

        var loaded = await _context.Candidates.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active)
            .Select(c => new
            {
                c.Id, c.FirstName, c.MiddleName, c.LastName, c.PassportNumber, c.LabourId,
                c.PartnerName, c.OfficeId, c.CurrentStageId, c.VisibleInStages,
                c.CurrentStatusValues, c.StageEnteredAt, c.RegisteredAt, c.PassportExpiryDate,
            })
            .ToListAsync(ct);

        var everyone = loaded.Select(c =>
        {
            var candidate = new Domain.Entities.Candidates.Candidate { CurrentStatusValues = c.CurrentStatusValues };
            return new Person(
                c.Id,
                string.IsNullOrEmpty(c.MiddleName) ? $"{c.FirstName} {c.LastName}" : $"{c.FirstName} {c.MiddleName} {c.LastName}",
                c.PassportNumber,
                c.LabourId,
                c.PartnerName,
                c.OfficeId,
                c.CurrentStageId,
                c.VisibleInStages ?? [],
                EmbassyLmisHelpers.ReadStatusValues(candidate),
                WorkQueueRules.DaysInStage(c.StageEnteredAt ?? c.RegisteredAt, now),
                c.PassportExpiryDate);
        }).ToList();

        // The dropdowns offer what is actually on the boards, before the filters narrow it, so
        // picking one partner does not empty the list of partners to switch to.
        var partnerOptions = everyone
            .Select(p => p.PartnerName?.Trim())
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .Select(n => new WorkQueueFilterOption(n!, n!))
            .ToList();
        var officeOptions = offices
            .Where(o => everyone.Any(p => p.OfficeId == o.Id))
            .OrderBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .Select(o => new WorkQueueFilterOption(o.Id.ToString(), o.Name))
            .ToList();

        var people = everyone.Where(p => Matches(p, request)).ToList();
        var byId = people.ToDictionary(p => p.Id);

        var openCases = await _context.ExceptionCases.AsNoTracking()
            .Where(e => !e.IsDeleted && (e.Status == ExceptionStatus.Open || e.Status == ExceptionStatus.UnderInvestigation))
            .Select(e => new { e.Id, e.CandidateId, e.Type, e.Status, e.OpenedAt })
            .ToListAsync(ct);
        var withOpenCase = openCases.Select(e => e.CandidateId).ToHashSet();

        string? Warning(Person p)
        {
            if (p.PassportExpiry is { } expiry)
            {
                var left = expiry.DayNumber - today.DayNumber;
                if (left < 0) return "Passport expired";
                if (left <= WorkQueueRules.ExpirySoonDays) return $"Passport expires in {PluralText.Count(left, "day")}";
            }
            return withOpenCase.Contains(p.Id) ? "Open exception case" : null;
        }

        Row CandidateRow(Person p, WorkQueueSteps.Step step, string href, int? days = null, Guid? rowId = null)
        {
            var inStage = days ?? p.DaysInStage;
            var overdue = inStage > WorkQueueRules.OverdueThresholdDays;
            var warning = Warning(p);
            return new Row(
                new WorkQueueRowDto(
                    rowId ?? p.Id, p.Id, p.FullName, p.PassportNumber, p.PartnerName,
                    p.OfficeId is { } o && officeNames.TryGetValue(o, out var name) ? name : null,
                    step.Text, step.NeedsAction, inStage, overdue, warning, href),
                overdue || warning is not null);
        }

        string Board(WorkQueues.Definition q, Person p) =>
            $"{q.BoardHref}?q={Uri.EscapeDataString(p.PassportNumber)}";

        var result = new List<WorkQueueDto>();
        foreach (var q in queues)
        {
            List<Row> rows;
            switch (q.Key)
            {
                case "exceptions":
                    rows = openCases
                        .Where(e => byId.ContainsKey(e.CandidateId))
                        .Select(e => CandidateRow(
                            byId[e.CandidateId],
                            e.Status == ExceptionStatus.Open
                                ? new WorkQueueSteps.Step($"{e.Type}: start the investigation", true)
                                : new WorkQueueSteps.Step($"{e.Type}: resolve the case", true),
                            $"/workflow/exceptions/{e.Id}",
                            WorkQueueRules.DaysInStage(e.OpenedAt, now),
                            e.Id))
                        .ToList();
                    break;

                case "commissions":
                    rows = await CommissionRowsAsync(byId, now, CandidateRow, ct);
                    break;

                default:
                    var stageId = StageId(q.StageName!);
                    if (stageId is null)
                        continue;
                    // Intake and New Contracts are worked only where the candidate stands; the
                    // later boards also show the mirrors, the same as the boards do.
                    var primaryOnly = q.Key is "intake" or "new-contracts";
                    rows = people
                        .Where(p => p.CurrentStageId == stageId || (!primaryOnly && p.VisibleInStages.Contains(stageId.Value)))
                        .Where(p => q.Key != "arrivals" || !WorkQueueSteps.ArrivalDone(p.Status))
                        .Select(p => CandidateRow(p, Step(q.Key, p.Status, today),
                            q.Key == "intake" ? $"/candidates/{p.Id}" : Board(q, p)))
                        .ToList();
                    break;
            }

            var ordered = rows
                .OrderByDescending(r => r.Dto.NeedsAction)
                .ThenByDescending(r => r.Urgent)
                .ThenByDescending(r => r.Dto.DaysInStage)
                .ToList();

            result.Add(new WorkQueueDto(
                q.Key,
                q.Label,
                q.BoardHref,
                ordered.Count(r => r.Dto.NeedsAction),
                ordered.Count(r => !r.Dto.NeedsAction),
                ordered.Count(r => r.Dto.Overdue),
                ordered.Take(MaxRowsPerQueue).Select(r => r.Dto).ToList()));
        }

        return Result<MyWorkQueuesResult>.Success(new MyWorkQueuesResult(result, partnerOptions, officeOptions));
    }

    private static WorkQueueSteps.Step Step(string key, Dictionary<string, string> status, DateOnly today) => key switch
    {
        "intake" => WorkQueueSteps.Intake(),
        "new-contracts" => WorkQueueSteps.NewContracts(status),
        "embassy" => WorkQueueSteps.Embassy(status, today),
        "case-executive" => WorkQueueSteps.CaseExecutive(status, today),
        "lmis" => WorkQueueSteps.Lmis(status),
        "tickets" => WorkQueueSteps.Ticket(status),
        "departures" => WorkQueueSteps.Departure(status, today),
        "arrivals" => WorkQueueSteps.Arrival(status),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, null),
    };

    /// <summary>
    /// Commissions still owed. A disputed one or one with no fees on it needs the finance desk;
    /// one with a balance is waiting on the partner to pay.
    /// </summary>
    private async Task<List<Row>> CommissionRowsAsync(
        Dictionary<Guid, Person> byId,
        DateTime now,
        Func<Person, WorkQueueSteps.Step, string, int?, Guid?, Row> row,
        CancellationToken ct)
    {
        var open = await _context.Commissions.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status != CommissionStatus.Settled)
            .Select(c => new { c.Id, c.CandidateId, c.Status, c.OpenedAt, c.TotalFeesAmount, c.BalanceAmount })
            .ToListAsync(ct);

        return open
            .Where(c => byId.ContainsKey(c.CandidateId))
            .Select(c =>
            {
                var step = c.Status == CommissionStatus.Disputed
                    ? new WorkQueueSteps.Step("Resolve the dispute", true)
                    : c.TotalFeesAmount <= 0
                        ? new WorkQueueSteps.Step("Add the fees", true)
                        : c.BalanceAmount > 0
                            ? new WorkQueueSteps.Step(
                                $"Waiting for {c.BalanceAmount.ToString("N0", CultureInfo.InvariantCulture)} ETB", false)
                            : new WorkQueueSteps.Step("Paid in full: settle", true);
                return row(byId[c.CandidateId], step, $"/workflow/commissions/{c.Id}",
                    WorkQueueRules.DaysInStage(c.OpenedAt, now), c.Id);
            })
            .ToList();
    }

    private static bool Matches(Person p, GetMyWorkQueuesQuery request)
    {
        if (request.OfficeId is { } office && p.OfficeId != office)
            return false;
        if (!string.IsNullOrWhiteSpace(request.Partner) &&
            !string.Equals(p.PartnerName?.Trim(), request.Partner.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrWhiteSpace(request.Search))
            return true;

        var needle = request.Search.Trim();
        return p.FullName.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || p.PassportNumber.Contains(needle, StringComparison.OrdinalIgnoreCase)
            || (p.LabourId?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
