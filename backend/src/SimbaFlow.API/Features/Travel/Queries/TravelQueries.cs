using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Travel.Queries;

public record TravelBoardRowDto(
    Guid Id,
    string FullName,
    string PassportNumber,
    string? LabourId,
    string? PartnerName,
    string? CountryOfTravel,
    Dictionary<string, string> StatusValues,
    int DaysInStage,
    int? RemainingDays,
    bool IsCanceled,
    DateTime RegisteredAt);

public record TravelBoardResult(
    List<TravelBoardRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record GetTicketBoardQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null) : IRequest<Result<TravelBoardResult>>, IRequirePermission
{
    public string RequiredPermission => "travel.read";
}

public class GetTicketBoardHandler : IRequestHandler<GetTicketBoardQuery, Result<TravelBoardResult>>
{
    private readonly ITenantDbContext _context;

    public GetTicketBoardHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<TravelBoardResult>> Handle(GetTicketBoardQuery request, CancellationToken ct)
    {
        var stage = await TravelArrivalHelpers.FindStageByNameAsync(
            _context, TravelArrivalHelpers.TicketStageName, ct);
        if (stage is null)
            return Result<TravelBoardResult>.Failure("Ticket stage not configured", 500);

        return await BoardQueryAsync(
            stage.Id, request.Page, request.PageSize, request.Search,
            includeCanceled: true, sortByRemainingDays: false, ct);
    }

    internal async Task<Result<TravelBoardResult>> BoardQueryAsync(
        Guid stageId,
        int page,
        int pageSize,
        string? search,
        bool includeCanceled,
        bool sortByRemainingDays,
        CancellationToken ct)
    {
        var query = _context.Candidates
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c =>
                EF.Functions.ILike(c.FirstName, $"%{search}%") ||
                EF.Functions.ILike(c.LastName, $"%{search}%") ||
                EF.Functions.ILike(c.PassportNumber, $"%{search}%") ||
                (c.LabourId != null && EF.Functions.ILike(c.LabourId, $"%{search}%")));
        }

        var candidates = await query.ToListAsync(ct);

        var matches = candidates
            .Where(c => TravelArrivalHelpers.IsVisibleInStage(c, stageId))
            .Select(c =>
            {
                var status = TravelArrivalHelpers.ReadStatusValues(c);
                return new
                {
                    Candidate = c,
                    Status = status,
                    Canceled = TravelArrivalHelpers.IsCanceled(status),
                    Remaining = TravelArrivalHelpers.RemainingDays(status)
                };
            })
            .Where(x => includeCanceled || !x.Canceled)
            .ToList();

        matches = sortByRemainingDays
            ? matches.OrderBy(x => x.Remaining ?? int.MaxValue).ThenBy(x => x.Candidate.FullName).ToList()
            : matches.OrderByDescending(x => x.Candidate.StageEnteredAt ?? x.Candidate.RegisteredAt).ToList();

        var total = matches.Count;
        var rows = matches.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var items = rows.Select(x => new TravelBoardRowDto(
            x.Candidate.Id,
            x.Candidate.FullName,
            x.Candidate.PassportNumber,
            x.Candidate.LabourId,
            x.Candidate.PartnerName,
            TravelArrivalHelpers.TrackValue(x.Status, "destination") ?? x.Candidate.CountryOfTravel,
            x.Status,
            TravelArrivalHelpers.DaysInStage(x.Candidate),
            x.Remaining,
            x.Canceled,
            x.Candidate.RegisteredAt)).ToList();

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return Result<TravelBoardResult>.Success(
            new TravelBoardResult(items, total, page, pageSize, totalPages));
    }
}

public record GetDepartureBoardQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool IncludeCanceled = false) : IRequest<Result<TravelBoardResult>>, IRequirePermission
{
    public string RequiredPermission => "travel.read";
}

public class GetDepartureBoardHandler : IRequestHandler<GetDepartureBoardQuery, Result<TravelBoardResult>>
{
    private readonly ITenantDbContext _context;
    private readonly GetTicketBoardHandler _board;

    public GetDepartureBoardHandler(ITenantDbContext context)
    {
        _context = context;
        _board = new GetTicketBoardHandler(context);
    }

    public async Task<Result<TravelBoardResult>> Handle(GetDepartureBoardQuery request, CancellationToken ct)
    {
        var stage = await TravelArrivalHelpers.FindStageByNameAsync(
            _context, TravelArrivalHelpers.DepartureStageName, ct);
        if (stage is null)
            return Result<TravelBoardResult>.Failure("Departure stage not configured", 500);

        return await _board.BoardQueryAsync(
            stage.Id, request.Page, request.PageSize, request.Search,
            request.IncludeCanceled, sortByRemainingDays: true, ct);
    }
}
