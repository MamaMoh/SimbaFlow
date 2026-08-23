using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.API.Features.Embassy;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Lmis.Queries;

public record LmisBoardRowDto(
    Guid Id,
    string FullName,
    string PassportNumber,
    string? LabourId,
    string? PartnerName,
    Dictionary<string, string> StatusValues,
    string? Insurance,
    string? Milestone,
    int DaysInStage,
    int DaysSinceRegistered,
    bool IsMirror,
    string Source,
    DateTime RegisteredAt);

public record LmisBoardResult(
    List<LmisBoardRowDto> Items,
    /// <summary>The stage this board represents — the UI filters actions to it.</summary>
    Guid StageId,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record GetLmisBoardQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Insurance = null,
    string? Milestone = null,
    bool? MirrorOnly = null) : IRequest<Result<LmisBoardResult>>, IRequirePermission
{
    public string RequiredPermission => "lmis.read";
}

public class GetLmisBoardHandler : IRequestHandler<GetLmisBoardQuery, Result<LmisBoardResult>>
{
    private readonly ITenantDbContext _context;

    public GetLmisBoardHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<LmisBoardResult>> Handle(GetLmisBoardQuery request, CancellationToken ct)
    {
        var lmis = await EmbassyLmisHelpers.FindStageByNameAsync(
            _context, EmbassyLmisHelpers.LmisStageName, ct);
        if (lmis is null)
            return Result<LmisBoardResult>.Failure("LMIS stage not configured", 500);

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
            .Where(c => EmbassyLmisHelpers.IsVisibleInStage(c, lmis.Id))
            .Select(c =>
            {
                var status = EmbassyLmisHelpers.ReadStatusValues(c);
                var isMirror = c.CurrentStageId != lmis.Id && c.VisibleInStages.Contains(lmis.Id);
                return new
                {
                    Candidate = c,
                    Status = status,
                    IsMirror = isMirror,
                    Insurance = EmbassyLmisHelpers.TrackValue(status, "insurance"),
                    Milestone = EmbassyLmisHelpers.TrackValue(status, "milestone")
                };
            });

        if (request.MirrorOnly == true)
            matches = matches.Where(x => x.IsMirror);
        else if (request.MirrorOnly == false)
            matches = matches.Where(x => !x.IsMirror);

        if (!string.IsNullOrWhiteSpace(request.Insurance))
            matches = matches.Where(x =>
                string.Equals(x.Insurance, request.Insurance, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(request.Milestone))
            matches = matches.Where(x =>
                string.Equals(x.Milestone, request.Milestone, StringComparison.OrdinalIgnoreCase));

        var list = matches.ToList();
        var total = list.Count;
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var rows = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var items = rows.Select(x => new LmisBoardRowDto(
            x.Candidate.Id,
            x.Candidate.FullName,
            x.Candidate.PassportNumber,
            x.Candidate.LabourId,
            x.Candidate.PartnerName,
            x.Status,
            x.Insurance,
            x.Milestone,
            EmbassyLmisHelpers.DaysInStage(x.Candidate),
            EmbassyLmisHelpers.DaysSinceRegistered(x.Candidate),
            x.IsMirror,
            x.IsMirror ? "Mirror" : "Primary",
            x.Candidate.RegisteredAt)).ToList();

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return Result<LmisBoardResult>.Success(
            new LmisBoardResult(items, lmis.Id, total, page, pageSize, totalPages));
    }
}
