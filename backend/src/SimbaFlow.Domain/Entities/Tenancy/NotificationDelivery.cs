using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Tenancy;

/// <summary>
/// Outbound bot/notification delivery attempt stored in the public schema.
/// </summary>
public class NotificationDelivery : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public BotChannel Channel { get; set; } = BotChannel.Telegram;
    public string EventType { get; set; } = string.Empty;
    public string PayloadSummary { get; set; } = string.Empty;
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
    public string? ExternalMessageId { get; set; }
    public string? Error { get; set; }
    public DateTime? SentAt { get; set; }
}
