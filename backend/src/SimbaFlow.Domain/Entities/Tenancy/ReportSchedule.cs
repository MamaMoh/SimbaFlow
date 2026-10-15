using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Tenancy;

/// <summary>
/// A report from the Reports catalog, emailed to a list of addresses on a repeating schedule.
///
/// Kept in the public schema with its tenant id rather than in each agency's schema, so the
/// sender finds everything that is due with one query instead of opening every agency to look.
/// The report itself is still built from the agency's own schema when it is sent.
/// </summary>
public class ReportSchedule : BaseEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Key from the report catalog, e.g. "pipeline".</summary>
    public string ReportKey { get; set; } = string.Empty;

    /// <summary>"excel" or "pdf" — the attachment's format.</summary>
    public string Format { get; set; } = "excel";

    public ReportScheduleFrequency Frequency { get; set; } = ReportScheduleFrequency.Weekly;

    /// <summary>Hour of the day in Addis time (0–23).</summary>
    public int HourLocal { get; set; } = 8;

    /// <summary>Weekly schedules only.</summary>
    public DayOfWeek? DayOfWeek { get; set; }

    /// <summary>Monthly schedules only, 1–28 so every month has the day.</summary>
    public int? DayOfMonth { get; set; }

    /// <summary>Addresses, comma separated.</summary>
    public string Recipients { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime? NextRunAt { get; set; }
    public DateTime? LastRunAt { get; set; }

    /// <summary>Sent, Partial, Failed or Skipped — what the last run did.</summary>
    public string? LastStatus { get; set; }
    public string? LastError { get; set; }
}
