using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Events;

public record CandidateStageChangedEvent(
    Guid CandidateId,
    string CandidateName,
    Guid TenantId,
    Guid OfficeId,
    Guid? FromStageId,
    string? FromStageName,
    Guid ToStageId,
    string ToStageName,
    string ChangedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
