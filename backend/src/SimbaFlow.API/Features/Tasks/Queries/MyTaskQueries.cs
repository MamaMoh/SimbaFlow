using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Tasks.Queries;

public record MyTaskItemDto(
    string Type,
    string Title,
    string Subtitle,
    string Severity,
    Guid? CandidateId,
    string? Href);

public record MyTasksResult(
    int OverdueCount,
    int ExpiringSoonCount,
    int OpenExceptionCount,
    List<MyTaskItemDto> Items);

public record GetMyTasksQuery : IRequest<Result<MyTasksResult>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public class GetMyTasksHandler : IRequestHandler<GetMyTasksQuery, Result<MyTasksResult>>
{
    private const int OverdueThresholdDays = 14;
    private const int ExpirySoonDays = 30;
    private const int PerBucket = 15;

    private readonly ITenantDbContext _context;

    public GetMyTasksHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<MyTasksResult>> Handle(GetMyTasksQuery request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var overdueCutoff = now.AddDays(-OverdueThresholdDays);
        var today = DateOnly.FromDateTime(now);
        var soon = today.AddDays(ExpirySoonDays);

        var active = _context.Candidates.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active);

        // Overdue / stuck candidates
        var overdue = await active
            .Where(c => c.StageEnteredAt != null && c.StageEnteredAt < overdueCutoff)
            .OrderBy(c => c.StageEnteredAt)
            .Select(c => new { c.Id, c.FirstName, c.LastName, c.CurrentStageName, c.StageEnteredAt })
            .Take(PerBucket)
            .ToListAsync(ct);

        // Passports expiring soon (or already expired)
        var expiring = await active
            .Where(c => c.PassportExpiryDate != null && c.PassportExpiryDate <= soon)
            .OrderBy(c => c.PassportExpiryDate)
            .Select(c => new { c.Id, c.FirstName, c.LastName, c.PassportNumber, c.PassportExpiryDate })
            .Take(PerBucket)
            .ToListAsync(ct);

        // Open exception cases
        var exceptions = await _context.ExceptionCases.AsNoTracking()
            .Where(e => !e.IsDeleted && e.Status == ExceptionStatus.Open)
            .OrderBy(e => e.OpenedAt)
            .Select(e => new { e.CandidateId, e.Type, e.OpenedAt })
            .Take(PerBucket)
            .ToListAsync(ct);

        var overdueCount = await active
            .CountAsync(c => c.StageEnteredAt != null && c.StageEnteredAt < overdueCutoff, ct);
        var expiringCount = await active
            .CountAsync(c => c.PassportExpiryDate != null && c.PassportExpiryDate <= soon, ct);
        var exceptionCount = await _context.ExceptionCases.AsNoTracking()
            .CountAsync(e => !e.IsDeleted && e.Status == ExceptionStatus.Open, ct);

        var items = new List<MyTaskItemDto>();

        foreach (var e in exceptions)
        {
            items.Add(new MyTaskItemDto(
                "exception",
                $"{e.Type} case open",
                $"Opened {(int)Math.Floor((now - e.OpenedAt).TotalDays)} day(s) ago",
                "high",
                e.CandidateId,
                $"/candidates/{e.CandidateId}"));
        }

        foreach (var c in expiring)
        {
            var days = c.PassportExpiryDate!.Value.DayNumber - today.DayNumber;
            items.Add(new MyTaskItemDto(
                "passport",
                $"{c.FirstName} {c.LastName}".Trim(),
                days < 0 ? $"Passport expired {-days} day(s) ago" : $"Passport expires in {days} day(s)",
                days < 0 ? "high" : "medium",
                c.Id,
                $"/candidates/{c.Id}"));
        }

        foreach (var c in overdue)
        {
            var days = c.StageEnteredAt is null ? 0 : (int)Math.Floor((now - c.StageEnteredAt.Value).TotalDays);
            items.Add(new MyTaskItemDto(
                "overdue",
                $"{c.FirstName} {c.LastName}".Trim(),
                $"{days} day(s) in {c.CurrentStageName ?? "current stage"}",
                "medium",
                c.Id,
                $"/candidates/{c.Id}"));
        }

        return Result<MyTasksResult>.Success(new MyTasksResult(
            overdueCount, expiringCount, exceptionCount, items));
    }
}
