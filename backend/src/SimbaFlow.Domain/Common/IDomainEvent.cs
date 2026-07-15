using MediatR;

namespace SimbaFlow.Domain.Common;

/// <summary>
/// Marker interface for domain events. Leverages MediatR INotification for dispatch.
/// </summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}
