using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Candidates.Commands;

/// <summary>
/// The visa and sponsor details captured when a candidate is marked Ready.
///
/// Deliberately narrow rather than reusing the full update command: that one takes the whole
/// candidate, so sending it three fields would blank everything else on the record.
/// Empty values are ignored, so re-running this never clears what is already there.
/// </summary>
public record SetVisaDetailsCommand(
    Guid CandidateId,
    string? VisaNumber = null,
    string? SponsorName = null,
    string? SponsorIdNumber = null) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "candidate.update";
}

public class SetVisaDetailsHandler : IRequestHandler<SetVisaDetailsCommand, Result>
{
    private readonly ITenantDbContext _context;

    public SetVisaDetailsHandler(ITenantDbContext context) => _context = context;

    public async Task<Result> Handle(SetVisaDetailsCommand request, CancellationToken ct)
    {
        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null) return Result.Failure("Candidate not found", 404);

        static string? Keep(string? incoming, string? current) =>
            string.IsNullOrWhiteSpace(incoming) ? current : incoming.Trim();

        candidate.VisaNumber = Keep(request.VisaNumber, candidate.VisaNumber);
        candidate.SponsorName = Keep(request.SponsorName, candidate.SponsorName);
        candidate.SponsorIdNumber = Keep(request.SponsorIdNumber, candidate.SponsorIdNumber);

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
