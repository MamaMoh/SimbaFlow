using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Candidates.Commands;

/// <summary>
/// The visa and sponsor details captured when a candidate is marked Ready.
///
/// Deliberately narrow rather than reusing the full update command: that one takes the whole
/// candidate, so sending it these fields would blank everything else on the record.
/// Empty values are ignored, so re-running this never clears what is already there.
/// </summary>
public record SetVisaDetailsCommand(
    Guid CandidateId,
    string? VisaNumber = null,
    string? SponsorName = null,
    string? SponsorIdNumber = null,
    string? SponsorPhone = null,
    string? SponsorAddress = null,
    string? ContractNo = null,
    string? AgentName = null,
    string? ENumber = null,
    // The passport block, because it is on the enjaze form's list of things that stop it
    // printing. A candidate registered in a hurry reaches the embassy desk with a passport
    // number and no dates, and the desk filling the gaps should not have to open the whole
    // registration form to type two of them.
    string? PassportNumber = null,
    string? PassportIssueDate = null,
    string? PassportExpiryDate = null) : IRequest<Result>, IRequirePermission
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

        // A date that cannot be parsed keeps what is on file rather than clearing it: this
        // command exists to fill gaps, so it must never be able to open one.
        static DateOnly? KeepDate(string? incoming, DateOnly? current) =>
            !string.IsNullOrWhiteSpace(incoming) && DateOnly.TryParse(incoming, out var parsed)
                ? parsed
                : current;

        candidate.VisaNumber = Keep(request.VisaNumber, candidate.VisaNumber);
        candidate.SponsorName = Keep(request.SponsorName, candidate.SponsorName);
        candidate.SponsorIdNumber = Keep(request.SponsorIdNumber, candidate.SponsorIdNumber);
        candidate.SponsorPhone = Keep(request.SponsorPhone, candidate.SponsorPhone);
        candidate.SponsorAddress = Keep(request.SponsorAddress, candidate.SponsorAddress);
        candidate.ContractNo = Keep(request.ContractNo, candidate.ContractNo);
        candidate.AgentName = Keep(request.AgentName, candidate.AgentName);
        candidate.ENumber = Keep(request.ENumber, candidate.ENumber);
        candidate.PassportNumber = Keep(request.PassportNumber, candidate.PassportNumber) ?? "";
        candidate.PassportIssueDate = KeepDate(request.PassportIssueDate, candidate.PassportIssueDate);
        candidate.PassportExpiryDate = KeepDate(request.PassportExpiryDate, candidate.PassportExpiryDate);

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
