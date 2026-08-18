using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Reports.Queries;

/// <summary>Static catalog of available reports (drives the Reports picker).</summary>
public static class ReportCatalog
{
    public const int OverdueThresholdDays = 14;

    public static readonly IReadOnlyList<ReportCatalogItem> Items =
    [
        new("pipeline", "Pipeline summary", "Operations",
            "Active candidates per workflow stage with share of pipeline."),
        new("agency-performance", "Agency performance", "Operations",
            "Average time candidates have spent in each stage."),
        new("office-comparison", "Office comparison", "Operations",
            "Candidates and outstanding commission by registering office."),
        new("overdue", "Overdue / stuck candidates", "Operations",
            $"Candidates that have been in their stage for more than {OverdueThresholdDays} days."),
        new("financial-summary", "Financial summary", "Finance",
            "Commission fees, collections and outstanding balance."),
    ];

    public static bool Exists(string key) => Items.Any(i => i.Key == key);
}

public record GetReportCatalogQuery : IRequest<Result<List<ReportCatalogItem>>>, IRequirePermission
{
    public string RequiredPermission => "report.view";
}

public class GetReportCatalogHandler
    : IRequestHandler<GetReportCatalogQuery, Result<List<ReportCatalogItem>>>
{
    public Task<Result<List<ReportCatalogItem>>> Handle(GetReportCatalogQuery request, CancellationToken ct)
        => Task.FromResult(Result<List<ReportCatalogItem>>.Success(ReportCatalog.Items.ToList()));
}

public record ReportFile(byte[] Bytes, string ContentType, string FileName);

public record ExportReportQuery(string Key, string Format)
    : IRequest<Result<ReportFile>>, IRequirePermission
{
    public string RequiredPermission => "report.export";
}

public class ExportReportHandler : IRequestHandler<ExportReportQuery, Result<ReportFile>>
{
    private readonly ISender _sender;
    private readonly IReportExportService _export;

    public ExportReportHandler(ISender sender, IReportExportService export)
    {
        _sender = sender;
        _export = export;
    }

    public async Task<Result<ReportFile>> Handle(ExportReportQuery request, CancellationToken ct)
    {
        var format = (request.Format ?? "excel").ToLowerInvariant();
        if (format is not ("excel" or "pdf"))
            return Result<ReportFile>.Failure("Format must be 'excel' or 'pdf'.", 400);

        var built = await _sender.Send(new GetReportQuery(request.Key), ct);
        if (!built.IsSuccess || built.Data is null)
            return Result<ReportFile>.Failure(built.Error ?? "Report not found.", built.StatusCode);

        var report = built.Data;
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd");

        return format == "pdf"
            ? Result<ReportFile>.Success(new ReportFile(
                _export.ToPdf(report), "application/pdf", $"{request.Key}_{stamp}.pdf"))
            : Result<ReportFile>.Success(new ReportFile(
                _export.ToExcel(report),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"{request.Key}_{stamp}.xlsx"));
    }
}

public record GetReportQuery(string Key) : IRequest<Result<ReportTable>>, IRequirePermission
{
    public string RequiredPermission => "report.view";
}

public class GetReportHandler : IRequestHandler<GetReportQuery, Result<ReportTable>>
{
    private readonly ITenantDbContext _context;

    public GetReportHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<ReportTable>> Handle(GetReportQuery request, CancellationToken ct)
    {
        if (!ReportCatalog.Exists(request.Key))
            return Result<ReportTable>.Failure($"Unknown report '{request.Key}'.", 404);

        var table = request.Key switch
        {
            "pipeline" => await BuildPipelineAsync(ct),
            "agency-performance" => await BuildPerformanceAsync(ct),
            "office-comparison" => await BuildOfficeComparisonAsync(ct),
            "overdue" => await BuildOverdueAsync(ct),
            "financial-summary" => await BuildFinancialSummaryAsync(ct),
            _ => null
        };

        return table is null
            ? Result<ReportTable>.Failure($"Unknown report '{request.Key}'.", 404)
            : Result<ReportTable>.Success(table);
    }

    private async Task<List<(Guid Id, string Name, int SortOrder)>> LoadStagesAsync(CancellationToken ct)
    {
        var definition = await _context.WorkflowDefinitions
            .AsNoTracking()
            .Include(w => w.Stages.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(w => w.IsActive && !w.IsDeleted, ct);

        return definition?.Stages
            .OrderBy(s => s.SortOrder)
            .Select(s => (s.Id, s.Name, s.SortOrder))
            .ToList() ?? [];
    }

    private async Task<ReportTable> BuildPipelineAsync(CancellationToken ct)
    {
        var stages = await LoadStagesAsync(ct);

        var counts = await _context.Candidates.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active)
            .GroupBy(c => c.CurrentStageId)
            .Select(g => new { StageId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byStage = counts.Where(c => c.StageId != null)
            .ToDictionary(c => c.StageId!.Value, c => c.Count);
        var unassigned = counts.Where(c => c.StageId == null).Sum(c => c.Count);
        var total = byStage.Values.Sum() + unassigned;

        var rows = new List<Dictionary<string, object?>>();
        foreach (var s in stages)
        {
            var count = byStage.GetValueOrDefault(s.Id, 0);
            rows.Add(new()
            {
                ["stage"] = s.Name,
                ["candidates"] = count,
                ["share"] = total == 0 ? 0d : Math.Round(count * 100d / total, 1)
            });
        }
        if (unassigned > 0)
            rows.Add(new() { ["stage"] = "Unassigned", ["candidates"] = unassigned, ["share"] = total == 0 ? 0d : Math.Round(unassigned * 100d / total, 1) });

        return new ReportTable(
            "pipeline", "Pipeline summary",
            $"{total} active candidate(s) across {stages.Count} stage(s).",
            [
                new("stage", "Stage"),
                new("candidates", "Candidates", ReportColumnType.Number),
                new("share", "Share", ReportColumnType.Percent),
            ],
            rows, "stage", "candidates", DateTime.UtcNow);
    }

    private async Task<ReportTable> BuildPerformanceAsync(CancellationToken ct)
    {
        var stages = await LoadStagesAsync(ct);
        var now = DateTime.UtcNow;

        var candidates = await _context.Candidates.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active && c.CurrentStageId != null)
            .Select(c => new { c.CurrentStageId, c.StageEnteredAt })
            .ToListAsync(ct);

        var grouped = candidates
            .GroupBy(c => c.CurrentStageId!.Value)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Count = g.Count(),
                    AvgDays = g.Where(x => x.StageEnteredAt != null)
                        .Select(x => (now - x.StageEnteredAt!.Value).TotalDays)
                        .DefaultIfEmpty(0)
                        .Average()
                });

        var rows = stages.Select(s =>
        {
            var stat = grouped.GetValueOrDefault(s.Id);
            return new Dictionary<string, object?>
            {
                ["stage"] = s.Name,
                ["candidates"] = stat?.Count ?? 0,
                ["avgDays"] = stat is null ? 0d : Math.Round(stat.AvgDays, 1)
            };
        }).ToList();

        return new ReportTable(
            "agency-performance", "Agency performance",
            "Average days candidates have spent in their current stage.",
            [
                new("stage", "Stage"),
                new("candidates", "Candidates", ReportColumnType.Number),
                new("avgDays", "Avg days in stage", ReportColumnType.Number),
            ],
            rows, "stage", "avgDays", DateTime.UtcNow);
    }

    private async Task<ReportTable> BuildOfficeComparisonAsync(CancellationToken ct)
    {
        var candidateByOffice = await _context.Candidates.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active)
            .GroupBy(c => c.PartnerName ?? "—")
            .Select(g => new { Office = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var commissionByOffice = await _context.Commissions.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .GroupBy(c => c.PartnerName ?? "—")
            .Select(g => new { Office = g.Key, Owed = g.Sum(x => x.BalanceAmount) })
            .ToListAsync(ct);

        var owedMap = commissionByOffice.ToDictionary(x => x.Office, x => x.Owed);

        var rows = candidateByOffice
            .OrderByDescending(x => x.Count)
            .Select(x => new Dictionary<string, object?>
            {
                ["office"] = x.Office,
                ["candidates"] = x.Count,
                ["owed"] = owedMap.GetValueOrDefault(x.Office, 0m)
            }).ToList();

        return new ReportTable(
            "office-comparison", "Office comparison",
            "Active candidates and outstanding commission by registering office.",
            [
                new("office", "Office"),
                new("candidates", "Candidates", ReportColumnType.Number),
                new("owed", "Commission owed (ETB)", ReportColumnType.Money),
            ],
            rows, "office", "candidates", DateTime.UtcNow);
    }

    private async Task<ReportTable> BuildOverdueAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-ReportCatalog.OverdueThresholdDays);
        var now = DateTime.UtcNow;

        var stuck = await _context.Candidates.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active
                && c.StageEnteredAt != null && c.StageEnteredAt < cutoff)
            .OrderBy(c => c.StageEnteredAt)
            .Select(c => new
            {
                c.FirstName, c.LastName, c.PassportNumber,
                c.CurrentStageName, c.PartnerName, c.StageEnteredAt
            })
            .Take(500)
            .ToListAsync(ct);

        var rows = stuck.Select(c => new Dictionary<string, object?>
        {
            ["candidate"] = $"{c.FirstName} {c.LastName}".Trim(),
            ["passport"] = c.PassportNumber,
            ["stage"] = c.CurrentStageName ?? "—",
            ["days"] = (int)Math.Floor((now - c.StageEnteredAt!.Value).TotalDays),
            ["office"] = c.PartnerName ?? "—"
        }).ToList();

        return new ReportTable(
            "overdue", "Overdue / stuck candidates",
            $"{rows.Count} candidate(s) in the same stage for more than {ReportCatalog.OverdueThresholdDays} days.",
            [
                new("candidate", "Candidate"),
                new("passport", "Passport"),
                new("stage", "Stage"),
                new("days", "Days in stage", ReportColumnType.Number),
                new("office", "Office"),
            ],
            rows, null, null, DateTime.UtcNow);
    }

    private async Task<ReportTable> BuildFinancialSummaryAsync(CancellationToken ct)
    {
        var totals = await _context.Commissions.AsNoTracking()
            .Where(c => !c.IsDeleted)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Fees = g.Sum(x => x.TotalFeesAmount),
                Paid = g.Sum(x => x.TotalPaidAmount),
                Balance = g.Sum(x => x.BalanceAmount),
                Open = g.Count(x => x.Status == CommissionStatus.Open)
            })
            .FirstOrDefaultAsync(ct);

        var fees = totals?.Fees ?? 0m;
        var paid = totals?.Paid ?? 0m;
        var balance = totals?.Balance ?? 0m;

        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["metric"] = "Total fees", ["amount"] = fees },
            new() { ["metric"] = "Total collected", ["amount"] = paid },
            new() { ["metric"] = "Outstanding balance", ["amount"] = balance },
        };

        return new ReportTable(
            "financial-summary", "Financial summary",
            $"{totals?.Open ?? 0} open commission record(s).",
            [
                new("metric", "Metric"),
                new("amount", "Amount (ETB)", ReportColumnType.Money),
            ],
            rows, "metric", "amount", DateTime.UtcNow);
    }
}
