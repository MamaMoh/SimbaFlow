using System.Net.Mail;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Services;

/// <summary>
/// When a scheduled report goes out next, and who it goes to.
///
/// Times are Addis time, which is UTC+3 all year (see <see cref="BotDigestRules"/>), so the next
/// run is arithmetic rather than a time-zone lookup. Kept apart from the sender so the calendar
/// can be tested without a clock, a database or a mail server.
/// </summary>
public static class ReportScheduleRules
{
    public const int MaxRecipients = 20;

    /// <summary>Highest day of month a schedule may use: every month has a 28th.</summary>
    public const int MaxDayOfMonth = 28;

    public static readonly IReadOnlyList<string> Formats = ["excel", "pdf"];

    /// <summary>
    /// The first sending moment strictly after <paramref name="afterUtc"/>, in UTC.
    ///
    /// Strictly after, so a run that finishes inside its own hour does not find itself due again.
    /// </summary>
    public static DateTime NextRunUtc(
        ReportScheduleFrequency frequency,
        int hourLocal,
        DayOfWeek? dayOfWeek,
        int? dayOfMonth,
        DateTime afterUtc)
    {
        var offset = TimeSpan.FromHours(BotDigestRules.EastAfricaOffsetHours);
        var localAfter = afterUtc + offset;
        var hour = Math.Clamp(hourLocal, 0, 23);

        // Walk forward a day at a time from today; a month is the longest gap, so 62 days is
        // always enough to find the next match.
        for (var i = 0; i <= 62; i++)
        {
            var day = localAfter.Date.AddDays(i);
            if (!Matches(frequency, day, dayOfWeek, dayOfMonth)) continue;

            var candidate = day.AddHours(hour);
            if (candidate > localAfter)
                return DateTime.SpecifyKind(candidate - offset, DateTimeKind.Utc);
        }

        throw new InvalidOperationException("No sending day found for the schedule.");
    }

    /// <summary>"every Monday at 08:00 (Addis time)" — for the email footer and the list.</summary>
    public static string Describe(
        ReportScheduleFrequency frequency, int hourLocal, DayOfWeek? dayOfWeek, int? dayOfMonth)
    {
        var at = $"at {Math.Clamp(hourLocal, 0, 23):00}:00 (Addis time)";
        return frequency switch
        {
            ReportScheduleFrequency.Daily => $"every day {at}",
            ReportScheduleFrequency.Weekly => $"every {dayOfWeek ?? DayOfWeek.Monday} {at}",
            ReportScheduleFrequency.Monthly => $"on day {Math.Clamp(dayOfMonth ?? 1, 1, MaxDayOfMonth)} of every month {at}",
            _ => at
        };
    }

    private static bool Matches(
        ReportScheduleFrequency frequency, DateTime day, DayOfWeek? dayOfWeek, int? dayOfMonth) =>
        frequency switch
        {
            ReportScheduleFrequency.Daily => true,
            ReportScheduleFrequency.Weekly => day.DayOfWeek == (dayOfWeek ?? DayOfWeek.Monday),
            ReportScheduleFrequency.Monthly => day.Day == Math.Clamp(dayOfMonth ?? 1, 1, MaxDayOfMonth),
            _ => false
        };

    /// <summary>
    /// Splits a typed list of addresses on commas, semicolons, spaces or new lines, drops
    /// duplicates (ignoring case) and returns the ones that are not addresses separately, so the
    /// form can say which one was mistyped instead of silently dropping it.
    /// </summary>
    public static (List<string> Valid, List<string> Invalid) ParseRecipients(string? raw)
    {
        var valid = new List<string>();
        var invalid = new List<string>();
        if (string.IsNullOrWhiteSpace(raw)) return (valid, invalid);

        var parts = raw.Split([',', ';', '\n', '\r', ' ', '\t'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (valid.Contains(part, StringComparer.OrdinalIgnoreCase)) continue;
            if (IsEmail(part)) valid.Add(part);
            else if (!invalid.Contains(part)) invalid.Add(part);
        }

        return (valid, invalid);
    }

    private static bool IsEmail(string value)
    {
        if (value.Length > 254 || !value.Contains('@')) return false;
        try
        {
            var address = new MailAddress(value);
            // MailAddress accepts "Name <a@b>" forms; only a bare address is wanted here.
            return address.Address == value && address.Host.Contains('.');
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
