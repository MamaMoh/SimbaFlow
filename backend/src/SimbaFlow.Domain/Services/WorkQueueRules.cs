namespace SimbaFlow.Domain.Services;

/// <summary>
/// When a candidate counts as stuck, and when a passport counts as about to expire.
///
/// The same two numbers decide what My work shows on the web, what the dashboard counts as
/// overdue, and what the bot answers to /overdue and /expiring. They were a pair of private
/// constants inside one query handler, so the bot would have had its own copy and the two
/// would have drifted the first time anybody tuned one of them.
/// </summary>
public static class WorkQueueRules
{
    /// <summary>Days in one stage before a candidate is worth chasing.</summary>
    public const int OverdueThresholdDays = 14;

    /// <summary>Days of passport validity left before it needs attention.</summary>
    public const int ExpirySoonDays = 30;

    public static DateTime OverdueCutoff(DateTime nowUtc) =>
        nowUtc.AddDays(-OverdueThresholdDays);

    public static DateOnly ExpiryHorizon(DateOnly today) =>
        today.AddDays(ExpirySoonDays);

    public static int DaysInStage(DateTime? stageEnteredAt, DateTime nowUtc) =>
        stageEnteredAt is null ? 0 : Math.Max(0, (int)Math.Floor((nowUtc - stageEnteredAt.Value).TotalDays));
}
