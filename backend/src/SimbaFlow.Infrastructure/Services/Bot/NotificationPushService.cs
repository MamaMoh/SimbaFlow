using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

public interface INotificationPushService
{
    Task PushStageChangedAsync(Guid tenantId, Guid? officeId, string candidateName, string? toStageName, CancellationToken ct = default);
}

public sealed class NotificationPushService : INotificationPushService
{
    private readonly IPlatformDbContext _platform;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITelegramGateway _telegram;
    private readonly ISignalRBroadcaster _broadcaster;
    private readonly IOptionsMonitor<TelegramOptions> _options;
    private readonly ILogger<NotificationPushService> _logger;

    public NotificationPushService(
        IPlatformDbContext platform,
        UserManager<ApplicationUser> userManager,
        ITelegramGateway telegram,
        ISignalRBroadcaster broadcaster,
        IOptionsMonitor<TelegramOptions> options,
        ILogger<NotificationPushService> logger)
    {
        _platform = platform;
        _userManager = userManager;
        _telegram = telegram;
        _broadcaster = broadcaster;
        _options = options;
        _logger = logger;
    }

    public async Task PushStageChangedAsync(Guid tenantId, Guid? officeId, string candidateName, string? toStageName, CancellationToken ct = default)
    {
        var recipients = await _userManager.Users
            .Where(u => u.TenantId == tenantId && u.BotLinked && u.TelegramChatId != null && !u.IsDeleted)
            .Select(u => new { u.Id, u.TelegramChatId, u.PreferredLanguage })
            .ToListAsync(ct);

        foreach (var recipient in recipients)
        {
            var body = string.Equals(recipient.PreferredLanguage, "am", StringComparison.OrdinalIgnoreCase)
                ? $"እጩ {candidateName} ወደ {toStageName ?? "new stage"} ተንቀሳቅሷል።"
                : $"Candidate {candidateName} moved to {toStageName ?? "a new stage"}.";

            var delivery = new NotificationDelivery
            {
                TenantId = tenantId,
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
                    var externalId = await _telegram.SendMessageAsync(recipient.TelegramChatId, body, ct);
                    delivery.Status = externalId is null ? DeliveryStatus.Failed : DeliveryStatus.Sent;
                    delivery.ExternalMessageId = externalId;
                    delivery.SentAt = DateTime.UtcNow;
                }

                await _broadcaster.SendPersonalNotificationAsync(recipient.Id.ToString(), new PersonalNotificationMessage(
                    Title: "Stage update",
                    Body: body,
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
