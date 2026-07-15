using MediatR;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Common;

namespace SimbaFlow.Infrastructure.DomainEvents;

/// <summary>
/// Dispatches domain events via MediatR.Publish after successful database commit.
/// </summary>
public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IPublisher _publisher;

    public DomainEventDispatcher(IPublisher publisher)
    {
        _publisher = publisher;
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var domainEvent in events)
        {
            await _publisher.Publish(domainEvent, ct);
        }
    }
}
