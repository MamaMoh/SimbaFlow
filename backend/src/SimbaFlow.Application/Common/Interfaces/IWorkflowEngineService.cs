using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Candidates;

namespace SimbaFlow.Application.Common.Interfaces;

public interface IWorkflowEngineService
{
    Task<Result> InitializeCandidateAsync(Candidate candidate, CancellationToken cancellationToken = default);

    Task<Result> ExecuteTransitionAsync(
        Guid candidateId,
        Guid transitionRuleId,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<Result> UpdateStatusAsync(
        Guid candidateId,
        string trackKey,
        string newValue,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<Result<object>> GetAvailableActionsAsync(Guid candidateId, CancellationToken cancellationToken = default);
}
