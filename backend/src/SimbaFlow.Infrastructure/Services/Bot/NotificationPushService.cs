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

    private static string CacheKey(Guid tenantId, Guid candidateId, Guid userId) =>
        $"bot:stage:{tenantId}:{candidateId}:{userId}";

    public async Task PushStageChangedAsync(StageChangePush push, CancellationToken ct = default)
    {
        var recipients = await _userManager.Users
            .Where(u => u.TenantId == push.TenantId && u.BotLinked && u.TelegramChatId != null && !u.IsDeleted)
            .Select(u => new { u.Id, u.UserName, u.TelegramChatId, u.PreferredLanguage })
            .ToListAsync(ct);

        foreach (var recipient in recipients)
        {
            // Nobody needs their phone to buzz about the button they just pressed. This alone
            // removes most of the traffic, because the person working a board is usually the only
            // one of the agency's staff who has linked the bot.
            if (!BotNotificationRules.ShouldNotify(recipient.UserName, push.ChangedByUserName))
                continue;

            var amharic = string.Equals(recipient.PreferredLanguage, "am", StringComparison.OrdinalIgnoreCase);
            var key = CacheKey(push.TenantId, push.CandidateId, recipient.Id);
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
                UserId = recipient.Id,
                Channel = BotChannel.Telegram,
                EventType = "CandidateStageChanged",
                PayloadSummary = body.Length > 512 ? body[..512] : body,
                Status = DeliveryStatus.Pending
            };
            _platform.NotificationDeliveries.Add(delivery);

            try
            {
                if (string.IsNullOrWhiteSpace(recipient.TelegramChatId))
                {
                    delivery.Status = DeliveryStatus.Skipped;
                }
                else
                {
                    string? messageId = null;

                    if (open is not null &&
                        await _telegram.EditMessageTextAsync(recipient.TelegramChatId, open.MessageId, body, ct))
                    {
                        messageId = open.MessageId;
                    }
                    else
                    {
                        // No thread open, or Telegram refused the edit (too old, or unchanged text).
                        messageId = await _telegram.SendMessageAsync(recipient.TelegramChatId, body, ct);
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

                // The web app's own toast is unaffected by any of the above: someone sitting in
                // front of a board still wants every step as it happens.
                await _broadcaster.SendPersonalNotificationAsync(recipient.Id.ToString(), new PersonalNotificationMessage(
                    Title: "Stage update",
                    Body: $"{push.CandidateName} → {push.ToStageName ?? "a new stage"}",
                    ActionUrl: "/candidates",
                    Severity: "info"));
            }
            catch (Exception ex)
            {
                delivery.Status = DeliveryStatus.Failed;
                delivery.Error = BotNotificationRules.SanitizeDeliveryError(
                    ex.Message, _options.CurrentValue.BotToken);
                _logger.LogWarning(ex, "Telegram stage push failed for user {UserId}", recipient.Id);
            }
        }

        await _platform.SaveChangesAsync(ct);
    }
}
