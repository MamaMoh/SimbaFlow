using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Events;

public record CandidateRegisteredEvent(
    Guid CandidateId,
    string CandidateName,
    Guid InitialStageId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
