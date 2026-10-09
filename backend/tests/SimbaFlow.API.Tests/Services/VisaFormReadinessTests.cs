using FluentAssertions;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// What has to be on the record before an enjaze can be printed.
///
/// The form used to be printable the day a candidate was registered, which produced a sheet with
/// an empty visa number and no barcode where the E number goes — found out at the consular
/// counter rather than at the desk.
/// </summary>
public class VisaFormReadinessTests
{
    private static Candidate Ready() => new()
    {
        FirstName = "ALMAZ",
        LastName = "KEBEDE",
        PassportNumber = "EP0000001",
        PassportIssueDate = new DateOnly(2024, 6, 8),
        PassportExpiryDate = new DateOnly(2029, 6, 7),
        DateOfBirth = new DateOnly(2002, 7, 9),
        VisaNumber = "1900111222",
        ENumber = "E000111222",
        SponsorName = "SAAD ABDULLAH MOHAMMED ALHARBI",
        SponsorIdNumber = "1099887766",
    };

    [Fact]
    public void AFullyFilledRecordIsReady()
    {
        VisaFormReadiness.Missing(Ready()).Should().BeEmpty();
        VisaFormReadiness.IsReady(Ready()).Should().BeTrue();
    }

    [Theory]
    [InlineData("Visa number")]
    [InlineData("E number")]
    [InlineData("Sponsor name")]
    [InlineData("Sponsor ID")]
    [InlineData("Passport date of issue")]
    [InlineData("Passport expiry date")]
    public void EachRequiredFieldIsNamedWhenItIsBlank(string label)
    {
        var candidate = Ready();
        switch (label)
        {
            case "Visa number": candidate.VisaNumber = null; break;
            case "E number": candidate.ENumber = null; break;
            case "Sponsor name": candidate.SponsorName = null; break;
            case "Sponsor ID": candidate.SponsorIdNumber = null; break;
            case "Passport date of issue": candidate.PassportIssueDate = null; break;
            case "Passport expiry date": candidate.PassportExpiryDate = null; break;
        }

        VisaFormReadiness.Missing(candidate).Should().Equal(label);
    }

    [Fact]
    public void AFieldTypedAsSpacesCountsAsBlank()
    {
        // A field cleared by selecting it and pressing space is empty to a reader and not to a
        // null check, and it prints as an empty box either way.
        var candidate = Ready();
        candidate.ENumber = "   ";

        VisaFormReadiness.Missing(candidate).Should().Equal("E number");
    }

    [Fact]
    public void ABiographicalGapDoesNotStopTheForm()
    {
        // Religion and marital status are asked for at intake and print as one blank line. A
        // missing one is a form that is wrong in a line, not a form that cannot be used — and
        // blocking on them would stop the desk printing anything.
        var candidate = Ready();
        candidate.Religion = null;
        candidate.MaritalStatus = null;
        candidate.PlaceOfBirth = null;
        candidate.Occupation = null;

        VisaFormReadiness.IsReady(candidate).Should().BeTrue();
    }

    [Fact]
    public void TheExplanationNamesEverythingThatIsMissing()
    {
        var candidate = Ready();
        candidate.VisaNumber = null;
        candidate.ENumber = null;
        candidate.SponsorIdNumber = null;

        VisaFormReadiness.Explain(VisaFormReadiness.Missing(candidate))
            .Should().Be("The visa form needs Visa number, E number and Sponsor ID before it can be printed.");
    }

    [Fact]
    public void AReadyRecordExplainsNothing()
    {
        VisaFormReadiness.Explain(VisaFormReadiness.Missing(Ready())).Should().BeEmpty();
    }
}
