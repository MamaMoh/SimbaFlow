using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Tenancy;

/// <summary>
/// One-time code used to link a Telegram chat to an existing system user.
/// Stored in the public schema.
/// </summary>
public class BotRegistrationChallenge : BaseEntity
{
    public Guid UserId { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}
