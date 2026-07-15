using SimbaFlow.Domain.Common;

namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Dispatches domain events after successful database commit.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
}
