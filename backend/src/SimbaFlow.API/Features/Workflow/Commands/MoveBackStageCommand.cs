using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Features.Workflow.Commands;

/// <summary>
/// What undoing this candidate's last move would do, so it can be confirmed before it is done.
///
/// <c>StageId</c> is the board the question is being asked from. Boards mirror, so a candidate
/// can be listed on one and standing in another; the move belongs to the board they are standing
/// in. Null for the candidates list and the candidate's own page, which are not boards.
/// </summary>
public record GetMoveBackPreviewQuery(Guid CandidateId, Guid? StageId = null)
    : IRequest<Result<MoveBackPreviewDto>>, IRequirePermission
{
    public string RequiredPermission => "workflow.execute";
}

public record MoveBackPreviewDto(
    string FromStageName,
    string ToStageName,
    IReadOnlyList<ClearedValue> Clears);

public class GetMoveBackPreviewHandler
    : IRequestHandler<GetMoveBackPreviewQuery, Result<MoveBackPreviewDto>>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;

    public GetMoveBackPreviewHandler(ITenantDbContext context, IWorkflowEngineService engine)
    {
        _context = context;
        _engine = engine;
    }

    public async Task<Result<MoveBackPreviewDto>> Handle(
        GetMoveBackPreviewQuery request, CancellationToken ct)
    {
        var (plan, error, status) = await StageRewind.PlanAsync(
            _context, _engine, request.CandidateId, ct, request.StageId);

        return plan is null
            ? Result<MoveBackPreviewDto>.Failure(error!, status)
            : Result<MoveBackPreviewDto>.Success(
                new MoveBackPreviewDto(plan.FromStageName, plan.ToStageName, plan.Clears));
    }
}

/// <summary>
/// Puts a candidate back in the stage they came from.
///
/// For the mis-click. Until now the only way back was all the way to intake, which threw away
/// every stage the candidate had legitimately passed through to get where they were — so people
/// left them in the wrong stage instead, and the boards stopped meaning anything.
///
/// The plan is worked out again here rather than taken from the caller: a preview is a few
/// seconds old by the time someone has read it and pressed the button, and in those seconds a
/// colleague may have moved the candidate on. Recomputing means what is written is what is true
/// now, and a candidate who has since moved gets the new answer rather than the stale one.
/// </summary>
public record MoveBackStageCommand(Guid CandidateId, string? Reason = null, Guid? StageId = null)
    : IRequest<Result<MoveBackPreviewDto>>, IRequirePermission
{
    public string RequiredPermission => "workflow.execute";
}

public class MoveBackStageHandler : IRequestHandler<MoveBackStageCommand, Result<MoveBackPreviewDto>>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public MoveBackStageHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result<MoveBackPreviewDto>> Handle(
        MoveBackStageCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result<MoveBackPreviewDto>.Failure("Sign in again — no user context.", 401);

        var (plan, error, status) = await StageRewind.PlanAsync(
            _context, _engine, request.CandidateId, ct, request.StageId);
        if (plan is null) return Result<MoveBackPreviewDto>.Failure(error!, status);

        var candidate = await _context.Candidates
            .FirstOrDefaultAsync(c => c.Id == request.CandidateId && !c.IsDeleted, ct);
        if (candidate is null) return Result<MoveBackPreviewDto>.Failure("Candidate not found", 404);

        await StageRewind.ApplyAsync(
            _context, candidate, plan, userId, _currentUser.UserName ?? "unknown", request.Reason, ct);

        await _context.SaveChangesAsync(ct);

        return Result<MoveBackPreviewDto>.Success(
            new MoveBackPreviewDto(plan.FromStageName, plan.ToStageName, plan.Clears));
    }
}
