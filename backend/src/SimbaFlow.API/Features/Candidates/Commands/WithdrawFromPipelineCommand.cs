using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Features.Candidates.Commands;

/// <summary>
/// Takes a candidate back off the workflow boards.
///
/// Candidates get pushed into New Contracts by a mis-click, and until now the only way back was a
/// database edit. This returns them to the initial stage and clears every board they were mirrored
/// onto, so they sit in intake again rather than half-way down a pipeline nobody meant to start.
///
/// The candidate record and its history are untouched — this is a correction, not a delete, and the
/// transition is written to the event stream like any other so the timeline still explains itself.
/// </summary>
public record WithdrawFromPipelineCommand(Guid CandidateId, string? Reason = null)
    : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "workflow.transition";
}

public class WithdrawFromPipelineHandler : IRequestHandler<WithdrawFromPipelineCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public WithdrawFromPipelineHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(WithdrawFromPipelineCommand request, CancellationToken ct)
    {
        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null) return Result.Failure("Candidate not found", 404);

        var initial = await _context.WorkflowStages
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.IsInitialStage && !s.IsDeleted, ct);
        if (initial is null)
            return Result.Failure("This agency has no intake stage to return the candidate to.", 400);

        if (candidate.CurrentStageId == initial.Id && candidate.VisibleInStages.Length <= 1)
            return Result.Failure("This candidate is already back in intake.", 400);

        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Sign in again — no user context.", 401);

        var state = await _engine.GetCurrentStateAsync(request.CandidateId, ct);
        var nextSeq = await _context.WorkflowEvents
            .Where(e => e.CandidateId == candidate.Id)
            .Select(e => (long?)e.SequenceNumber)
            .MaxAsync(ct) ?? 0;

        _context.WorkflowEvents.Add(new Domain.Entities.Workflow.WorkflowEvent
        {
            CandidateId = candidate.Id,
            SequenceNumber = nextSeq + 1,
            EventType = WorkflowEventType.StageTransitioned,
            FromStageId = state.StageId,
            FromStageName = state.StageName,
            ToStageId = initial.Id,
            ToStageName = initial.Name,
            Data = System.Text.Json.JsonDocument.Parse(
                System.Text.Json.JsonSerializer.Serialize(new { withdrawn = true })),
            UserId = userId,
            UserName = _currentUser.UserName ?? "unknown",
            Notes = request.Reason,
        });

        candidate.CurrentStageId = initial.Id;
        candidate.CurrentStageName = initial.Name;
        candidate.StageEnteredAt = DateTime.UtcNow;
        // Every mirror goes with it, or the candidate keeps appearing on boards they left.
        candidate.VisibleInStages = [initial.Id];

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
