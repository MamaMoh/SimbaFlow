using MediatR;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Workflow.Commands;

public record ExecuteTransitionCommand(Guid CandidateId, Guid TransitionRuleId, string? Notes) : IRequest<Result>;

public class ExecuteTransitionHandler : IRequestHandler<ExecuteTransitionCommand, Result>
{
    public Task<Result> Handle(ExecuteTransitionCommand request, CancellationToken cancellationToken)
    {
        // TODO: Implement via IWorkflowEngineService
        return Task.FromResult(Result.Success());
    }
}

public record UpdateStatusCommand(Guid CandidateId, string TrackName, string NewValue, string? Notes) : IRequest<Result>;

public class UpdateStatusHandler : IRequestHandler<UpdateStatusCommand, Result>
{
    public Task<Result> Handle(UpdateStatusCommand request, CancellationToken cancellationToken)
    {
        // TODO: Implement via IWorkflowEngineService
        return Task.FromResult(Result.Success());
    }
}

public record CreateStageCommand(string Name, string? Description, int SortOrder, int StageType) : IRequest<Result<Guid>>;

public class CreateStageHandler : IRequestHandler<CreateStageCommand, Result<Guid>>
{
    public Task<Result<Guid>> Handle(CreateStageCommand request, CancellationToken cancellationToken)
    {
        // TODO: Implement
        return Task.FromResult(Result<Guid>.Success(Guid.NewGuid()));
    }
}

public record UpdateStageCommand(Guid StageId, string Name, string? Description, int SortOrder, int StageType) : IRequest<Result>;

public class UpdateStageHandler : IRequestHandler<UpdateStageCommand, Result>
{
    public Task<Result> Handle(UpdateStageCommand request, CancellationToken cancellationToken)
    {
        // TODO: Implement
        return Task.FromResult(Result.Success());
    }
}

public record CreateTransitionRuleCommand(
    Guid SourceStageId, Guid TargetStageId, string ButtonLabel,
    string? ButtonIcon, object? Conditions, string[]? RequiredFields,
    string[]? AllowedRoles, bool RemoveFromSource) : IRequest<Result<Guid>>;

public class CreateTransitionRuleHandler : IRequestHandler<CreateTransitionRuleCommand, Result<Guid>>
{
    public Task<Result<Guid>> Handle(CreateTransitionRuleCommand request, CancellationToken cancellationToken)
    {
        // TODO: Implement
        return Task.FromResult(Result<Guid>.Success(Guid.NewGuid()));
    }
}

public record ConfigureParallelTracksCommand(Guid StageId, List<ParallelTrackInput> Tracks) : IRequest<Result>;
public record ParallelTrackInput(string TrackName, string CompletionStatus, int SortOrder);

public class ConfigureParallelTracksHandler : IRequestHandler<ConfigureParallelTracksCommand, Result>
{
    public Task<Result> Handle(ConfigureParallelTracksCommand request, CancellationToken cancellationToken)
    {
        // TODO: Implement
        return Task.FromResult(Result.Success());
    }
}
