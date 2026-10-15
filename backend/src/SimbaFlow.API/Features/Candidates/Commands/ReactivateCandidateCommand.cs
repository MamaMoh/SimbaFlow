using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.API.Features.Embassy;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Features.Candidates.Commands;

/// <summary>
/// Brings a candidate back from the Inactive list.
///
/// Two things put someone there, and neither could be taken back: an Unfit medical result, and
/// Delete. One misclick on either ended the file, and the only way back was a database edit.
///
/// A removed candidate comes back exactly where they were removed from (intake). An unfit one comes
/// back with the medical set to Booked again — the state it was in before the result was recorded —
/// so the embassy desk can record the right result, or unbook it for a fresh medical. Either way
/// the timeline says who brought them back and why.
///
/// Restoring a removed record takes the permission that removed it. Undoing an Unfit takes either
/// the embassy desk's permission, which recorded it, or the right to amend the candidate.
/// </summary>
public record ReactivateCandidateCommand(Guid CandidateId, string? Reason = null)
    : IRequest<Result>, IRequireAnyPermission
{
    public IReadOnlyList<string> AcceptedPermissions =>
        ["candidate.delete", "candidate.update", "embassy.update"];
}

public class ReactivateCandidateHandler : IRequestHandler<ReactivateCandidateCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public ReactivateCandidateHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ReactivateCandidateCommand request, CancellationToken ct)
    {
        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId, ct);
        if (candidate is null) return Result.Failure("Candidate not found", 404);

        if (!candidate.IsDeleted && candidate.Status == CandidateStatus.Active)
            return Result.Failure("This candidate is already active.", 400);

        var removed = candidate.IsDeleted || candidate.Status == CandidateStatus.Archived;
        var allowed = removed
            ? _currentUser.HasPermission("candidate.delete")
            : _currentUser.HasPermission("candidate.update") || _currentUser.HasPermission("embassy.update");
        if (!allowed && !_currentUser.IsSuperAdmin)
            return Result.Failure(removed
                ? "Only someone who can delete candidates can bring back a removed one."
                : "Only the embassy desk or someone who can edit candidates can bring this one back.", 403);

        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Sign in again — no user context.", 401);
        var userName = _currentUser.UserName ?? "unknown";

        // Everything below is saved once at the end, so a refused medical reset leaves the
        // candidate exactly as they were rather than active with an Unfit result.
        candidate.IsDeleted = false;
        candidate.DeletedAt = null;
        candidate.DeletedBy = null;
        candidate.Status = CandidateStatus.Active;

        var medicalReset = false;
        if (!removed)
        {
            var state = await _engine.GetCurrentStateAsync(candidate.Id, ct);
            var embassy = await EmbassyLmisHelpers.FindStageByNameAsync(
                _context, EmbassyLmisHelpers.EmbassyStageName, ct);
            var unfit = string.Equals(
                state.StatusValues.GetValueOrDefault("medical"), "Unfit", StringComparison.OrdinalIgnoreCase);

            // Only while they are still on the Embassy board. A candidate walked back to intake
            // after the result has no medical track to reset, and is simply active again.
            if (unfit && embassy is not null && EmbassyLmisHelpers.IsVisibleInStage(candidate, embassy.Id))
            {
                var update = await _engine.UpdateStatusAsync(
                    candidate.Id, "medical", "Booked", userId, userName,
                    "Unfit result undone", saveChanges: false, ct: ct);
                if (!update.IsSuccess)
                    return Result.Failure(update.Error ?? "Could not reset the medical result", 400);
                medicalReset = true;
            }
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        var what = removed ? "Restored after removal" : medicalReset
            ? "Reactivated — medical set back to Booked"
            : "Reactivated";

        _context.WorkflowEvents.Add(new WorkflowEvent
        {
            CandidateId = candidate.Id,
            SequenceNumber = await CandidateEventSequence.NextAsync(_context, candidate.Id, ct),
            EventType = WorkflowEventType.Reactivated,
            FromStageId = candidate.CurrentStageId,
            FromStageName = candidate.CurrentStageName,
            ToStageId = candidate.CurrentStageId,
            ToStageName = candidate.CurrentStageName,
            UserId = userId,
            UserName = userName,
            Notes = reason is null ? what : $"{what}: {reason}",
        });

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>
/// The next event number for a candidate, counting events added but not yet saved — the engine may
/// have just written one in the same unit of work.
/// </summary>
internal static class CandidateEventSequence
{
    internal static async Task<long> NextAsync(ITenantDbContext context, Guid candidateId, CancellationToken ct)
    {
        var saved = await context.WorkflowEvents
            .Where(e => e.CandidateId == candidateId)
            .Select(e => (long?)e.SequenceNumber)
            .MaxAsync(ct) ?? 0;

        var pending = context.WorkflowEvents.Local
            .Where(e => e.CandidateId == candidateId)
            .Select(e => e.SequenceNumber)
            .DefaultIfEmpty(0)
            .Max();

        return Math.Max(saved, pending) + 1;
    }
}
