using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// `/stats` argument parsing and period windows — these decide what number the owner sees on
/// their phone, so the boundaries are pinned.
/// </summary>
public class BotStatsRulesTests
{
    // Wednesday 2026-08-19 12:00 UTC
    private static readonly DateTime Now = new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(null, StatsPeriod.AllTime)]
    [InlineData("", StatsPeriod.AllTime)]
    [InlineData("  ", StatsPeriod.AllTime)]
    [InlineData("week", StatsPeriod.Week)]
    [InlineData(" WEEK ", StatsPeriod.Week)]
    [InlineData("w", StatsPeriod.Week)]
    [InlineData("month", StatsPeriod.Month)]
    [InlineData("this-month", StatsPeriod.Month)]
    [InlineData("year", StatsPeriod.Year)]
    [InlineData("all", StatsPeriod.AllTime)]
    public void ParseArgument_RecognizesPeriods(string? arg, StatsPeriod expected)
    {
        var (period, stage) = BotStatsRules.ParseArgument(arg);
        period.Should().Be(expected);
        stage.Should().BeNull();
    }

    [Theory]
    [InlineData("embassy")]
    [InlineData("New Contracts")]
    [InlineData("lmis")]
    public void ParseArgument_TreatsAnythingElseAsAStageName(string arg)
    {
        var (period, stage) = BotStatsRules.ParseArgument(arg);
        period.Should().BeNull();
        stage.Should().Be(arg.Trim());
    }

    [Fact]
    public void WeekStartsMonday()
    {
        // Wednesday → back to Monday the 17th.
        BotStatsRules.StartOf(StatsPeriod.Week, Now).Should().Be(new DateTime(2026, 8, 17));
    }

    [Fact]
    public void WeekOnAMonday_StartsThatSameDay()
    {
        var monday = new DateTime(2026, 8, 17, 9, 0, 0, DateTimeKind.Utc);
        BotStatsRules.StartOf(StatsPeriod.Week, monday).Should().Be(new DateTime(2026, 8, 17));
    }

    [Fact]
    public void WeekOnASunday_StartsPreviousMonday()
    {
        var sunday = new DateTime(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        BotStatsRules.StartOf(StatsPeriod.Week, sunday).Should().Be(new DateTime(2026, 8, 17));
    }

    [Fact]
    public void MonthAndYearStartAtTheFirst()
    {
        BotStatsRules.StartOf(StatsPeriod.Month, Now).Should().Be(new DateTime(2026, 8, 1));
        BotStatsRules.StartOf(StatsPeriod.Year, Now).Should().Be(new DateTime(2026, 1, 1));
    }

    [Fact]
    public void AllTimeHasNoLowerBound()
    {
        BotStatsRules.StartOf(StatsPeriod.AllTime, Now).Should().BeNull();
    }

    [Fact]
    public void StatsRequireAReportingPermission()
    {
        BotStatsRules.AllowedPermissions.Should().Contain("report.view");
        BotStatsRules.AllowedPermissions.Should().Contain("candidate.read");
        // Field-agent-only permissions must not unlock agency-wide numbers.
        BotStatsRules.AllowedPermissions.Should().NotContain("bot.use");
    }

    [Fact]
    public void PeriodLabels_AreLocalized()
    {
        BotStatsRules.PeriodLabel(StatsPeriod.Week, amharic: false).Should().Be("This week");
        BotStatsRules.PeriodLabel(StatsPeriod.Week, amharic: true).Should().NotBe("This week");
    }
}
