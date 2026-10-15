using System.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Options;

namespace SimbaFlow.API.Features.Tenants;

/// <summary>
/// Chases unpaid subscriptions so the platform team does not have to notice them.
///
/// Once an hour it emails each agency's owner when a payment comes due (a week or two before, on
/// the day, and once it is late), each stage once per due date. An agency still unpaid
/// <c>Billing:AutoSuspendAfterDays</c> days after its due date (14 unless configured; 0 turns
/// it off) is suspended, the owner is told why, and the row is marked so the platform team can
/// see the system did it.
/// </summary>
public sealed class BillingReminderService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BillingReminderService> _logger;

    public BillingReminderService(IServiceScopeFactory scopeFactory, ILogger<BillingReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Let startup migrations and seeding finish first.
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await RunOnceAsync(scope.ServiceProvider, DateOnly.FromDateTime(DateTime.UtcNow), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Billing reminder pass failed");
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

    /// <summary>One pass over every agency. Public so it can be exercised directly.</summary>
    public static async Task RunOnceAsync(IServiceProvider services, DateOnly today, CancellationToken ct)
    {
        var db = services.GetRequiredService<IPlatformDbContext>();
        var email = services.GetRequiredService<IEmailService>();
        var schemas = services.GetRequiredService<ITenantSchemaResolver>();
        var audit = services.GetRequiredService<IAuditService>();
        var config = services.GetRequiredService<IConfiguration>();
        var baseUrl = services.GetRequiredService<IOptionsMonitor<EmailOptions>>().CurrentValue.AppBaseUrl.TrimEnd('/');
        var graceDays = config.GetValue("Billing:AutoSuspendAfterDays", SubscriptionRules.DefaultAutoSuspendAfterDays);

        var agencies = await db.Tenants
            .Where(t => !t.IsDeleted && t.SubscriptionStatus == TenantStatus.Active
                        && t.SubscriptionAmount > 0 && t.NextPaymentDue != null)
            .ToListAsync(ct);

        foreach (var tenant in agencies)
        {
            var due = tenant.NextPaymentDue!.Value;
            var recipients = await RecipientsAsync(db, tenant, ct);

            if (SubscriptionRules.ShouldAutoSuspend(
                    tenant.SubscriptionStatus, tenant.SubscriptionAmount, due, today, graceDays))
            {
                tenant.SubscriptionStatus = TenantStatus.Suspended;
                tenant.AutoSuspendedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                schemas.InvalidateCache(tenant.Id);

                await audit.LogAsync(new AuditEntry(
                    UserId: null, Action: "AutoSuspendAgency", EntityType: "Tenant", EntityId: tenant.Id.ToString(),
                    Success: true, DurationMs: 0,
                    NewValues: $"{{\"dueOn\":\"{due:yyyy-MM-dd}\",\"graceDays\":{graceDays}}}",
                    TenantId: tenant.Id), ct);

                foreach (var to in recipients)
                    await email.SendAsync(to, "Your SimbaFlow subscription has been suspended",
                        SuspendedBody(tenant, due, baseUrl), ct);
                continue;
            }

            var reminder = SubscriptionRules.ReminderFor(due, tenant.BillingCycle, today);
            if (reminder is not RenewalReminder stage) continue;

            // Already sent this stage, or a later one, for this due date.
            if (tenant.LastRenewalReminderFor == due && tenant.LastRenewalReminder is RenewalReminder sent && sent >= stage)
                continue;

            var sentAny = false;
            foreach (var to in recipients)
                sentAny |= await email.SendAsync(to, ReminderSubject(stage, tenant),
                    ReminderBody(stage, tenant, due, today, graceDays, baseUrl), ct);

            // Only remembered once a message actually left, so a mail outage retries next pass.
            if (sentAny)
            {
                tenant.LastRenewalReminder = stage;
                tenant.LastRenewalReminderFor = due;
                await db.SaveChangesAsync(ct);
            }
        }
    }

    /// <summary>The agency's contact address and its owner's, without repeats.</summary>
    private static async Task<List<string>> RecipientsAsync(IPlatformDbContext db, TenantInfo tenant, CancellationToken ct)
    {
        var owner = await db.ApplicationUsers.AsNoTracking()
            .Where(u => u.TenantId == tenant.Id && !u.IsDeleted && u.IsActive)
            .OrderBy(u => u.CreatedAt)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);

        return new[] { tenant.ContactEmail, owner }
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string Money(TenantInfo t) => $"{t.SubscriptionAmount:N2} {t.SubscriptionCurrency}";

    private static string ReminderSubject(RenewalReminder stage, TenantInfo t) => stage switch
    {
        RenewalReminder.DueSoon => $"SimbaFlow subscription for {t.Name} is due soon",
        RenewalReminder.DueToday => $"SimbaFlow subscription for {t.Name} is due today",
        _ => $"SimbaFlow subscription for {t.Name} is overdue",
    };

    private static string ReminderBody(
        RenewalReminder stage, TenantInfo t, DateOnly due, DateOnly today, int graceDays, string baseUrl)
    {
        var name = HttpUtility.HtmlEncode(t.Name);
        var when = stage switch
        {
            RenewalReminder.DueSoon => $"is due on <b>{due:d MMMM yyyy}</b>",
            RenewalReminder.DueToday => "is due <b>today</b>",
            _ => $"was due on <b>{due:d MMMM yyyy}</b> and has not been paid yet",
        };
        var warning = stage == RenewalReminder.Overdue && graceDays > 0
            ? $"<p>If it is still unpaid on <b>{due.AddDays(graceDays):d MMMM yyyy}</b>, sign-in will be suspended until it is settled.</p>"
            : "";
        var link = string.IsNullOrEmpty(baseUrl) ? "" : $"<p><a href=\"{baseUrl}/settings\">See your subscription in SimbaFlow</a></p>";
        return $"""
            <p>Hello,</p>
            <p>The SimbaFlow subscription for {name} ({Money(t)}) {when}.</p>
            {warning}
            {link}
            <p style="color:#555;font-size:13px">If you have already paid, thank you, and please send us the payment reference so we can record it.</p>
            """;
    }

    private static string SuspendedBody(TenantInfo t, DateOnly due, string baseUrl) => $"""
        <p>Hello,</p>
        <p>The SimbaFlow subscription for {HttpUtility.HtmlEncode(t.Name)} ({Money(t)}) was due on
        <b>{due:d MMMM yyyy}</b> and is still unpaid, so sign-in for your agency has been suspended.
        Your data is kept safe.</p>
        <p>Contact the SimbaFlow team with your payment reference and access will be restored.</p>
        """;
}
