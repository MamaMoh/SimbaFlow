using FluentAssertions;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// The address line on a CV, which is where the candidate lives.
///
/// Choosing a partner agency fills the candidate's Country in from the partner's, so a record
/// with no address printed the destination under Address — read by whoever received the CV as
/// the applicant's home town.
/// </summary>
public class CvAddressTests
{
    [Fact]
    public void TheDestinationIsNotAnAddress()
    {
        var candidate = new Candidate { Country = "Saudi Arabia", CountryOfTravel = "Saudi Arabia" };

        HomeAddress.Format(candidate).Should().BeNull();
    }

    [Fact]
    public void ARealAddressStillPrintsWithoutTheDestination()
    {
        var candidate = new Candidate
        {
            City = "Addis Ababa",
            Subcity = "Bole",
            Country = "Saudi Arabia",
            CountryOfTravel = "Saudi Arabia",
        };

        HomeAddress.Format(candidate).Should().Be("Bole, Addis Ababa");
    }

    [Fact]
    public void AHomeCountryThatIsNotTheDestinationIsPartOfTheAddress()
    {
        var candidate = new Candidate
        {
            City = "Addis Ababa",
            Country = "Ethiopia",
            CountryOfTravel = "Saudi Arabia",
        };

        HomeAddress.Format(candidate).Should().Be("Addis Ababa, Ethiopia");
    }
}
