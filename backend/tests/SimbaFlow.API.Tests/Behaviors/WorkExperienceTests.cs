using FluentAssertions;
using SimbaFlow.API.Features.Candidates;
using SimbaFlow.Domain.Entities.Candidates;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// A candidate's postings abroad, which there can be more than one of.
///
/// The two columns the rest of the system reads — WorksIn and ExperienceAbroadYears — are
/// derived from the list every time it is saved, so these cover the derivation as much as the
/// list itself: a CV printing three years for someone who worked five is the failure that
/// started this.
/// </summary>
public class WorkExperienceTests
{
    private static Candidate WithHistory(IReadOnlyList<CandidateWorkExperienceEntry> entries)
    {
        var candidate = new Candidate();
        CandidateIntakeMapper.SyncWorkExperiences(candidate, entries);
        return candidate;
    }

    private static IReadOnlyList<CandidateWorkExperience> Live(Candidate c) =>
        [.. c.WorkExperiences.Where(w => !w.IsDeleted).OrderBy(w => w.SortOrder)];

    [Fact]
    public void YearsAcrossCountriesAreAddedUpRatherThanReplacingEachOther()
    {
        var candidate = WithHistory([
            new("Lebanon", "House maid", 2),
            new("Kuwait", "Nanny", 3)]);

        candidate.ExperienceAbroadYears.Should().Be(5);
        candidate.WorksIn.Should().Be("Lebanon, Kuwait");
    }

    [Fact]
    public void TheSameCountryTwiceIsOneCountryButBothTerms()
    {
        // Two separate postings to Saudi Arabia is five years of experience in one country, and
        // the summary column should not read "Saudi Arabia, Saudi Arabia".
        var candidate = WithHistory([
            new("Saudi Arabia", "House maid", 2),
            new("Saudi Arabia", "Nanny", 3)]);

        candidate.WorksIn.Should().Be("Saudi Arabia");
        candidate.ExperienceAbroadYears.Should().Be(5);
        Live(candidate).Should().HaveCount(2);
    }

    [Fact]
    public void ACountryWithNoTermStillCounts()
    {
        var candidate = WithHistory([new("Dubai")]);

        candidate.WorksIn.Should().Be("Dubai");
        candidate.ExperienceAbroadYears.Should().BeNull("nobody said how long");
    }

    [Fact]
    public void RowsWithNoCountryAreNotRows()
    {
        // A line added and then left alone — the occupation typed, the country never picked.
        // Saving must not file a posting to nowhere.
        var candidate = WithHistory([new("Kuwait", "Nanny", 3), new("  ")]);

        Live(candidate).Should().ContainSingle();
    }

    [Fact]
    public void EditingACountryCorrectsTheRowRatherThanAddingOne()
    {
        var candidate = WithHistory([new("Kuwiat", "Nanny", 3)]);
        var original = Live(candidate).Single().Id;

        CandidateIntakeMapper.SyncWorkExperiences(candidate, [new("Kuwait", "Nanny", 3)]);

        Live(candidate).Should().ContainSingle()
            .Which.Id.Should().Be(original, "a typo fixed is the same posting");
        candidate.WorksIn.Should().Be("Kuwait");
    }

    [Fact]
    public void RemovingTheLastPostingClearsTheSummaryColumns()
    {
        var candidate = WithHistory([new("Lebanon", "House maid", 2)]);

        CandidateIntakeMapper.SyncWorkExperiences(candidate, []);

        Live(candidate).Should().BeEmpty();
        candidate.WorksIn.Should().BeNull();
        candidate.ExperienceAbroadYears.Should().BeNull();
    }

    [Fact]
    public void AShorterHistoryDoesNotLeaveTheDroppedRowsBehind()
    {
        var candidate = WithHistory([
            new("Lebanon", "House maid", 2),
            new("Kuwait", "Nanny", 3),
            new("Dubai", "Cleaner", 1)]);

        CandidateIntakeMapper.SyncWorkExperiences(candidate, [new("Lebanon", "House maid", 2)]);

        Live(candidate).Should().ContainSingle();
        candidate.ExperienceAbroadYears.Should().Be(2);
    }

    [Fact]
    public void ASaveThatSaysNothingAboutTheHistoryLeavesItAlone()
    {
        // Several screens save a candidate without ever showing the work-experience table. A
        // payload with no list must not be read as "the candidate has never worked abroad".
        var candidate = WithHistory([new("Kuwait", "Nanny", 3)]);

        CandidateIntakeMapper.Apply(candidate, new CandidateIntakePayload());

        Live(candidate).Should().ContainSingle();
        candidate.WorksIn.Should().Be("Kuwait");
        candidate.ExperienceAbroadYears.Should().Be(3);
    }
}
