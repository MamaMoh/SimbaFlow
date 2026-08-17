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
        await using var tenantDb = await _tenantFactory.CreateAsync(tenantId, cancellationToken);
        var candidate = await tenantDb.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == candidateId && !c.IsDeleted, cancellationToken);
        if (candidate is null)
            return;

        var recipients = await _platform.ApplicationUsers
            .Where(u => u.TenantId == tenantId && u.BotLinked && u.TelegramChatId != null && !u.IsDeleted)
            .Select(u => new { u.Id, u.TelegramChatId, u.PreferredLanguage })
            .ToListAsync(cancellationToken);

        foreach (var recipient in recipients)
        {
            var text = string.Equals(recipient.PreferredLanguage, "am", StringComparison.OrdinalIgnoreCase)
                ? $"ማሳወቂያ: {candidate.FullName} - {messageKey}"
                : $"Notification: {candidate.FullName} - {messageKey}";

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
