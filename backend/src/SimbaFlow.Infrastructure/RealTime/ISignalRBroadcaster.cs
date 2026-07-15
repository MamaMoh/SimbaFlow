namespace SimbaFlow.Infrastructure.RealTime;

/// <summary>
/// Service for broadcasting real-time updates to connected clients via SignalR.
/// </summary>
public interface ISignalRBroadcaster
{
    /// <summary>Broadcast a candidate status change to all users in the tenant.</summary>
    Task BroadcastCandidateUpdateAsync(Guid tenantId, Guid? officeId, CandidateUpdatedMessage message);

    /// <summary>Send a personal notification to a specific user.</summary>
    Task SendPersonalNotificationAsync(string userId, PersonalNotificationMessage message);

    /// <summary>Broadcast a system alert to all users in a tenant.</summary>
    Task BroadcastSystemAlertAsync(Guid tenantId, SystemAlertMessage message);
}

public record CandidateUpdatedMessage(
    Guid CandidateId,
    string ChangeType,
    string? Field,
    string? OldValue,
    string? NewValue,
    string ChangedBy,
    DateTime Timestamp);

public record PersonalNotificationMessage(
    string Title,
    string Body,
    string? ActionUrl,
    string Severity);

public record SystemAlertMessage(
    string Title,
    string Body,
    string AlertType);
