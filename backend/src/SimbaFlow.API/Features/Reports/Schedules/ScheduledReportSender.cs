using Microsoft.EntityFrameworkCore;
using SimbaFlow.API.Features.Reports.Queries;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Services.Bot;

namespace SimbaFlow.API.Features.Reports.Schedules;

public static class ReportScheduleStatus
{
    public const string Sent = "Sent";
    public const string Partial = "Partial";
    public const string Failed = "Failed";
    public const string Skipped = "Skipped";
}

/// <summary>What one run of a schedule did.</summary>
public record ScheduledReportOutcome(string Status, int Delivered, int Recipients, string? Error);

public interface IScheduledReportSender
{
    /// <summary>
    /// Builds the schedule's report from its agency's data and emails it to each recipient.
    /// Never throws for an ordinary failure: the outcome says what went wrong, so the schedule
    /// can show it.
    /// </summary>
    Task<ScheduledReportOutcome> SendAsync(ReportSchedule schedule, CancellationToken ct = default);
}

public sealed class ScheduledReportSender : IScheduledReportSender
{
    private readonly IPlatformDbContext _platform;
    private readonly ITenantBotDbContextFactory _tenantFactory;
    private readonly IReportExportService _export;
    private readonly IEmailService _email;
    private readonly ILogger<ScheduledReportSender> _logger;

    public ScheduledReportSender(
        IPlatformDbContext platform,
        ITenantBotDbContextFactory tenantFactory,
        IReportExportService export,
        IEmailService email,
        ILogger<ScheduledReportSender> logger)
    {
        _platform = platform;
        _tenantFactory = tenantFactory;
        _export = export;
        _email = email;
        _logger = logger;
    }

    public async Task<ScheduledReportOutcome> SendAsync(ReportSchedule schedule, CancellationToken ct = default)
    {
        var (recipients, _) = ReportScheduleRules.ParseRecipients(schedule.Recipients);
        if (recipients.Count == 0)
            return new(ReportScheduleStatus.Failed, 0, 0, "The schedule has no valid email addresses.");

        if (!_email.IsConfigured)
            return new(ReportScheduleStatus.Skipped, 0, recipients.Count,
                "Email is not set up on this server, so nothing was sent.");

        var agencyName = await _platform.Tenants.AsNoTracking()
            .Where(t => t.Id == schedule.TenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct);

        // Built against the agency's own schema, as the scheduler — not as whoever set it up,
        // who may have left since. The report reads the whole agency, as it does on screen.
        await using var db = await _tenantFactory.CreateAsync(schedule.TenantId, false, ct);
        var built = await new GetReportHandler(db).Handle(new GetReportQuery(schedule.ReportKey), ct);
        if (!built.IsSuccess || built.Data is null)
            return new(ReportScheduleStatus.Failed, 0, recipients.Count, built.Error ?? "The report could not be built.");

        var report = built.Data;
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var attachment = schedule.Format == "pdf"
            ? new EmailAttachment($"{schedule.ReportKey}_{stamp}.pdf", "application/pdf", _export.ToPdf(report))
            : new EmailAttachment($"{schedule.ReportKey}_{stamp}.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", _export.ToExcel(report));

        var subject = ScheduledReportEmail.Subject(report, agencyName);
        var body = ScheduledReportEmail.HtmlBody(
            report, agencyName, attachment.FileName, ReportScheduleRules.Describe(schedule.Frequency, schedule.HourLocal, schedule.DayOfWeek, schedule.DayOfMonth));

        var delivered = 0;
        foreach (var to in recipients)
        {
            if (await _email.SendAsync(to, subject, body, [attachment], ct))
                delivered++;
        }

        _logger.LogInformation(
            "Scheduled report {ReportKey} for tenant {TenantId}: {Delivered}/{Total} delivered",
            schedule.ReportKey, schedule.TenantId, delivered, recipients.Count);

        return delivered == recipients.Count
            ? new(ReportScheduleStatus.Sent, delivered, recipients.Count, null)
            : delivered == 0
                ? new(ReportScheduleStatus.Failed, 0, recipients.Count, "The mail server did not accept any of the messages.")
                : new(ReportScheduleStatus.Partial, delivered, recipients.Count,
                    $"{recipients.Count - delivered} of {recipients.Count} messages could not be sent.");
    }
}
