using FluentAssertions;
using SimbaFlow.API.Features.Candidates.Queries;
using SimbaFlow.Application.Common.Filtering;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.API.Features.Exceptions.Queries;
using SimbaFlow.Domain.Entities.Travel;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// The filter row, from the query string to the rows it leaves behind.
///
/// Every name below is invented.
/// </summary>
public class TableFilterTests
{
    // ──── Reading the query string ────

    [Fact]
    public void Parse_NothingAtAll_IsNoFilters_TEST85()
    {
        TableFilters.Parse(null).Should().BeEmpty();
        TableFilters.Parse("").Should().BeEmpty();
        TableFilters.Parse("   ").Should().BeEmpty();
    }

    [Fact]
    public void Parse_Rubbish_IsDroppedRatherThanRefused_TEST85()
    {
        // A stale bookmark or a hand-edited URL is not worth a 400 in front of somebody trying
        // to look at a list. A dropped filter shows as a wider result, not an error.
        TableFilters.Parse("not json at all").Should().BeEmpty();
        TableFilters.Parse("{\"field\":\"age\"}").Should().BeEmpty();
        TableFilters.Parse("[{\"field\":\"age\",\"op\":\"wat\",\"value\":\"3\"}]").Should().BeEmpty();
        TableFilters.Parse("[{\"op\":\"eq\",\"value\":\"3\"}]").Should().BeEmpty();
    }

    [Fact]
    public void Parse_AnOperatorStillWaitingForItsValue_IsDropped_TEST85()
    {
        // The ordinary state of the box while somebody is still typing in it. It should widen
        // the list to everything rather than narrow it to nothing.
        TableFilters.Parse("[{\"field\":\"age\",\"op\":\"gt\"}]").Should().BeEmpty();
        TableFilters.Parse("[{\"field\":\"age\",\"op\":\"between\",\"value\":\"20\"}]").Should().BeEmpty();
    }

    [Fact]
    public void Parse_BlankNeedsNoValue_TEST85()
    {
        var parsed = TableFilters.Parse("[{\"field\":\"labourId\",\"op\":\"blank\"}]");
        parsed.Should().HaveCount(1);
        parsed[0].Operator.Should().Be(FilterOperator.Blank);
    }

    [Fact]
    public void Parse_IsCappedAgainstAHandWrittenUrl_TEST85()
    {
        var many = string.Join(",",
            Enumerable.Range(0, 100).Select(_ => "{\"field\":\"passportNumber\",\"op\":\"eq\",\"value\":\"x\"}"));
        TableFilters.Parse($"[{many}]").Count.Should().Be(TableFilters.MaxFilters);
    }

    // ──── What the whitelist lets through ────

    private static Candidate Person(
        string first, string last, string? middle = null,
        int ageYears = 30, string? labourId = null, string? occupation = null,
        int? experience = null, DateTime? registered = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new Candidate
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            MiddleName = middle,
            LastName = last,
            PassportNumber = "EX0000000",
            DateOfBirth = today.AddYears(-ageYears),
            LabourId = labourId,
            Occupation = occupation,
            ExperienceAbroadYears = experience,
            RegisteredAt = registered ?? DateTime.UtcNow,
        };
    }

    private static List<Candidate> Filter(IEnumerable<Candidate> people, string json)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return CandidateFilters.For(today)
            .Apply(people.AsQueryable(), TableFilters.Parse(json))
            .ToList();
    }

    [Fact]
    public void AFieldNobodyWhitelisted_NeverReachesTheDatabase_TEST86()
    {
        // The whole point of the whitelist: a field name arriving from a browser is matched
        // against a list this code wrote, never used to name a column.
        var people = new[] { Person("Hiwot", "Gebru"), Person("Munir", "Alemu") };

        Filter(people, "[{\"field\":\"PasswordHash\",\"op\":\"notblank\"}]").Should().HaveCount(2);
        Filter(people, "[{\"field\":\"1=1; DROP TABLE Candidates\",\"op\":\"notblank\"}]").Should().HaveCount(2);
        Filter(people, "[{\"field\":\"TenantId\",\"op\":\"notblank\"}]").Should().HaveCount(2);
    }

    [Fact]
    public void AValueThatWillNotParse_WidensRatherThanEmpties_TEST86()
    {
        var people = new[] { Person("Hiwot", "Gebru", ageYears: 24) };
        Filter(people, "[{\"field\":\"age\",\"op\":\"gt\",\"value\":\"banana\"}]").Should().HaveCount(1);
    }

    [Fact]
    public void Text_ContainsIsCaseInsensitive_TEST86()
    {
        var people = new[]
        {
            Person("Hiwot", "Gebru", occupation: "House Maid"),
            Person("Munir", "Alemu", occupation: "Driver"),
        };

        Filter(people, "[{\"field\":\"occupation\",\"op\":\"contains\",\"value\":\"maid\"}]")
            .Should().ContainSingle(c => c.FirstName == "Hiwot");
    }

    [Fact]
    public void Text_BlankCoversNothingRecordedAndAnEmptyBox_TEST86()
    {
        var people = new[]
        {
            Person("Hiwot", "Gebru", labourId: null),
            Person("Munir", "Alemu", labourId: ""),
            Person("Selam", "Bekele", labourId: "L-1"),
        };

        Filter(people, "[{\"field\":\"labourId\",\"op\":\"blank\"}]").Should().HaveCount(2);
        Filter(people, "[{\"field\":\"labourId\",\"op\":\"notblank\"}]")
            .Should().ContainSingle(c => c.FirstName == "Selam");
    }

    [Fact]
    public void Text_NotEqualsKeepsTheOnesWithNothingRecorded_TEST86()
    {
        // "not a driver" has to include somebody whose occupation nobody has filled in, or the
        // two halves of a question do not add up to the whole list.
        var people = new[]
        {
            Person("Hiwot", "Gebru", occupation: "Driver"),
            Person("Munir", "Alemu", occupation: null),
        };

        Filter(people, "[{\"field\":\"occupation\",\"op\":\"neq\",\"value\":\"Driver\"}]")
            .Should().ContainSingle(c => c.FirstName == "Munir");
    }

    [Fact]
    public void Name_MatchesTheWholeNameAsItIsWritten_TEST87()
    {
        // In neither FirstName nor LastName, which is why this column has its own handling.
        var people = new[]
        {
            Person("Hiwot", "Gebru", middle: "Tadesse"),
            Person("Munir", "Alemu"),
        };

        Filter(people, "[{\"field\":\"fullName\",\"op\":\"contains\",\"value\":\"hiwot tadesse\"}]")
            .Should().HaveCount(1);
        // And still matches for somebody with no middle name recorded.
        Filter(people, "[{\"field\":\"fullName\",\"op\":\"contains\",\"value\":\"munir alemu\"}]")
            .Should().HaveCount(1);
    }

    [Fact]
    public void Number_ComparesAndCountsTheOnesWithNothingRecordedAsNeither_TEST87()
    {
        var people = new[]
        {
            Person("Hiwot", "Gebru", experience: 2),
            Person("Munir", "Alemu", experience: 6),
            Person("Selam", "Bekele", experience: null),
        };

        Filter(people, "[{\"field\":\"experienceAbroadYears\",\"op\":\"gte\",\"value\":\"3\"}]")
            .Should().ContainSingle(c => c.FirstName == "Munir");
        Filter(people, "[{\"field\":\"experienceAbroadYears\",\"op\":\"between\",\"value\":\"1\",\"value2\":\"3\"}]")
            .Should().ContainSingle(c => c.FirstName == "Hiwot");
        Filter(people, "[{\"field\":\"experienceAbroadYears\",\"op\":\"blank\"}]")
            .Should().ContainSingle(c => c.FirstName == "Selam");
    }

    // ──── Age, which is years on the screen and a date of birth in the row ────

    [Fact]
    public void Age_EqualsIsTheWholeYearNotTheBirthday_TEST88()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var people = new[]
        {
            // 25 today, and 25 for another 364 days.
            new Candidate { FirstName = "Hiwot", LastName = "Gebru", PassportNumber = "EX1",
                            DateOfBirth = today.AddYears(-25) },
            new Candidate { FirstName = "Selam", LastName = "Bekele", PassportNumber = "EX2",
                            DateOfBirth = today.AddYears(-26).AddDays(2) },
            new Candidate { FirstName = "Munir", LastName = "Alemu", PassportNumber = "EX3",
                            DateOfBirth = today.AddYears(-26) },
        };

        var twentyFive = Filter(people, "[{\"field\":\"age\",\"op\":\"eq\",\"value\":\"25\"}]");
        twentyFive.Select(c => c.FirstName).Should().BeEquivalentTo(["Hiwot", "Selam"]);
    }

    [Fact]
    public void Age_RunsTheRightWayRound_TEST88()
    {
        // An older person has an earlier date of birth, so the comparison is inverted. Getting
        // this backwards is the mistake worth a test.
        var people = new[]
        {
            Person("Hiwot", "Gebru", ageYears: 22),
            Person("Munir", "Alemu", ageYears: 40),
        };

        Filter(people, "[{\"field\":\"age\",\"op\":\"gt\",\"value\":\"30\"}]")
            .Should().ContainSingle(c => c.FirstName == "Munir");
        Filter(people, "[{\"field\":\"age\",\"op\":\"lt\",\"value\":\"30\"}]")
            .Should().ContainSingle(c => c.FirstName == "Hiwot");
    }

    [Fact]
    public void Age_BetweenTakesTheRangeEitherWayRound_TEST88()
    {
        var people = new[]
        {
            Person("Hiwot", "Gebru", ageYears: 22),
            Person("Munir", "Alemu", ageYears: 31),
            Person("Selam", "Bekele", ageYears: 45),
        };

        Filter(people, "[{\"field\":\"age\",\"op\":\"between\",\"value\":\"20\",\"value2\":\"35\"}]")
            .Select(c => c.FirstName).Should().BeEquivalentTo(["Hiwot", "Munir"]);
        Filter(people, "[{\"field\":\"age\",\"op\":\"between\",\"value\":\"35\",\"value2\":\"20\"}]")
            .Select(c => c.FirstName).Should().BeEquivalentTo(["Hiwot", "Munir"]);
    }

    [Fact]
    public void Age_AStrayYearIsNotAnAge_TEST88()
    {
        // Somebody typing a year of birth into an age box. 1998 years ago is not a date this
        // should try to build.
        var people = new[] { Person("Hiwot", "Gebru", ageYears: 26) };
        Filter(people, "[{\"field\":\"age\",\"op\":\"gte\",\"value\":\"1998\"}]").Should().HaveCount(1);
    }

    [Fact]
    public void Timestamp_ComparesByTheDayNotTheMoment_TEST89()
    {
        // A registration is almost never at midnight, so comparing a date to a moment directly
        // makes "registered today" match nothing.
        var day = new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc);
        var people = new[]
        {
            Person("Hiwot", "Gebru", registered: day.AddHours(9).AddMinutes(41)),
            Person("Munir", "Alemu", registered: day.AddDays(1).AddHours(2)),
        };

        Filter(people, "[{\"field\":\"registeredAt\",\"op\":\"eq\",\"value\":\"2026-03-04\"}]")
            .Should().ContainSingle(c => c.FirstName == "Hiwot");
        Filter(people, "[{\"field\":\"registeredAt\",\"op\":\"lte\",\"value\":\"2026-03-04\"}]")
            .Should().ContainSingle(c => c.FirstName == "Hiwot");
        Filter(people, "[{\"field\":\"registeredAt\",\"op\":\"gte\",\"value\":\"2026-03-05\"}]")
            .Should().ContainSingle(c => c.FirstName == "Munir");
    }

    [Fact]
    public void SeveralFiltersNarrowTogether_TEST89()
    {
        var people = new[]
        {
            Person("Hiwot", "Gebru", ageYears: 24, occupation: "House Maid"),
            Person("Munir", "Alemu", ageYears: 24, occupation: "Driver"),
            Person("Selam", "Bekele", ageYears: 44, occupation: "House Maid"),
        };

        Filter(people,
            "[{\"field\":\"age\",\"op\":\"lt\",\"value\":\"30\"}," +
            "{\"field\":\"occupation\",\"op\":\"contains\",\"value\":\"maid\"}]")
            .Should().ContainSingle(c => c.FirstName == "Hiwot");
    }

    // ──── A column held as an enum ────

    private static List<ExceptionCase> FilterCases(IEnumerable<ExceptionCase> cases, string json) =>
        ExceptionFilters.For().Apply(cases.AsQueryable(), TableFilters.Parse(json)).ToList();

    private static ExceptionCase Case(ExceptionType type, ExceptionStatus status) =>
        new() { Id = Guid.NewGuid(), CandidateId = Guid.NewGuid(), Type = type, Status = status };

    [Fact]
    public void Enum_IsMatchedByTheNameTheBoardShows_TEST90()
    {
        var cases = new[]
        {
            Case(ExceptionType.Returned, ExceptionStatus.Open),
            Case(ExceptionType.Runaway, ExceptionStatus.Open),
        };

        FilterCases(cases, "[{\"field\":\"type\",\"op\":\"eq\",\"value\":\"runaway\"}]")
            .Should().ContainSingle(c => c.Type == ExceptionType.Runaway);
    }

    [Fact]
    public void Enum_HalfTheNameStillFindsIt_TEST90()
    {
        // Somebody typing "run" under Type should see the runaways before they finish the word.
        var cases = new[]
        {
            Case(ExceptionType.Returned, ExceptionStatus.Open),
            Case(ExceptionType.Runaway, ExceptionStatus.Open),
        };

        FilterCases(cases, "[{\"field\":\"type\",\"op\":\"startswith\",\"value\":\"run\"}]")
            .Should().ContainSingle(c => c.Type == ExceptionType.Runaway);
    }

    [Fact]
    public void Enum_NothingIsCalledThat_IsAnEmptyListNotEveryRow_TEST90()
    {
        var cases = new[] { Case(ExceptionType.Returned, ExceptionStatus.Open) };

        FilterCases(cases, "[{\"field\":\"status\",\"op\":\"eq\",\"value\":\"banana\"}]")
            .Should().BeEmpty();
        // And its negation is everything, which is the only consistent answer.
        FilterCases(cases, "[{\"field\":\"status\",\"op\":\"neq\",\"value\":\"banana\"}]")
            .Should().HaveCount(1);
    }

    [Fact]
    public void Enum_IsNeverBlank_TEST90()
    {
        // A column that is always one of a fixed set has no blank to ask about. Saying so beats
        // a funnel that silently matches nothing either way.
        var cases = new[] { Case(ExceptionType.Returned, ExceptionStatus.Open) };

        FilterCases(cases, "[{\"field\":\"type\",\"op\":\"blank\"}]").Should().BeEmpty();
        FilterCases(cases, "[{\"field\":\"type\",\"op\":\"notblank\"}]").Should().HaveCount(1);
    }

    [Fact]
    public void AJoinedBoardKnowsThePersonByItsOwnColumnName_TEST91()
    {
        // The commissions and exceptions boards head the column "Candidate", not "Name", and
        // the whitelist has to answer to what the board calls it or the funnel does nothing.
        var people = new[] { Person("Hiwot", "Gebru"), Person("Munir", "Alemu") };

        CandidateFilters.Joined()
            .Apply(people.AsQueryable(),
                TableFilters.Parse("[{\"field\":\"candidateName\",\"op\":\"contains\",\"value\":\"munir\"}]"))
            .Should().ContainSingle(c => c.FirstName == "Munir");

        // And the candidates board's own name for it is not on this one.
        CandidateFilters.Joined()
            .Apply(people.AsQueryable(),
                TableFilters.Parse("[{\"field\":\"fullName\",\"op\":\"contains\",\"value\":\"munir\"}]"))
            .Should().HaveCount(2);
    }

    [Fact]
    public void TheSameFilterListNarrowsBothHalvesOfAJoinedBoard_TEST91()
    {
        // One parsed list is offered to both whitelists; each ignores what it does not know.
        // That is what lets a filter on the person and a filter on the case apply together.
        var json = "[{\"field\":\"candidateName\",\"op\":\"contains\",\"value\":\"hiwot\"}," +
                   "{\"field\":\"type\",\"op\":\"eq\",\"value\":\"runaway\"}]";
        var parsed = TableFilters.Parse(json);

        var people = new[] { Person("Hiwot", "Gebru"), Person("Munir", "Alemu") };
        var cases = new[]
        {
            Case(ExceptionType.Returned, ExceptionStatus.Open),
            Case(ExceptionType.Runaway, ExceptionStatus.Open),
        };

        CandidateFilters.Joined().Apply(people.AsQueryable(), parsed).Should().HaveCount(1);
        ExceptionFilters.For().Apply(cases.AsQueryable(), parsed).Should().HaveCount(1);
    }
}
