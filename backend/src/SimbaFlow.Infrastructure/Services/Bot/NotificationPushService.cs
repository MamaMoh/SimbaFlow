using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Options;
using SimbaFlow.Infrastructure.RealTime;

namespace SimbaFlow.Infrastructure.Services.Bot;

/// <summary>What moved, and who moved it.</summary>
public sealed record StageChangePush(
    Guid TenantId,
    Guid CandidateId,
    string CandidateName,
    string? PassportNumber,
    string? FromStageName,
    string? ToStageName,
    string? ChangedByUserName);

public interface INotificationPushService
{
    Task PushStageChangedAsync(StageChangePush push, CancellationToken ct = default);
}

public sealed class NotificationPushService : INotificationPushService
{
    /// <summary>
    /// How long a candidate's notification stays the one that gets rewritten rather than replaced.
    /// A desk working through a candidate does it in one sitting; the next day's move deserves its
    /// own message so it is not buried inside yesterday's.
    /// </summary>
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMinutes(45);

    private readonly IPlatformDbContext _platform;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITelegramGateway _telegram;
    private readonly ISignalRBroadcaster _broadcaster;
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<TelegramOptions> _options;
    private readonly ILogger<NotificationPushService> _logger;

    public NotificationPushService(
        IPlatformDbContext platform,
        UserManager<ApplicationUser> userManager,
        ITelegramGateway telegram,
        ISignalRBroadcaster broadcaster,
        IMemoryCache cache,
        IOptionsMonitor<TelegramOptions> options,
        ILogger<NotificationPushService> logger)
    {
        _platform = platform;
        _userManager = userManager;
        _telegram = telegram;
        _broadcaster = broadcaster;
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    /// <summary>The message already in the chat for this candidate, and where they started.</summary>
    private sealed record OpenThread(string MessageId, string? FirstStageName);

    // Keyed per chat, not per person: the running message that gets edited in place is a
    // message in one chat, and two of somebody's phones each hold their own.
    private static string CacheKey(Guid tenantId, Guid candidateId, string chatId) =>
        $"bot:stage:{tenantId}:{candidateId}:{chatId}";

    public async Task PushStageChangedAsync(StageChangePush push, CancellationToken ct = default)
    {
        // The agency's own off switch. SignalR still broadcasts — the web application is not
        // what was switched off.
        if (await BotSwitch.IsOffAsync(_platform, push.TenantId, ct))
            return;

        var recipients = await BotRecipients.ForTenantAsync(_platform, push.TenantId, ct);

        foreach (var recipient in recipients)
        {
            // Nobody needs their phone to buzz about the button they just pressed. This alone
            // removes most of the traffic, because the person working a board is usually the only
            // one of the agency's staff who has linked the bot.
            if (!BotNotificationRules.ShouldNotify(recipient.UserName, push.ChangedByUserName))
                continue;

            // Muted, or watching other stages. Checked before the delivery row is written: a
            // message nobody wanted is not a delivery that failed.
            if (!BotSubscription.Wants(recipient.Muted, recipient.WatchStages, push.ToStageName))
                continue;

            var amharic = string.Equals(recipient.PreferredLanguage, "am", StringComparison.OrdinalIgnoreCase);

            // One delivery row per chat. A person with two phones where one send failed is not
            // "sent" and not "failed", and a single row would have to pick one of those.
            foreach (var chatId in recipient.ChatIds)
            {
                var key = CacheKey(push.TenantId, push.CandidateId, chatId);
                var open = _cache.Get<OpenThread>(key);

                // Keep the journey's starting point, so after six hops the one line still reads
                // "New Contracts → Commission" rather than "Arrival → Commission".
                var fromStage = open?.FirstStageName ?? push.FromStageName;

                var body = BotMessages.StageMoved(
                    push.CandidateName, push.PassportNumber, fromStage, push.ToStageName,
                    push.ChangedByUserName, amharic);

                var delivery = new NotificationDelivery
                {
                    TenantId = push.TenantId,
                    UserId = recipient.UserId,
                    Channel = BotChannel.Telegram,
                    EventType = "CandidateStageChanged",
                    PayloadSummary = body.Length > 512 ? body[..512] : body,
                    Status = DeliveryStatus.Pending
                };
                _platform.NotificationDeliveries.Add(delivery);

                try
                {
                    string? messageId;

                    if (open is not null &&
                        await _telegram.EditMessageTextAsync(chatId, open.MessageId, body, ct))
                    {
                        messageId = open.MessageId;
                    }
                    else
                    {
                        // No thread open, or Telegram refused the edit (too old, or unchanged text).
                        messageId = await _telegram.SendMessageAsync(chatId, body, ct);
                    }

                    delivery.Status = messageId is null ? DeliveryStatus.Failed : DeliveryStatus.Sent;
                    delivery.ExternalMessageId = messageId;
                    delivery.SentAt = DateTime.UtcNow;

                    if (messageId is not null)
                    {
                        _cache.Set(
                            key,
                            new OpenThread(messageId, fromStage),
                            new MemoryCacheEntryOptions { SlidingExpiration = CoalesceWindow });
                    }
                }
                catch (Exception ex)
                {
                    delivery.Status = DeliveryStatus.Failed;
                    delivery.Error = BotNotificationRules.SanitizeDeliveryError(
                        ex.Message, _options.CurrentValue.BotToken);
                    _logger.LogWarning(ex, "Telegram stage push failed for user {UserId}", recipient.UserId);
                }
            }

            // The web app's own toast is unaffected by any of the above: someone sitting in
            // front of a board still wants every step as it happens. Once per person, however
            // many phones they carry.
            try
            {
                await _broadcaster.SendPersonalNotificationAsync(recipient.UserId.ToString(), new PersonalNotificationMessage(
                    Title: "Stage update",
                    Body: $"{push.CandidateName} → {push.ToStageName ?? "a new stage"}",
                    ActionUrl: "/candidates",
                    Severity: "info"));
            }
            catch (Exception ex)
            {
                // A hub that will not take the message must not stop the next person's phone.
                _logger.LogWarning(ex, "In-app notification failed for user {UserId}", recipient.UserId);
            }
        }

        await _platform.SaveChangesAsync(ct);
    }
}
