using MediatR;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Workflow.Queries;

public record GetAvailableActionsQuery(Guid CandidateId) : IRequest<Result<object>>;

public class GetAvailableActionsHandler : IRequestHandler<GetAvailableActionsQuery, Result<object>>
{
    public Task<Result<object>> Handle(GetAvailableActionsQuery request, CancellationToken cancellationToken)
    {
        // TODO: Implement via IWorkflowEngineService
        return Task.FromResult(Result<object>.Success(Array.Empty<object>()));
    }
}

public record GetWorkflowStateQuery(Guid CandidateId) : IRequest<Result<object>>;

public class GetWorkflowStateHandler : IRequestHandler<GetWorkflowStateQuery, Result<object>>
{
    public Task<Result<object>> Handle(GetWorkflowStateQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result<object>.Success(new { }));
    }
}

public record GetWorkflowEventsQuery(Guid CandidateId) : IRequest<Result<object>>;

public class GetWorkflowEventsHandler : IRequestHandler<GetWorkflowEventsQuery, Result<object>>
{
    public Task<Result<object>> Handle(GetWorkflowEventsQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result<object>.Success(Array.Empty<object>()));
    }
}

public record GetViewCandidatesQuery(Guid StageId, int Page, int PageSize, string? Search, Guid? OfficeId) : IRequest<Result<object>>;

public class GetViewCandidatesHandler : IRequestHandler<GetViewCandidatesQuery, Result<object>>
{
    public Task<Result<object>> Handle(GetViewCandidatesQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result<object>.Success(new { items = Array.Empty<object>(), totalCount = 0 }));
    }
}

public record GetWorkflowDefinitionQuery : IRequest<Result<object>>;

public class GetWorkflowDefinitionHandler : IRequestHandler<GetWorkflowDefinitionQuery, Result<object>>
{
    public Task<Result<object>> Handle(GetWorkflowDefinitionQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result<object>.Success(new { }));
    }
}
