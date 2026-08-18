using MediatR;
using Microsoft.Extensions.Logging;
using SimbaFlow.Domain.Events;
using SimbaFlow.Infrastructure.RealTime;

namespace SimbaFlow.Infrastructure.DomainEvents;

/// <summary>
/// Broadcasts candidate status-field updates to the tenant/office SignalR group.
/// </summary>
public class CandidateStatusChangedHandler : INotificationHandler<CandidateStatusChangedEvent>
{
    private readonly ISignalRBroadcaster _broadcaster;
    private readonly ILogger<CandidateStatusChangedHandler> _logger;

    public CandidateStatusChangedHandler(
        ISignalRBroadcaster broadcaster,
        ILogger<CandidateStatusChangedHandler> logger)
    {
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task Handle(CandidateStatusChangedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.TenantId == Guid.Empty)
        {
            _logger.LogWarning(
                "Skipping SignalR broadcast for candidate {CandidateId}: TenantId is empty",
                notification.CandidateId);
            return;
        }

        await _broadcaster.BroadcastCandidateUpdateAsync(
            notification.TenantId,
            new CandidateUpdatedMessage(
                CandidateId: notification.CandidateId,
                ChangeType: "StatusUpdated",
                Field: notification.Field,
                OldValue: notification.OldValue,
                NewValue: notification.NewValue,
                ChangedBy: notification.ChangedBy,
                Timestamp: notification.OccurredAt));
    }
}
