using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.API.Features.Travel;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Arrival.Queries;

public record ArrivalBoardRowDto(
    Guid Id,
    string FullName,
    string PassportNumber,
    string? LabourId,
    string? PartnerName,
    string? CountryOfTravel,
    Dictionary<string, string> StatusValues,
    int DaysInStage,
    int DaysSinceRegistered,
    bool CommissionLinked,
    bool HasOpenException,
    DateTime RegisteredAt);

public record ArrivalBoardResult(
    List<ArrivalBoardRowDto> Items,
    /// <summary>The stage this board represents — the UI filters actions to it.</summary>
    Guid StageId,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record GetArrivalBoardQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null) : IRequest<Result<ArrivalBoardResult>>, IRequirePermission
{
    public string RequiredPermission => "arrival.read";
}

public class GetArrivalBoardHandler : IRequestHandler<GetArrivalBoardQuery, Result<ArrivalBoardResult>>
{
    private readonly ITenantDbContext _context;

    public GetArrivalBoardHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<ArrivalBoardResult>> Handle(GetArrivalBoardQuery request, CancellationToken ct)
    {
        var stage = await TravelArrivalHelpers.FindStageByNameAsync(
            _context, TravelArrivalHelpers.ArrivalStageName, ct);
        if (stage is null)
            return Result<ArrivalBoardResult>.Failure("Arrival stage not configured", 500);

        var query = _context.Candidates
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search;
            query = query.Where(c =>
                EF.Functions.ILike(c.FirstName, $"%{search}%") ||
                EF.Functions.ILike(c.LastName, $"%{search}%") ||
                EF.Functions.ILike(c.PassportNumber, $"%{search}%") ||
                (c.LabourId != null && EF.Functions.ILike(c.LabourId, $"%{search}%")));
        }

        var candidates = await query
            .OrderByDescending(c => c.StageEnteredAt ?? c.RegisteredAt)
            .ToListAsync(ct);

        var matches = candidates
            .Where(c => TravelArrivalHelpers.IsVisibleInStage(c, stage.Id))
            .ToList();

        var total = matches.Count;
        var rows = matches.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        var ids = rows.Select(c => c.Id).ToList();

        var openExceptionIds = await _context.ExceptionCases
            .AsNoTracking()
            .Where(e => ids.Contains(e.CandidateId) && !e.IsDeleted && e.Status == ExceptionStatus.Open)
            .Select(e => e.CandidateId)
            .ToListAsync(ct);
        var openSet = openExceptionIds.ToHashSet();

        var items = rows.Select(c =>
        {
            var status = TravelArrivalHelpers.ReadStatusValues(c);
            var linked = string.Equals(
                TravelArrivalHelpers.TrackValue(status, "commission_linked"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            return new ArrivalBoardRowDto(
                c.Id,
                c.FullName,
                c.PassportNumber,
                c.LabourId,
                c.PartnerName,
                TravelArrivalHelpers.TrackValue(status, "destination") ?? c.CountryOfTravel,
                status,
                TravelArrivalHelpers.DaysInStage(c),
                TravelArrivalHelpers.DaysSinceRegistered(c),
                linked,
                openSet.Contains(c.Id),
                c.RegisteredAt);
        }).ToList();

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);
        return Result<ArrivalBoardResult>.Success(
            new ArrivalBoardResult(items, stage.Id, total, request.Page, request.PageSize, totalPages));
    }
}
