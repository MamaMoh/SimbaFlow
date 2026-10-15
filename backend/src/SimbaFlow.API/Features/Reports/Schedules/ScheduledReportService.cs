using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Reports.Schedules;

/// <summary>
/// Sends scheduled reports when they fall due.
///
/// Each schedule carries the moment it next goes out. A pass picks up every active schedule
/// whose moment has passed, moves that moment on to the following run and saves it before
/// sending — so a crash mid-send, or a second instance, skips a run rather than mailing
/// everyone twice. A pass after downtime sends each overdue schedule once, not once per
/// missed run.
/// </summary>
public sealed class ScheduledReportService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledReportService> _logger;

    public ScheduledReportService(IServiceScopeFactory scopeFactory, ILogger<ScheduledReportService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Let startup migrations and seeding finish first.
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Scheduled report pass failed");
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
        List<Guid> dueIds;
        using (var scope = _scopeFactory.CreateScope())
        {
            var platform = scope.ServiceProvider.GetRequiredService<IPlatformDbContext>();
            var now = DateTime.UtcNow;
            dueIds = await platform.ReportSchedules.AsNoTracking()
                .Where(s => s.IsActive && !s.IsDeleted && s.NextRunAt != null && s.NextRunAt <= now)
                .OrderBy(s => s.NextRunAt)
                .Select(s => s.Id)
                .ToListAsync(ct);
        }

        foreach (var id in dueIds)
        {
            if (ct.IsCancellationRequested) return;

            // A scope per schedule: one agency's failure leaves nothing tracked for the next.
            using var scope = _scopeFactory.CreateScope();
            try
            {
                await RunScheduleAsync(scope.ServiceProvider, id, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Scheduled report {ScheduleId} failed", id);
            }
        }
    }

    private async Task RunScheduleAsync(IServiceProvider services, Guid id, CancellationToken ct)
    {
        var platform = services.GetRequiredService<IPlatformDbContext>();
        var sender = services.GetRequiredService<IScheduledReportSender>();

        var schedule = await platform.ReportSchedules.FirstOrDefaultAsync(s => s.Id == id, ct);
        var now = DateTime.UtcNow;
        if (schedule is null || !schedule.IsActive || schedule.IsDeleted
            || schedule.NextRunAt is null || schedule.NextRunAt > now)
            return;

        // Claim the run before doing it. RowVersion makes a second instance's claim fail here.
        schedule.NextRunAt = ReportScheduleRules.NextRunUtc(
            schedule.Frequency, schedule.HourLocal, schedule.DayOfWeek, schedule.DayOfMonth, now);
        try
        {
            await platform.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return;
        }

        var tenantActive = await platform.Tenants.AsNoTracking()
            .AnyAsync(t => t.Id == schedule.TenantId && !t.IsDeleted
                           && t.SubscriptionStatus == TenantStatus.Active, ct);

        var outcome = tenantActive
            ? await sender.SendAsync(schedule, ct)
            : new ScheduledReportOutcome(ReportScheduleStatus.Skipped, 0, 0, "The agency is not active.");

        schedule.LastRunAt = DateTime.UtcNow;
        schedule.LastStatus = outcome.Status;
        schedule.LastError = outcome.Error;
        await platform.SaveChangesAsync(ct);
    }
}
