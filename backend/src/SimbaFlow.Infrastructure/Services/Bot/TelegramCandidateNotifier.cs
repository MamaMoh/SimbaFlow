using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Options;

namespace SimbaFlow.Infrastructure.Services.Bot;

public sealed class TelegramCandidateNotifier : ICandidateNotifier
{
    private readonly ITenantBotDbContextFactory _tenantFactory;
    private readonly IPlatformDbContext _platform;
    private readonly ICurrentUserService _currentUser;
    private readonly ITelegramGateway _telegram;
    private readonly IOptionsMonitor<TelegramOptions> _options;
    private readonly ILogger<TelegramCandidateNotifier> _logger;

    public TelegramCandidateNotifier(
        ITenantBotDbContextFactory tenantFactory,
        IPlatformDbContext platform,
        ICurrentUserService currentUser,
        ITelegramGateway telegram,
        IOptionsMonitor<TelegramOptions> options,
        ILogger<TelegramCandidateNotifier> logger)
    {
        _tenantFactory = tenantFactory;
        _platform = platform;
        _currentUser = currentUser;
        _telegram = telegram;
        _options = options;
        _logger = logger;
    }

    public async Task NotifyAsync(Guid candidateId, string messageKey, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.TenantId.HasValue)
            return;

        var tenantId = _currentUser.TenantId.Value;

        // The agency's own off switch. Checked before anything is drawn or sent, so turning the
        // bot off is silence rather than a pile of failed deliveries.
        if (await BotSwitch.IsOffAsync(_platform, tenantId, cancellationToken))
            return;

        await using var tenantDb = await _tenantFactory.CreateAsync(tenantId, _currentUser.IsSuperAdmin, cancellationToken);
        var candidate = await tenantDb.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == candidateId && !c.IsDeleted, cancellationToken);
        if (candidate is null)
            return;

        var recipients = await _platform.ApplicationUsers
            .Where(u => u.TenantId == tenantId && u.BotLinked && u.TelegramChatId != null && !u.IsDeleted)
            .Select(u => new
            {
                u.Id, u.UserName, u.TelegramChatId, u.PreferredLanguage, u.BotMuted, u.BotWatchStages,
            })
            .ToListAsync(cancellationToken);

        foreach (var recipient in recipients)
        {
            // The person who triggered this does not need to be told about it.
            if (!BotNotificationRules.ShouldNotify(recipient.UserName, _currentUser.UserName))
                continue;

            // Muted, or watching other stages than the one this candidate is standing in.
            if (!BotSubscription.Wants(
                    recipient.BotMuted, recipient.BotWatchStages, candidate.CurrentStageName))
                continue;

            // Was "Notification: FOZIYA SEID YIMER - departure.notified" — the system talking to
            // itself in front of the user. BotMessages turns the key into a sentence.
            var text = BotMessages.EventHeadline(
                candidate.FullName,
                candidate.PassportNumber,
                messageKey,
                string.Equals(recipient.PreferredLanguage, "am", StringComparison.OrdinalIgnoreCase),
                _currentUser.UserName);

            var delivery = new NotificationDelivery
            {
                TenantId = tenantId,
                UserId = recipient.Id,
                Channel = BotChannel.Telegram,
                EventType = messageKey,
                PayloadSummary = text,
                Status = DeliveryStatus.Pending
            };
            _platform.NotificationDeliveries.Add(delivery);

            try
            {
                var externalId = await _telegram.SendMessageAsync(recipient.TelegramChatId!, text, cancellationToken);
                delivery.Status = externalId is null ? DeliveryStatus.Failed : DeliveryStatus.Sent;
                delivery.ExternalMessageId = externalId;
                delivery.SentAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                delivery.Status = DeliveryStatus.Failed;
                delivery.Error = BotNotificationRules.SanitizeDeliveryError(
                    ex.Message, _options.CurrentValue.BotToken);
                _logger.LogWarning(ex, "Telegram candidate notify failed for candidate {CandidateId}", candidateId);
            }
        }

        await _platform.SaveChangesAsync(cancellationToken);
    }
}
