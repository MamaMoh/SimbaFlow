using FluentAssertions;
using FluentValidation.TestHelper;
using SimbaFlow.API.Features.Reports.Schedules;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// The email a scheduled report arrives as, and what the schedule form accepts.
/// </summary>
public class ScheduledReportEmailTests
{
    private static ReportTable Table(int rows, string firstName = "Embassy") => new(
        "pipeline", "Pipeline summary", "3 active candidates.",
        [
            new("stage", "Stage"),
            new("candidates", "Candidates", ReportColumnType.Number),
            new("share", "Share", ReportColumnType.Percent),
        ],
        Enumerable.Range(0, rows)
            .Select(i => new Dictionary<string, object?>
            {
                ["stage"] = i == 0 ? firstName : $"Stage {i}",
                ["candidates"] = 1200 + i,
                ["share"] = 12.5,
            })
            .ToList());

    [Fact]
    public void BodyShowsTheRowsFormattedLikeTheScreen()
    {
        var html = ScheduledReportEmail.HtmlBody(Table(1), "Laba Agency", "pipeline.xlsx", "every day");

        html.Should().Contain("Pipeline summary")
            .And.Contain("Laba Agency")
            .And.Contain("1,200")
            .And.Contain("12.5%")
            .And.Contain("pipeline.xlsx");
    }

    [Fact]
    public void CellTextIsEncoded()
    {
        var html = ScheduledReportEmail.HtmlBody(
            Table(1, "<script>alert(1)</script>"), "A & B", "f.xlsx", "every day");

        html.Should().NotContain("<script>")
            .And.Contain("&lt;script&gt;")
            .And.Contain("A &amp; B");
    }

    [Fact]
    public void LongReportsAreCutInTheBodyAndPointAtTheAttachment()
    {
        var html = ScheduledReportEmail.HtmlBody(Table(40), null, "f.xlsx", "every day");

        html.Should().Contain($"first {ScheduledReportEmail.BodyRowLimit} of 40 rows");
        html.Should().NotContain("Stage 30");
    }

    [Fact]
    public void EmptyReportSaysSo()
    {
        ScheduledReportEmail.HtmlBody(Table(0), null, "f.xlsx", "every day")
            .Should().Contain("Nothing to report");
    }

    private static CreateReportScheduleCommand Command(
        string frequency = "weekly", int? dayOfWeek = 1, int? dayOfMonth = null,
        string recipients = "ops@agency.et", string report = "pipeline", string format = "excel") =>
        new(report, format, frequency, 8, dayOfWeek, dayOfMonth, recipients);

    [Fact]
    public void AValidScheduleIsAccepted()
    {
        new CreateReportScheduleValidator().TestValidate(Command()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void WeeklyNeedsADayAndMonthlyNeedsADate()
    {
        var v = new CreateReportScheduleValidator();
        v.TestValidate(Command(dayOfWeek: null)).ShouldHaveValidationErrorFor(x => x.DayOfWeek);
        v.TestValidate(Command("monthly", null, 31)).ShouldHaveValidationErrorFor(x => x.DayOfMonth);
        v.TestValidate(Command("monthly", null, 28)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UnknownReportsFormatsAndAddressesAreRejected()
    {
        var v = new CreateReportScheduleValidator();
        v.TestValidate(Command(report: "salaries")).ShouldHaveValidationErrorFor(x => x.ReportKey);
        v.TestValidate(Command(format: "csv")).ShouldHaveValidationErrorFor(x => x.Format);
        v.TestValidate(Command(frequency: "hourly")).ShouldHaveValidationErrorFor(x => x.Frequency);
        v.TestValidate(Command(recipients: "ops@agency.et, typo")).ShouldHaveValidationErrorFor(x => x.Recipients);
        v.TestValidate(Command(recipients: "  ")).ShouldHaveValidationErrorFor(x => x.Recipients);
    }
}
