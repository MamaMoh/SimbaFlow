using FluentAssertions;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// A scheduled report goes out at the hour the agency picked, in Addis time, and the next run is
/// always in the future — a run that lands on its own hour must not find itself due again.
/// </summary>
public class ReportScheduleRulesTests
{
    // Thursday 15 October 2026.
    private static DateTime Utc(int day, int hour, int minute = 0) =>
        new(2026, 10, day, hour, minute, 0, DateTimeKind.Utc);

    [Fact]
    public void DailyGoesOutLaterTodayWhenTheHourIsStillAhead()
    {
        // 04:00 UTC is 07:00 in Addis; 08:00 Addis is 05:00 UTC.
        ReportScheduleRules.NextRunUtc(ReportScheduleFrequency.Daily, 8, null, null, Utc(15, 4))
            .Should().Be(Utc(15, 5));
    }

    [Fact]
    public void DailyMovesToTomorrowOnceTheHourHasCome()
    {
        ReportScheduleRules.NextRunUtc(ReportScheduleFrequency.Daily, 8, null, null, Utc(15, 5))
            .Should().Be(Utc(16, 5));
    }

    [Fact]
    public void TheAddisDayStartsThreeHoursBeforeUtc()
    {
        // 22:00 UTC on the 15th is 01:00 on the 16th in Addis; the 08:00 run that day is 05:00 UTC.
        ReportScheduleRules.NextRunUtc(ReportScheduleFrequency.Daily, 8, null, null, Utc(15, 22))
            .Should().Be(Utc(16, 5));

        // Midnight in Addis is 21:00 UTC the day before.
        ReportScheduleRules.NextRunUtc(ReportScheduleFrequency.Daily, 0, null, null, Utc(15, 12))
            .Should().Be(Utc(15, 21));
    }

    [Fact]
    public void WeeklyWaitsForItsDay()
    {
        // Thursday the 15th → the following Monday is the 19th.
        ReportScheduleRules.NextRunUtc(ReportScheduleFrequency.Weekly, 9, DayOfWeek.Monday, null, Utc(15, 10))
            .Should().Be(Utc(19, 6));
    }

    [Fact]
    public void WeeklyOnTodayAfterTheHourIsNextWeek()
    {
        ReportScheduleRules.NextRunUtc(ReportScheduleFrequency.Weekly, 8, DayOfWeek.Thursday, null, Utc(15, 10))
            .Should().Be(Utc(22, 5));
    }

    [Fact]
    public void MonthlyRollsIntoNextMonth()
    {
        ReportScheduleRules.NextRunUtc(ReportScheduleFrequency.Monthly, 8, null, 1, Utc(15, 10))
            .Should().Be(new DateTime(2026, 11, 1, 5, 0, 0, DateTimeKind.Utc));

        ReportScheduleRules.NextRunUtc(ReportScheduleFrequency.Monthly, 8, null, 20, Utc(15, 10))
            .Should().Be(Utc(20, 5));
    }

    [Fact]
    public void MonthlyAcrossTheYearEnd()
    {
        ReportScheduleRules.NextRunUtc(
                ReportScheduleFrequency.Monthly, 8, null, 5,
                new DateTime(2026, 12, 20, 0, 0, 0, DateTimeKind.Utc))
            .Should().Be(new DateTime(2027, 1, 5, 5, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void RecipientsAreSplitTrimmedAndDeduplicated()
    {
        var (valid, invalid) = ReportScheduleRules.ParseRecipients(
            " a@agency.et, b@agency.et;\nA@agency.et  c@agency.et ");

        valid.Should().Equal("a@agency.et", "b@agency.et", "c@agency.et");
        invalid.Should().BeEmpty();
    }

    [Fact]
    public void MistypedAddressesAreReportedNotDropped()
    {
        var (valid, invalid) = ReportScheduleRules.ParseRecipients("ok@agency.et, nope, x@localhost");

        valid.Should().Equal("ok@agency.et");
        invalid.Should().Equal("nope", "x@localhost");
    }

    [Fact]
    public void DescribesTheScheduleInWords()
    {
        ReportScheduleRules.Describe(ReportScheduleFrequency.Weekly, 8, DayOfWeek.Monday, null)
            .Should().Be("every Monday at 08:00 (Addis time)");
        ReportScheduleRules.Describe(ReportScheduleFrequency.Monthly, 17, null, 1)
            .Should().Be("on day 1 of every month at 17:00 (Addis time)");
    }
}
