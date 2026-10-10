using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// The morning digest fires in the morning, in Addis, and stays quiet when there is nothing
/// to say.
///
/// A digest is the one message the bot sends that nobody asked for, which makes both halves of
/// that load-bearing: send it at the wrong hour and it wakes people in the night; send it every
/// day regardless and it gets muted, and a muted digest misses the morning it mattered.
/// </summary>
public class BotDigestRulesTests
{
    private static DateTime Utc(int hour, int minute = 0) =>
        new(2026, 10, 11, hour, minute, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(5, true)]    // 08:00 in Addis
    [InlineData(5, true, 59)]
    [InlineData(4, false)]   // 07:00 — too early
    [InlineData(6, false)]   // 09:00 — the window has passed
    [InlineData(21, false)]  // midnight in Addis
    public void SendsOnlyDuringTheAddisMorningHour(int utcHour, bool expected, int minute = 0)
    {
        BotDigestRules.IsSendingHour(Utc(utcHour, minute)).Should().Be(expected);
    }

    [Fact]
    public void TheLocalDayRollsOverThreeHoursBeforeUtcDoes()
    {
        // 22:00 UTC is already the next morning's date in Addis.
        BotDigestRules.LocalDate(new DateTime(2026, 10, 11, 22, 0, 0, DateTimeKind.Utc))
            .Should().Be(new DateOnly(2026, 10, 12));
    }

    [Fact]
    public void SaysNothingWhenThereIsNothingToSay()
    {
        BotDigestRules.WorthSending(0, 0, 0, 0).Should().BeFalse();
    }

    [Theory]
    [InlineData(1, 0, 0, 0)]
    [InlineData(0, 1, 0, 0)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(0, 0, 0, 1)]
    public void AnySingleThingIsWorthTheMessage(int registered, int overdue, int expiring, int cases)
    {
        BotDigestRules.WorthSending(registered, overdue, expiring, cases).Should().BeTrue();
    }

    [Fact]
    public void TheDigestNamesWhatItCounted()
    {
        var text = BotMessages.Digest(2, 3, 1, 0, amharic: false);
        text.Should().Contain("2").And.Contain("3").And.Contain("/overdue").And.Contain("/expiring");
        // Nothing open, so no line about open cases.
        text.Should().NotContain("open case");
    }
}
