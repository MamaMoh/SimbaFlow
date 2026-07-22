using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Workflow.Commands;

public record ExecuteTransitionCommand(Guid CandidateId, Guid TransitionRuleId, string? Notes) : IRequest<Result>;

public class ExecuteTransitionHandler : IRequestHandler<ExecuteTransitionCommand, Result>
{
    private readonly IWorkflowEngineService _engine;

    public ExecuteTransitionHandler(IWorkflowEngineService engine) => _engine = engine;

    public Task<Result> Handle(ExecuteTransitionCommand request, CancellationToken cancellationToken)
        => _engine.ExecuteTransitionAsync(request.CandidateId, request.TransitionRuleId, request.Notes, cancellationToken);
}

public record UpdateStatusCommand(Guid CandidateId, string TrackName, string NewValue, string? Notes) : IRequest<Result>;

public class UpdateStatusHandler : IRequestHandler<UpdateStatusCommand, Result>
{
    private readonly IWorkflowEngineService _engine;

    public UpdateStatusHandler(IWorkflowEngineService engine) => _engine = engine;

    public Task<Result> Handle(UpdateStatusCommand request, CancellationToken cancellationToken)
        => _engine.UpdateStatusAsync(request.CandidateId, request.TrackName, request.NewValue, request.Notes, cancellationToken);
}

public record CreateStageCommand(string Name, string? Description, int SortOrder, int StageType) : IRequest<Result<Guid>>;

public class CreateStageHandler : IRequestHandler<CreateStageCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _db;

    public CreateStageHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateStageCommand request, CancellationToken cancellationToken)
    {
        var def = await _db.WorkflowDefinitions.FirstOrDefaultAsync(d => d.IsActive && !d.IsDeleted, cancellationToken);
        if (def is null) return Result<Guid>.Failure("No active workflow definition.");

        var stage = new Domain.Entities.Workflow.WorkflowStage
        {
            WorkflowDefinitionId = def.Id,
            Name = request.Name,
            Description = request.Description,
            SortOrder = request.SortOrder,
            StageType = (Domain.Enums.StageType)request.StageType
        };
        _db.WorkflowStages.Add(stage);
        await _db.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(stage.Id, 201);
    }
}

public record UpdateStageCommand(Guid StageId, string Name, string? Description, int SortOrder, int StageType) : IRequest<Result>;

public class UpdateStageHandler : IRequestHandler<UpdateStageCommand, Result>
{
    private readonly IApplicationDbContext _db;

    public UpdateStageHandler(IApplicationDbContext db) => _db = db;

    public async Task<Result> Handle(UpdateStageCommand request, CancellationToken cancellationToken)
    {
        var stage = await _db.WorkflowStages.FirstOrDefaultAsync(s => s.Id == request.StageId && !s.IsDeleted, cancellationToken);
        if (stage is null) return Result.Failure("Stage not found.", 404);
        stage.Name = request.Name;
        stage.Description = request.Description;
        stage.SortOrder = request.SortOrder;
        stage.StageType = (Domain.Enums.StageType)request.StageType;
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public record CreateTransitionRuleCommand(
    Guid SourceStageId, Guid TargetStageId, string ButtonLabel,
    string? ButtonIcon, object? Conditions, string[]? RequiredFields,
    string[]? AllowedRoles, bool RemoveFromSource) : IRequest<Result<Guid>>;

public class CreateTransitionRuleHandler : IRequestHandler<CreateTransitionRuleCommand, Result<Guid>>
{
    public Task<Result<Guid>> Handle(CreateTransitionRuleCommand request, CancellationToken cancellationToken)
        => Task.FromResult(Result<Guid>.Success(Guid.NewGuid()));
}

public record ConfigureParallelTracksCommand(Guid StageId, List<ParallelTrackInput> Tracks) : IRequest<Result>;
public record ParallelTrackInput(string TrackName, string CompletionStatus, int SortOrder);

public class ConfigureParallelTracksHandler : IRequestHandler<ConfigureParallelTracksCommand, Result>
{
    public Task<Result> Handle(ConfigureParallelTracksCommand request, CancellationToken cancellationToken)
        => Task.FromResult(Result.Success());
}
