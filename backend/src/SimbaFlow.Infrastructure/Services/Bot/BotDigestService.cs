using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Options;

namespace SimbaFlow.Infrastructure.Services.Bot;

/// <summary>
/// One message each morning, to each agency, saying what the day starts with.
///
/// Everything else the bot sends is a reaction to somebody pressing something. This is the one
/// message that arrives because nobody did: the candidate who has been in Embassy for three
/// weeks does not generate an event, and that is exactly the one worth chasing.
///
/// Sent at 08:00 Addis time, and only when there is something to say — a digest that reports
/// nothing every morning is the one people mute, and a muted digest misses the morning that
/// matters. What went out is recorded as a delivery, which is also how a restart knows not to
/// send the same morning twice.
/// </summary>
public sealed class BotDigestService : BackgroundService
{
    /// <summary>
    /// Checked four times an hour. The sending window is an hour wide, so a restart cannot step
    /// over it, and the delivery record stops the extra passes sending anything.
    /// </summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private const string DigestEventType = "DailyDigest";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<TelegramOptions> _options;
    private readonly ITelegramGateway _telegram;
    private readonly ILogger<BotDigestService> _logger;

    public BotDigestService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<TelegramOptions> options,
        ITelegramGateway telegram,
        ILogger<BotDigestService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _telegram = telegram;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_telegram.IsConfigured
                    && _options.CurrentValue.Enabled
                    && BotDigestRules.IsSendingHour(DateTime.UtcNow))
                {
                    await RunOnceAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // One bad morning must not take the loop down for every morning after it.
                _logger.LogWarning(ex, "Bot digest pass failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var tenantFactory = scope.ServiceProvider.GetRequiredService<ITenantBotDbContextFactory>();

        var today = BotDigestRules.LocalDate(DateTime.UtcNow);

        // Only agencies with somebody listening.
        var tenantIds = await users.Users
            .Where(u => u.BotLinked && u.TelegramChatId != null && !u.IsDeleted && u.TenantId != null)
            .Select(u => u.TenantId!.Value)
            .Distinct()
            .ToListAsync(ct);

        foreach (var tenantId in tenantIds)
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                await SendForTenantAsync(platform, users, tenantFactory, tenantId, today, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bot digest failed for tenant {TenantId}", tenantId);
            }
        }
    }

    private async Task SendForTenantAsync(
        IPlatformDbContext platform,
        UserManager<ApplicationUser> users,
        ITenantBotDbContextFactory tenantFactory,
        Guid tenantId,
        DateOnly today,
        CancellationToken ct)
    {
        if (await BotSwitch.IsOffAsync(platform, tenantId, ct)) return;

        // Already sent this morning? The record is the guard, not the clock — a redeploy at
        // 08:10 restarts the loop and must not send a second one.
        var since = DateTime.UtcNow.AddHours(-BotDigestRules.EastAfricaOffsetHours).Date;
        var alreadySent = await platform.NotificationDeliveries
            .AsNoTracking()
            .AnyAsync(d => d.TenantId == tenantId
                           && d.EventType == DigestEventType
                           && d.SentAt != null
                           && d.SentAt >= since, ct);
        if (alreadySent) return;

        await using var db = await tenantFactory.CreateAsync(tenantId, false, ct);

        var now = DateTime.UtcNow;
        var active = db.Candidates.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active);

        var yesterday = today.AddDays(-1);
        var registered = await active.CountAsync(
            c => DateOnly.FromDateTime(c.RegisteredAt) == yesterday, ct);
        var overdue = await active.CountAsync(
            c => c.StageEnteredAt != null && c.StageEnteredAt < WorkQueueRules.OverdueCutoff(now), ct);
        var expiring = await active.CountAsync(
            c => c.PassportExpiryDate != null
                 && c.PassportExpiryDate <= WorkQueueRules.ExpiryHorizon(today), ct);
        var openCases = await db.ExceptionCases.AsNoTracking()
            .CountAsync(e => !e.IsDeleted && e.Status == ExceptionStatus.Open, ct);

        if (!BotDigestRules.WorthSending(registered, overdue, expiring, openCases)) return;

        // Muted people are left alone. A stage list is not applied: the digest is about the
        // agency's morning, not about one board.
        var recipients = await users.Users
            .Where(u => u.TenantId == tenantId && u.BotLinked && u.TelegramChatId != null
                        && !u.IsDeleted && !u.BotMuted)
            .Select(u => new { u.Id, u.TelegramChatId, u.PreferredLanguage })
            .ToListAsync(ct);

        if (recipients.Count == 0) return;

        foreach (var recipient in recipients)
        {
            var amharic = string.Equals(
                recipient.PreferredLanguage, "am", StringComparison.OrdinalIgnoreCase);
            var text = BotMessages.Digest(registered, overdue, expiring, openCases, amharic);

            var delivery = new NotificationDelivery
            {
                TenantId = tenantId,
                UserId = recipient.Id,
                Channel = BotChannel.Telegram,
                EventType = DigestEventType,
                PayloadSummary = text.Length > 512 ? text[..512] : text,
                Status = DeliveryStatus.Pending,
            };
            platform.NotificationDeliveries.Add(delivery);

            var externalId = await _telegram.SendMessageAsync(recipient.TelegramChatId!, text, ct);
            delivery.Status = externalId is null ? DeliveryStatus.Failed : DeliveryStatus.Sent;
            delivery.ExternalMessageId = externalId;
            delivery.SentAt = DateTime.UtcNow;
        }

        await platform.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Morning digest sent to {Count} recipients in tenant {TenantId}", recipients.Count, tenantId);
    }
}
