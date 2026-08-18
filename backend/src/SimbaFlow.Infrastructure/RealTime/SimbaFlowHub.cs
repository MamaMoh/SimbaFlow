using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace SimbaFlow.Infrastructure.RealTime;

/// <summary>
/// Central SignalR hub for real-time candidate and notification updates.
/// Users are grouped by tenant for scoped broadcasting.
/// </summary>
[Authorize]
public class SimbaFlowHub : Hub
{
    private readonly ILogger<SimbaFlowHub> _logger;

    public SimbaFlowHub(ILogger<SimbaFlowHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var tenantId = Context.User?.FindFirst("tenant_id")?.Value;
        var userId = Context.UserIdentifier;

        if (tenantId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{tenantId}");
        }

        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        _logger.LogDebug("Client connected: {ConnectionId}, Tenant: {TenantId}",
            Context.ConnectionId, tenantId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
