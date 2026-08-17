namespace SimbaFlow.Domain.Enums;

/// <summary>
/// Notification delivery outcome for bot pushes.
/// </summary>
public enum DeliveryStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    Skipped = 3
}
