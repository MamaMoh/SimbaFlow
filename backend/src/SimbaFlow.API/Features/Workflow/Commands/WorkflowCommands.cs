using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Features.Workflow.Commands;

public record ExecuteTransitionCommand(Guid CandidateId, Guid TransitionRuleId, string? Notes)
    : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "workflow.execute";
}

public class ExecuteTransitionHandler : IRequestHandler<ExecuteTransitionCommand, Result>
{
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public ExecuteTransitionHandler(IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(ExecuteTransitionCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var result = await _engine.ExecuteTransitionAsync(
            request.CandidateId,
            request.TransitionRuleId,
            userId,
            _currentUser.UserName ?? "unknown",
            request.Notes,
            cancellationToken);

        return result.IsSuccess
            ? Result.Success()
            : Result.Failure(result.Error ?? "Transition failed", 400);
    }
}

public record UpdateStatusCommand(Guid CandidateId, string TrackName, string NewValue, string? Notes)
    : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "workflow.execute";
}

public class UpdateStatusHandler : IRequestHandler<UpdateStatusCommand, Result>
{
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public UpdateStatusHandler(IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateStatusCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result.Failure("Unauthenticated", 401);

        var result = await _engine.UpdateStatusAsync(
            request.CandidateId,
            request.TrackName,
            request.NewValue,
            userId,
            _currentUser.UserName ?? "unknown",
            request.Notes,
            ct: cancellationToken);

        return result.IsSuccess
            ? Result.Success()
            : Result.Failure(result.Error ?? "Status update failed", 400);
    }
}

public record CreateStageCommand(string Name, string? Description, int SortOrder, int StageType)
    : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "workflow.configure";
}

public class CreateStageHandler : IRequestHandler<CreateStageCommand, Result<Guid>>
{
    private readonly ITenantDbContext _context;

    public CreateStageHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<Guid>> Handle(CreateStageCommand request, CancellationToken cancellationToken)
    {
        var definition = await _context.WorkflowDefinitions
            .FirstOrDefaultAsync(w => w.IsActive && !w.IsDeleted, cancellationToken);

        if (definition is null)
            return Result<Guid>.Failure("No active workflow definition found.", 404);

        var stage = new WorkflowStage
        {
            WorkflowDefinitionId = definition.Id,
            Name = request.Name,
            Description = request.Description,
            SortOrder = request.SortOrder,
            StageType = (StageType)request.StageType
        };

        _context.WorkflowStages.Add(stage);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(stage.Id, 201);
    }
}

public record UpdateStageCommand(Guid StageId, string Name, string? Description, int SortOrder, int StageType)
    : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "workflow.configure";
}

public class UpdateStageHandler : IRequestHandler<UpdateStageCommand, Result>
{
    private readonly ITenantDbContext _context;

    public UpdateStageHandler(ITenantDbContext context) => _context = context;

    public async Task<Result> Handle(UpdateStageCommand request, CancellationToken cancellationToken)
    {
        var stage = await _context.WorkflowStages
            .FirstOrDefaultAsync(s => s.Id == request.StageId && !s.IsDeleted, cancellationToken);

        if (stage is null)
            return Result.Failure("Stage not found", 404);

        stage.Name = request.Name;
        stage.Description = request.Description;
        stage.SortOrder = request.SortOrder;
        stage.StageType = (StageType)request.StageType;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public record CreateTransitionRuleCommand(
    Guid SourceStageId, Guid TargetStageId, string ButtonLabel,
    string? ButtonIcon, object? Conditions, string[]? RequiredFields,
    string[]? AllowedRoles, bool RemoveFromSource) : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "workflow.configure";
}

public class CreateTransitionRuleHandler : IRequestHandler<CreateTransitionRuleCommand, Result<Guid>>
{
    private readonly ITenantDbContext _context;

    public CreateTransitionRuleHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<Guid>> Handle(CreateTransitionRuleCommand request, CancellationToken cancellationToken)
    {
        var definition = await _context.WorkflowDefinitions
            .FirstOrDefaultAsync(w => w.IsActive && !w.IsDeleted, cancellationToken);

        if (definition is null)
            return Result<Guid>.Failure("No active workflow definition found.", 404);

        var sourceExists = await _context.WorkflowStages
            .AnyAsync(s => s.Id == request.SourceStageId && !s.IsDeleted, cancellationToken);
        var targetExists = await _context.WorkflowStages
            .AnyAsync(s => s.Id == request.TargetStageId && !s.IsDeleted, cancellationToken);

        if (!sourceExists || !targetExists)
            return Result<Guid>.Failure("Source or target stage not found.", 404);

        var conditionsJson = request.Conditions is null
            ? "{}"
            : JsonSerializer.Serialize(request.Conditions);

        var rule = new WorkflowTransitionRule
        {
            WorkflowDefinitionId = definition.Id,
            SourceStageId = request.SourceStageId,
            TargetStageId = request.TargetStageId,
            ButtonLabel = request.ButtonLabel,
            ButtonIcon = request.ButtonIcon,
            Conditions = JsonDocument.Parse(conditionsJson),
            RequiredFields = request.RequiredFields ?? [],
            AllowedRoles = request.AllowedRoles ?? [],
            RemoveFromSource = request.RemoveFromSource,
            IsActive = true
        };

        _context.WorkflowTransitionRules.Add(rule);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(rule.Id, 201);
    }
}

public record ConfigureParallelTracksCommand(Guid StageId, List<ParallelTrackInput> Tracks)
    : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "workflow.configure";
}

public record ParallelTrackInput(string TrackName, string CompletionStatus, int SortOrder);

public class ConfigureParallelTracksHandler : IRequestHandler<ConfigureParallelTracksCommand, Result>
{
    private readonly ITenantDbContext _context;

    public ConfigureParallelTracksHandler(ITenantDbContext context) => _context = context;

    public async Task<Result> Handle(ConfigureParallelTracksCommand request, CancellationToken cancellationToken)
    {
        var stage = await _context.WorkflowStages
            .Include(s => s.ParallelTracks)
            .FirstOrDefaultAsync(s => s.Id == request.StageId && !s.IsDeleted, cancellationToken);

        if (stage is null)
            return Result.Failure("Stage not found", 404);

        foreach (var existing in stage.ParallelTracks.Where(t => !t.IsDeleted))
            existing.IsDeleted = true;

        foreach (var track in request.Tracks)
        {
            _context.ParallelTrackDefinitions.Add(new ParallelTrackDefinition
            {
                WorkflowStageId = stage.Id,
                TrackName = track.TrackName,
                CompletionStatus = track.CompletionStatus,
                SortOrder = track.SortOrder
            });
        }

        stage.StageType = StageType.ParallelTrack;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

// ─────────────────────────────────────────────────────────────────────────
// Step builder: update / delete transitions, delete stages.
// Lets each agency decide WHO performs each step (AllowedRoles) and reshape
// the pipeline — one role can own several steps, or steps can be split.
// ─────────────────────────────────────────────────────────────────────────

public record UpdateTransitionRuleCommand(
    Guid TransitionId, string ButtonLabel, string? ButtonIcon,
    string[]? RequiredFields, string[]? AllowedRoles,
    bool RemoveFromSource, bool IsActive) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "workflow.configure";
}

public class UpdateTransitionRuleHandler : IRequestHandler<UpdateTransitionRuleCommand, Result>
{
    private readonly ITenantDbContext _context;
    public UpdateTransitionRuleHandler(ITenantDbContext context) => _context = context;

    public async Task<Result> Handle(UpdateTransitionRuleCommand request, CancellationToken ct)
    {
        var rule = await _context.WorkflowTransitionRules
            .FirstOrDefaultAsync(r => r.Id == request.TransitionId && !r.IsDeleted, ct);
        if (rule is null)
            return Result.Failure("Transition not found.", 404);

        if (string.IsNullOrWhiteSpace(request.ButtonLabel))
            return Result.Failure("Button label is required.", 400);

        rule.ButtonLabel = request.ButtonLabel.Trim();
        rule.ButtonIcon = request.ButtonIcon;
        rule.RequiredFields = request.RequiredFields ?? [];
        rule.AllowedRoles = request.AllowedRoles ?? [];
        rule.RemoveFromSource = request.RemoveFromSource;
        rule.IsActive = request.IsActive;

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public record DeleteTransitionRuleCommand(Guid TransitionId) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "workflow.configure";
}

public class DeleteTransitionRuleHandler : IRequestHandler<DeleteTransitionRuleCommand, Result>
{
    private readonly ITenantDbContext _context;
    public DeleteTransitionRuleHandler(ITenantDbContext context) => _context = context;

    public async Task<Result> Handle(DeleteTransitionRuleCommand request, CancellationToken ct)
    {
        var rule = await _context.WorkflowTransitionRules
            .FirstOrDefaultAsync(r => r.Id == request.TransitionId && !r.IsDeleted, ct);
        if (rule is null)
            return Result.Failure("Transition not found.", 404);

        rule.IsDeleted = true;
        await _context.SaveChangesAsync(ct);
        return Result.Success(204);
    }
}

public record DeleteStageCommand(Guid StageId) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "workflow.configure";
}

public class DeleteStageHandler : IRequestHandler<DeleteStageCommand, Result>
{
    private readonly ITenantDbContext _context;
    public DeleteStageHandler(ITenantDbContext context) => _context = context;

    public async Task<Result> Handle(DeleteStageCommand request, CancellationToken ct)
    {
        var stage = await _context.WorkflowStages
            .FirstOrDefaultAsync(s => s.Id == request.StageId && !s.IsDeleted, ct);
        if (stage is null)
            return Result.Failure("Stage not found.", 404);

        // Refuse to remove a stage that still holds candidates — the agency must
        // move them first, otherwise their records would become unreachable.
        var inUse = await _context.Candidates
            .AnyAsync(c => !c.IsDeleted && c.CurrentStageId == request.StageId, ct);
        if (inUse)
            return Result.Failure(
                "This step still has candidates in it. Move them to another step first.", 409);

        stage.IsDeleted = true;

        // Soft-delete transitions that point at this stage so no dead buttons remain.
        var related = await _context.WorkflowTransitionRules
            .Where(r => !r.IsDeleted &&
                (r.SourceStageId == request.StageId || r.TargetStageId == request.StageId))
            .ToListAsync(ct);
        foreach (var r in related) r.IsDeleted = true;

        await _context.SaveChangesAsync(ct);
        return Result.Success(204);
    }
}
