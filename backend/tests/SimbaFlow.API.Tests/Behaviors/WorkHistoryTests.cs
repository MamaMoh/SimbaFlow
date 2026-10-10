using FluentAssertions;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// The two work-experience rows on a CV, which have to line up.
///
/// Country and Period print side by side, so "Lebanon, Kuwait" against "2, 3" has to mean two
/// years in Lebanon and three in Kuwait. Anything that drops an entry from one list and not the
/// other pairs every later number with the wrong country.
/// </summary>
public class WorkHistoryTests
{
    private static Candidate With(params (string Country, int? Years)[] postings)
    {
        var candidate = new Candidate();
        var order = 0;
        foreach (var (country, years) in postings)
            candidate.WorkExperiences.Add(new CandidateWorkExperience
            {
                Country = country,
                Years = years,
                SortOrder = order++,
            });
        return candidate;
    }

    [Fact]
    public void TwoPostingsPrintAsTwoCommaSeparatedLists()
    {
        var candidate = With(("Lebanon", 2), ("Kuwait", 3));

        WorkHistory.Countries(candidate).Should().Be("Lebanon, Kuwait");
        WorkHistory.Years(candidate).Should().Be("2, 3");
    }

    [Fact]
    public void TheSameCountryTwiceIsPrintedTwice()
    {
        // The list page de-duplicates these; the CV must not. One "Saudi Arabia" against "2, 3"
        // leaves the reader paying the wrong number against the wrong posting.
        var candidate = With(("Saudi Arabia", 2), ("Saudi Arabia", 3));

        WorkHistory.Countries(candidate).Should().Be("Saudi Arabia, Saudi Arabia");
        WorkHistory.Years(candidate).Should().Be("2, 3");
    }

    [Fact]
    public void APostingWithNoTermHoldsItsPlace()
    {
        var candidate = With(("Lebanon", null), ("Kuwait", 3));

        WorkHistory.Countries(candidate).Should().Be("Lebanon, Kuwait");
        WorkHistory.Years(candidate).Should().Be("—, 3", "or the 3 would read as Lebanon's");
    }

    [Fact]
    public void ACandidateWhoHasNeverWorkedAbroadPrintsNothing()
    {
        // It used to fall back to the country they are being sent to, which printed a
        // first-time traveller as already experienced there.
        var candidate = new Candidate { CountryOfTravel = "Saudi Arabia" };

        WorkHistory.Countries(candidate).Should().BeEmpty();
        WorkHistory.Years(candidate).Should().BeEmpty();
        WorkHistory.HasAny(candidate).Should().BeFalse();
    }

    [Fact]
    public void ARowWithNoCountryIsNotAPosting()
    {
        var candidate = With(("Kuwait", 3), ("  ", 1));

        WorkHistory.Countries(candidate).Should().Be("Kuwait");
        WorkHistory.Years(candidate).Should().Be("3");
    }

    [Fact]
    public void ARecordFromBeforeTheListKeepsItsSingleValue()
    {
        var candidate = new Candidate { WorksIn = "Dubai", ExperienceAbroadYears = 4 };

        WorkHistory.Countries(candidate).Should().Be("Dubai");
        WorkHistory.Years(candidate).Should().Be("4");
        WorkHistory.HasAny(candidate).Should().BeTrue();
    }
}
