using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.SampleData;

public record RemoveSampleDataCommand : IRequest<Result<int>>, IRequirePermission
{
    public string RequiredPermission => "candidate.delete";
}

/// <summary>
/// Removes everything the sample seeder created, matched on the SMP- application number.
///
/// This is a hard delete, not the soft delete used for real candidates: sample rows exist to be
/// thrown away, and leaving them soft-deleted would keep their passport and labour numbers taken,
/// so a re-seed would fail the uniqueness checks.
/// </summary>
public class RemoveSampleDataHandler : IRequestHandler<RemoveSampleDataCommand, Result<int>>
{
    private readonly ITenantDbContext _context;

    public RemoveSampleDataHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<int>> Handle(RemoveSampleDataCommand request, CancellationToken ct)
    {
        var ids = await _context.Candidates
            .Where(c => c.ApplicationNo != null && c.ApplicationNo.StartsWith(SampleDataSpec.Prefix))
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (ids.Count == 0) return Result<int>.Success(0);

        // Children first — the candidate row is the parent of all of these.
        await _context.ExceptionCases.Where(e => ids.Contains(e.CandidateId)).ExecuteDeleteAsync(ct);
        await _context.WorkflowEvents.Where(e => ids.Contains(e.CandidateId)).ExecuteDeleteAsync(ct);
        await _context.WorkflowSnapshots.Where(s => ids.Contains(s.CandidateId)).ExecuteDeleteAsync(ct);
        await _context.CandidateDocuments.Where(d => ids.Contains(d.CandidateId)).ExecuteDeleteAsync(ct);
        await _context.Candidates.Where(c => ids.Contains(c.Id)).ExecuteDeleteAsync(ct);

        return Result<int>.Success(ids.Count);
    }
}
