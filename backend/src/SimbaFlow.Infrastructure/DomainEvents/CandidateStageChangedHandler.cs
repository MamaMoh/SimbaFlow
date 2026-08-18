using MediatR;
using Microsoft.Extensions.Logging;
using SimbaFlow.Domain.Events;
using SimbaFlow.Infrastructure.RealTime;

namespace SimbaFlow.Infrastructure.DomainEvents;

/// <summary>
/// Broadcasts candidate stage transitions to the tenant/office SignalR group.
/// </summary>
public class CandidateStageChangedHandler : INotificationHandler<CandidateStageChangedEvent>
{
    private readonly ISignalRBroadcaster _broadcaster;
    private readonly Services.Bot.INotificationPushService _push;
    private readonly ILogger<CandidateStageChangedHandler> _logger;

    public CandidateStageChangedHandler(
        ISignalRBroadcaster broadcaster,
        Services.Bot.INotificationPushService push,
        ILogger<CandidateStageChangedHandler> logger)
    {
        _broadcaster = broadcaster;
        _push = push;
        _logger = logger;
    }

    public async Task Handle(CandidateStageChangedEvent notification, CancellationToken cancellationToken)
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
                ChangeType: "StageTransitioned",
                Field: "currentStage",
                OldValue: notification.FromStageName,
                NewValue: notification.ToStageName,
                ChangedBy: notification.ChangedBy,
                Timestamp: notification.OccurredAt));

        try
        {
            await _push.PushStageChangedAsync(
                notification.TenantId,
                notification.CandidateName,
                notification.ToStageName,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram push failed for candidate {CandidateId}", notification.CandidateId);
        }
    }
}
