using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Tenancy;

/// <summary>
/// One Telegram chat connected to one SimbaFlow account. Stored in the public schema.
///
/// This used to be a single <c>TelegramChatId</c> column on the account, which made the link
/// one-to-one: linking a second phone silently stole the first one's notifications, and the
/// first phone went quiet with nothing to show why. Owners in particular carry two numbers —
/// the office line and their own — and both have to ring.
///
/// A chat belongs to exactly one account, so the bot always knows whose permissions apply to a
/// message; an account may have several chats, and every one of them is written to.
/// </summary>
public class BotChatLink : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Telegram's own chat id, as a string. Unique across the platform.</summary>
    public string ChatId { get; set; } = string.Empty;

    /// <summary>
    /// The @handle and the name Telegram reports, kept only so the owner's list reads as people
    /// rather than as chat ids. Telegram does not disclose the phone number to a bot.
    /// </summary>
    public string? TelegramUsername { get; set; }
    public string? DisplayName { get; set; }

    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last time this chat sent the bot anything. The owner's list is mostly for finding the
    /// links nobody is using any more, and that is the column that says so.
    /// </summary>
    public DateTime? LastSeenAt { get; set; }
}
