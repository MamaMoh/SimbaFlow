namespace SimbaFlow.Domain.Services;

/// <summary>Reporting window for a `/stats` request.</summary>
public enum StatsPeriod
{
    Week,
    Month,
    Year,
    AllTime
}

/// <summary>
/// Parsing and formatting rules for the Telegram `/stats` command, kept separate from the
/// dispatcher so the argument handling and labels are unit-testable.
/// </summary>
public static class BotStatsRules
{
    /// <summary>Permissions that allow pulling agency-wide numbers from the bot.</summary>
    public static readonly string[] AllowedPermissions =
        ["report.view", "candidate.read", "system.admin"];

    /// <summary>
    /// Interprets the argument after `/stats`. Returns a period when the argument names one
    /// (or is empty), otherwise treats the argument as a stage name to look up.
    /// </summary>
    public static (StatsPeriod? Period, string? StageQuery) ParseArgument(string? argument)
    {
        var arg = argument?.Trim();
        if (string.IsNullOrEmpty(arg)) return (StatsPeriod.AllTime, null);

        return arg.ToLowerInvariant() switch
        {
            "week" or "w" or "thisweek" or "this-week" => (StatsPeriod.Week, null),
            "month" or "m" or "thismonth" or "this-month" => (StatsPeriod.Month, null),
            "year" or "y" or "thisyear" or "this-year" => (StatsPeriod.Year, null),
            "all" or "total" => (StatsPeriod.AllTime, null),
            _ => (null, arg),
        };
    }

    /// <summary>Inclusive start of the window; null for all-time.</summary>
    public static DateTime? StartOf(StatsPeriod period, DateTime nowUtc) => period switch
    {
        // Week runs Monday→now, which is how agencies talk about "this week".
        StatsPeriod.Week => nowUtc.Date.AddDays(-(((int)nowUtc.DayOfWeek + 6) % 7)),
        StatsPeriod.Month => new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc),
        StatsPeriod.Year => new DateTime(nowUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        _ => null,
    };

    public static string PeriodLabel(StatsPeriod period, bool amharic) => period switch
    {
        StatsPeriod.Week => amharic ? "በዚህ ሳምንት" : "This week",
        StatsPeriod.Month => amharic ? "በዚህ ወር" : "This month",
        StatsPeriod.Year => amharic ? "በዚህ ዓመት" : "This year",
        _ => amharic ? "በጠቅላላ" : "All time",
    };
}
