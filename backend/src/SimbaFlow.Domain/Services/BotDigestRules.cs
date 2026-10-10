namespace SimbaFlow.Domain.Services;

/// <summary>
/// When the morning digest goes out, and whether it is worth sending.
///
/// Ethiopia keeps UTC+3 all year, so the hour is arithmetic rather than a time-zone database:
/// 08:00 in Addis is 05:00 UTC, every day. Pulled out of the service so the decision can be
/// tested without a clock, a database or a bot.
/// </summary>
public static class BotDigestRules
{
    /// <summary>Hours ahead of UTC. Ethiopia does not observe daylight saving.</summary>
    public const int EastAfricaOffsetHours = 3;

    /// <summary>Local hour the digest is sent — early enough to shape the day.</summary>
    public const int SendAtLocalHour = 8;

    public static DateTime LocalNow(DateTime utcNow) => utcNow.AddHours(EastAfricaOffsetHours);

    /// <summary>The local calendar day a digest would belong to.</summary>
    public static DateOnly LocalDate(DateTime utcNow) => DateOnly.FromDateTime(LocalNow(utcNow));

    /// <summary>
    /// True inside the sending hour. An hour wide rather than a minute: the loop wakes on its
    /// own schedule and a restart must not be able to step over the moment entirely. Sending
    /// twice is prevented by the record of what went out, not by the width of this window.
    /// </summary>
    public static bool IsSendingHour(DateTime utcNow) => LocalNow(utcNow).Hour == SendAtLocalHour;

    /// <summary>
    /// Worth waking someone's phone for?
    ///
    /// A digest that says "nothing to report" every morning is the thing people mute, and once
    /// muted they miss the morning it matters. Silence when there is nothing is the feature.
    /// </summary>
    public static bool WorthSending(int registeredYesterday, int overdue, int expiring, int openCases) =>
        registeredYesterday > 0 || overdue > 0 || expiring > 0 || openCases > 0;
}
