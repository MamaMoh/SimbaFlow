using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Embassy.Queries;

public record EmbassyBoardRowDto(
    Guid Id,
    string FullName,
    string PassportNumber,
    string? LabourId,
    string? OfficeName,
    string? CountryOfTravel,
    Dictionary<string, string> StatusValues,
    int DaysInStage,
    bool IsMirror,
    DateTime RegisteredAt);

public record EmbassyBoardResult(
    List<EmbassyBoardRowDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record GetEmbassyBoardQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? OfficeId = null) : IRequest<Result<EmbassyBoardResult>>, IRequirePermission
{
    public string RequiredPermission => "embassy.read";
}

public class GetEmbassyBoardHandler : IRequestHandler<GetEmbassyBoardQuery, Result<EmbassyBoardResult>>
{
    private readonly ITenantDbContext _context;

    public GetEmbassyBoardHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<EmbassyBoardResult>> Handle(
        GetEmbassyBoardQuery request, CancellationToken ct)
    {
        var embassy = await EmbassyLmisHelpers.FindStageByNameAsync(
            _context, EmbassyLmisHelpers.EmbassyStageName, ct);
        if (embassy is null)
            return Result<EmbassyBoardResult>.Failure("Embassy stage not configured", 500);

        return await BoardQueryAsync(
            embassy.Id,
            request.Page,
            request.PageSize,
            request.Search,
            request.OfficeId,
            primaryOnly: false,
            ct);
    }

    internal async Task<Result<EmbassyBoardResult>> BoardQueryAsync(
        Guid stageId,
        int page,
        int pageSize,
        string? search,
        Guid? officeId,
        bool primaryOnly,
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

        if (officeId.HasValue)
            query = query.Where(c => c.OfficeId == officeId);

        var candidates = await query
            .OrderByDescending(c => c.StageEnteredAt ?? c.RegisteredAt)
            .ToListAsync(ct);

        var matches = candidates.Where(c =>
        {
            if (primaryOnly)
                return c.CurrentStageId == stageId;
            return EmbassyLmisHelpers.IsVisibleInStage(c, stageId);
        }).ToList();

        var total = matches.Count;
        var rows = matches.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var items = rows.Select(c =>
        {
            var status = EmbassyLmisHelpers.ReadStatusValues(c);
            var isMirror = c.CurrentStageId != stageId && c.VisibleInStages.Contains(stageId);
            return new EmbassyBoardRowDto(
                c.Id,
                c.FullName,
                c.PassportNumber,
                c.LabourId,
                c.OfficeName,
                c.CountryOfTravel,
                status,
                EmbassyLmisHelpers.DaysInStage(c),
                isMirror,
                c.RegisteredAt);
        }).ToList();

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return Result<EmbassyBoardResult>.Success(
            new EmbassyBoardResult(items, total, page, pageSize, totalPages));
    }
}

public record GetCaseExecutiveBoardQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? OfficeId = null) : IRequest<Result<EmbassyBoardResult>>, IRequirePermission
{
    public string RequiredPermission => "embassy.case_view";
}

public class GetCaseExecutiveBoardHandler
    : IRequestHandler<GetCaseExecutiveBoardQuery, Result<EmbassyBoardResult>>
{
    private readonly ITenantDbContext _context;
    private readonly GetEmbassyBoardHandler _board;

    public GetCaseExecutiveBoardHandler(ITenantDbContext context)
    {
        _context = context;
        _board = new GetEmbassyBoardHandler(context);
    }

    public async Task<Result<EmbassyBoardResult>> Handle(
        GetCaseExecutiveBoardQuery request, CancellationToken ct)
    {
        var stage = await EmbassyLmisHelpers.FindStageByNameAsync(
            _context, EmbassyLmisHelpers.CaseExecutiveStageName, ct);
        if (stage is null)
            return Result<EmbassyBoardResult>.Failure("Case Executive stage not configured", 500);

        // Case Executive is mirror-only — filter VisibleInStages
        return await _board.BoardQueryAsync(
            stage.Id,
            request.Page,
            request.PageSize,
            request.Search,
            request.OfficeId,
            primaryOnly: false,
            ct);
    }
}
