using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace SimbaFlow.Infrastructure.RealTime;

public class SignalRBroadcaster : ISignalRBroadcaster
{
    private readonly IHubContext<SimbaFlowHub> _hubContext;
    private readonly ILogger<SignalRBroadcaster> _logger;

    public SignalRBroadcaster(IHubContext<SimbaFlowHub> hubContext, ILogger<SignalRBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task BroadcastCandidateUpdateAsync(Guid tenantId, Guid? officeId, CandidateUpdatedMessage message)
    {
        var group = officeId.HasValue
            ? $"tenant:{tenantId}:office:{officeId}"
            : $"tenant:{tenantId}";

        await _hubContext.Clients.Group(group).SendAsync("candidateUpdated", message);

        _logger.LogDebug("Broadcast candidateUpdated to {Group}: {CandidateId} {ChangeType}",
            group, message.CandidateId, message.ChangeType);
    }

    public async Task SendPersonalNotificationAsync(string userId, PersonalNotificationMessage message)
    {
        await _hubContext.Clients.Group($"user:{userId}").SendAsync("notification", message);
    }

    public async Task BroadcastSystemAlertAsync(Guid tenantId, SystemAlertMessage message)
    {
        await _hubContext.Clients.Group($"tenant:{tenantId}").SendAsync("systemAlert", message);
    }
}
