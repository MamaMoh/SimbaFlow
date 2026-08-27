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

        // Same for mirrors, or the stage keeps pulling candidates onto a board that no longer exists.
        var mirrors = await _context.MirrorViewRules
            .Where(m => !m.IsDeleted &&
                (m.WorkflowStageId == request.StageId || m.TargetStageId == request.StageId))
            .ToListAsync(ct);
        foreach (var m in mirrors) m.IsDeleted = true;

        await _context.SaveChangesAsync(ct);
        return Result.Success(204);
    }
}

// ──── Mirror view rules ────
//
// A mirror puts a candidate on a second board without moving them off the first. The
// Embassy → LMIS mirror is the one agencies retune: whether tasheer must be booked before
// LMIS registration is a government rule that differs by destination country, so it has to
// be editable rather than baked into the seed.

public record UpsertMirrorViewRuleCommand(
    Guid? Id,
    Guid SourceStageId,
    Guid TargetStageId,
    JsonElement Conditions,
    bool IsActive) : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "workflow.configure";
}

public class UpsertMirrorViewRuleHandler : IRequestHandler<UpsertMirrorViewRuleCommand, Result<Guid>>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public UpsertMirrorViewRuleHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(UpsertMirrorViewRuleCommand request, CancellationToken ct)
    {
        if (request.SourceStageId == request.TargetStageId)
            return Result<Guid>.Failure("A step cannot mirror into itself.", 400);

        var stages = await _context.WorkflowStages
            .Where(s => !s.IsDeleted &&
                (s.Id == request.SourceStageId || s.Id == request.TargetStageId))
            .Select(s => s.Id)
            .ToListAsync(ct);

        if (!stages.Contains(request.SourceStageId) || !stages.Contains(request.TargetStageId))
            return Result<Guid>.Failure("Source or target step not found.", 404);

        var conditions = ParseConditions(request.Conditions, out var conditionError);
        if (conditions is null)
            return Result<Guid>.Failure(conditionError!, 400);

        MirrorViewRule rule;

        if (request.Id.HasValue)
        {
            var existing = await _context.MirrorViewRules
                .FirstOrDefaultAsync(r => r.Id == request.Id.Value && !r.IsDeleted, ct);
            if (existing is null)
                return Result<Guid>.Failure("Mirror rule not found.", 404);
            rule = existing;
        }
        else
        {
            var duplicate = await _context.MirrorViewRules.AnyAsync(r =>
                !r.IsDeleted &&
                r.WorkflowStageId == request.SourceStageId &&
                r.TargetStageId == request.TargetStageId, ct);
            if (duplicate)
                return Result<Guid>.Failure(
                    "A mirror between these two steps already exists. Edit that one instead.", 409);

            rule = new MirrorViewRule();
            _context.MirrorViewRules.Add(rule);
        }

        rule.WorkflowStageId = request.SourceStageId;
        rule.TargetStageId = request.TargetStageId;
        rule.Conditions = conditions;
        rule.IsActive = request.IsActive;

        await _context.SaveChangesAsync(ct);

        // Apply the new rule to candidates already in flight, otherwise the change appears to
        // do nothing until each candidate's status is next touched.
        if (Guid.TryParse(_currentUser.UserId, out var userId))
            await _engine.ReapplyMirrorViewsAsync(userId, _currentUser.UserName ?? "system", ct);

        return Result<Guid>.Success(rule.Id);
    }

    /// <summary>
    /// Conditions are hand-editable JSON, so a malformed shape has to be rejected with a message
    /// the admin can act on rather than blowing up later inside the evaluator.
    /// </summary>
    private static JsonDocument? ParseConditions(JsonElement element, out string? error)
    {
        error = null;

        if (element.ValueKind != JsonValueKind.Object)
        {
            error = "Conditions must be a JSON object.";
            return null;
        }

        if (element.TryGetProperty("rules", out var rules))
        {
            if (rules.ValueKind != JsonValueKind.Array)
            {
                error = "\"rules\" must be an array.";
                return null;
            }

            foreach (var rule in rules.EnumerateArray())
            {
                if (!rule.TryGetProperty("field", out var field) ||
                    field.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(field.GetString()))
                {
                    error = "Every rule needs a non-empty \"field\".";
                    return null;
                }

                if (!rule.TryGetProperty("op", out var op) || op.ValueKind != JsonValueKind.String)
                {
                    error = "Every rule needs an \"op\".";
                    return null;
                }

                var opName = op.GetString();
                if (opName is not ("eq" or "neq" or "in" or "not_empty" or "empty"))
                {
                    error = $"Unsupported operator \"{opName}\". Use eq, neq, in, not_empty or empty.";
                    return null;
                }

                if (opName is "eq" or "neq" or "in")
                {
                    if (!rule.TryGetProperty("value", out var value))
                    {
                        error = $"Operator \"{opName}\" needs a \"value\".";
                        return null;
                    }

                    if (opName == "in" && value.ValueKind != JsonValueKind.Array)
                    {
                        error = "Operator \"in\" needs an array \"value\".";
                        return null;
                    }
                }
            }
        }

        return JsonDocument.Parse(element.GetRawText());
    }
}

public record DeleteMirrorViewRuleCommand(Guid Id) : IRequest<Result>, IRequirePermission
{
    public string RequiredPermission => "workflow.configure";
}

public class DeleteMirrorViewRuleHandler : IRequestHandler<DeleteMirrorViewRuleCommand, Result>
{
    private readonly ITenantDbContext _context;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public DeleteMirrorViewRuleHandler(
        ITenantDbContext context, IWorkflowEngineService engine, ICurrentUserService currentUser)
    {
        _context = context;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(DeleteMirrorViewRuleCommand request, CancellationToken ct)
    {
        var rule = await _context.MirrorViewRules
            .FirstOrDefaultAsync(r => r.Id == request.Id && !r.IsDeleted, ct);
        if (rule is null)
            return Result.Failure("Mirror rule not found.", 404);

        rule.IsDeleted = true;
        await _context.SaveChangesAsync(ct);

        if (Guid.TryParse(_currentUser.UserId, out var userId))
            await _engine.ReapplyMirrorViewsAsync(userId, _currentUser.UserName ?? "system", ct);

        return Result.Success(204);
    }
}
