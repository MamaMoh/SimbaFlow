using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Events;

public record CandidateStatusChangedEvent(
    Guid CandidateId,
    string CandidateName,
    Guid TenantId,
    Guid OfficeId,
    string Field,
    string? OldValue,
    string NewValue,
    string ChangedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
