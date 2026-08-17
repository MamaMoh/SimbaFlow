using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Dashboard.Queries;

public record PipelineFunnelStageDto(
    Guid StageId,
    string StageName,
    int SortOrder,
    bool IsFinalStage,
    int Count);

public record PipelineFunnelResult(
    List<PipelineFunnelStageDto> Stages,
    int TotalCandidates,
    int UnassignedCount);

public record GetPipelineFunnelQuery
    : IRequest<Result<PipelineFunnelResult>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public class GetPipelineFunnelHandler
    : IRequestHandler<GetPipelineFunnelQuery, Result<PipelineFunnelResult>>
{
    private readonly ITenantDbContext _context;

    public GetPipelineFunnelHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<PipelineFunnelResult>> Handle(
        GetPipelineFunnelQuery request, CancellationToken ct)
    {
        var definition = await _context.WorkflowDefinitions
            .AsNoTracking()
            .Include(w => w.Stages.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(w => w.IsActive && !w.IsDeleted, ct);

        if (definition is null)
            return Result<PipelineFunnelResult>.Failure("No active workflow definition found.", 404);

        var stages = definition.Stages
            .OrderBy(s => s.SortOrder)
            .ToList();

        var counts = await _context.Candidates
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active)
            .GroupBy(c => c.CurrentStageId)
            .Select(g => new { StageId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var countByStage = counts
            .Where(c => c.StageId is not null)
            .ToDictionary(c => c.StageId!.Value, c => c.Count);

        var unassigned = counts
            .Where(c => c.StageId is null)
            .Sum(c => c.Count);

        var stageDtos = stages.Select(s => new PipelineFunnelStageDto(
            s.Id,
            s.Name,
            s.SortOrder,
            s.IsFinalStage,
            countByStage.GetValueOrDefault(s.Id, 0)
        )).ToList();

        var total = stageDtos.Sum(s => s.Count) + unassigned;

        return Result<PipelineFunnelResult>.Success(new PipelineFunnelResult(
            stageDtos,
            total,
            unassigned));
    }
}

// ─────────────────────────────────────────────────────────────────────────
// Command-center metrics (KPI tiles)
// ─────────────────────────────────────────────────────────────────────────

public record DashboardMetricsResult(
    int ActiveCandidates,
    int NewThisMonth,
    decimal CommissionsOwed,
    int OpenExceptions,
    int OverdueCandidates);

public record GetDashboardMetricsQuery
    : IRequest<Result<DashboardMetricsResult>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public class GetDashboardMetricsHandler
    : IRequestHandler<GetDashboardMetricsQuery, Result<DashboardMetricsResult>>
{
    private const int OverdueThresholdDays = 14;
    private readonly ITenantDbContext _context;

    public GetDashboardMetricsHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<DashboardMetricsResult>> Handle(
        GetDashboardMetricsQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var overdueCutoff = now.AddDays(-OverdueThresholdDays);

        var activeQuery = _context.Candidates.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active);

        var active = await activeQuery.CountAsync(ct);
        var newThisMonth = await activeQuery.CountAsync(c => c.RegisteredAt >= monthStart, ct);
        var overdue = await activeQuery.CountAsync(
            c => c.StageEnteredAt != null && c.StageEnteredAt < overdueCutoff, ct);

        var owed = await _context.Commissions.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .SumAsync(c => (decimal?)c.BalanceAmount, ct) ?? 0m;

        var openExceptions = await _context.ExceptionCases.AsNoTracking()
            .CountAsync(e => !e.IsDeleted && e.Status == ExceptionStatus.Open, ct);

        return Result<DashboardMetricsResult>.Success(new DashboardMetricsResult(
            active, newThisMonth, owed, openExceptions, overdue));
    }
}

// ─────────────────────────────────────────────────────────────────────────
// Monthly trends (intake & outcomes over the last 12 months)
// ─────────────────────────────────────────────────────────────────────────

public record TrendPointDto(string Month, int Registered, int Commissions, int Exceptions);

public record GetDashboardTrendsQuery
    : IRequest<Result<List<TrendPointDto>>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public class GetDashboardTrendsHandler
    : IRequestHandler<GetDashboardTrendsQuery, Result<List<TrendPointDto>>>
{
    private readonly ITenantDbContext _context;

    public GetDashboardTrendsHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<List<TrendPointDto>>> Handle(
        GetDashboardTrendsQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-11);

        var registered = await _context.Candidates.AsNoTracking()
            .Where(c => !c.IsDeleted && c.RegisteredAt >= start)
            .Select(c => c.RegisteredAt)
            .ToListAsync(ct);

        var commissions = await _context.Commissions.AsNoTracking()
            .Where(c => !c.IsDeleted && c.OpenedAt >= start)
            .Select(c => c.OpenedAt)
            .ToListAsync(ct);

        var exceptions = await _context.ExceptionCases.AsNoTracking()
            .Where(e => !e.IsDeleted && e.OpenedAt >= start)
            .Select(e => e.OpenedAt)
            .ToListAsync(ct);

        static string Bucket(DateTime dt) => $"{dt.Year:0000}-{dt.Month:00}";
        var regByMonth = registered.GroupBy(Bucket).ToDictionary(g => g.Key, g => g.Count());
        var comByMonth = commissions.GroupBy(Bucket).ToDictionary(g => g.Key, g => g.Count());
        var excByMonth = exceptions.GroupBy(Bucket).ToDictionary(g => g.Key, g => g.Count());

        var points = new List<TrendPointDto>();
        for (var i = 0; i < 12; i++)
        {
            var m = start.AddMonths(i);
            var key = $"{m.Year:0000}-{m.Month:00}";
            points.Add(new TrendPointDto(
                m.ToString("MMM yyyy"),
                regByMonth.GetValueOrDefault(key, 0),
                comByMonth.GetValueOrDefault(key, 0),
                excByMonth.GetValueOrDefault(key, 0)));
        }

        return Result<List<TrendPointDto>>.Success(points);
    }
}
