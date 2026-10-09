using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Candidates.Queries;

/// <summary>
/// What stands between these candidates and a printed enjaze form.
///
/// Asked before printing rather than after. The bulk print used to refuse a batch of twenty
/// because two of them had a blank visa number, naming the two and leaving the desk to go and
/// find each one, edit them, come back and select the batch again. Now the same answer is
/// fetched up front and turned into boxes to type into, candidate by candidate.
///
/// One row per candidate asked about, whether or not anything is missing, so the caller can tell
/// "nothing missing" from "candidate not found" without counting.
/// </summary>
public record GetVisaFormGapsQuery(List<Guid> CandidateIds)
    : IRequest<Result<List<VisaFormGapDto>>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

/// <summary>
/// <paramref name="Fields"/> is what to show and what to send: a label for the person and a key
/// the save understands, from the one list so they cannot drift.
/// </summary>
public record VisaFormGapDto(
    Guid Id,
    string FullName,
    IReadOnlyList<VisaFormGapField> Fields);

public record VisaFormGapField(string Key, string Label);

public class GetVisaFormGapsHandler
    : IRequestHandler<GetVisaFormGapsQuery, Result<List<VisaFormGapDto>>>
{
    private readonly ITenantDbContext _context;

    public GetVisaFormGapsHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<List<VisaFormGapDto>>> Handle(
        GetVisaFormGapsQuery request, CancellationToken ct)
    {
        var ids = (request.CandidateIds ?? [])
            .Where(id => id != Guid.Empty).Distinct().Take(50).ToList();

        if (ids.Count == 0) return Result<List<VisaFormGapDto>>.Failure("Select at least one candidate", 400);

        var candidates = await _context.Candidates.AsNoTracking()
            .Where(c => ids.Contains(c.Id) && !c.IsDeleted)
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .ToListAsync(ct);

        var gaps = candidates
            .Select(c => new VisaFormGapDto(
                c.Id,
                c.FullName,
                [.. VisaFormReadiness.Gaps(c)
                    .Select(f => new VisaFormGapField(f.Key, f.Label))]))
            .ToList();

        return Result<List<VisaFormGapDto>>.Success(gaps);
    }
}
