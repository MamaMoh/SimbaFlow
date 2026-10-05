using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Finding a candidate by the name someone actually types.
///
/// "/status ETENESH ACHALU TOLESA" answered "Candidate not found" for a candidate on file. The
/// query matched FirstName + " " + LastName, which drops the middle name — and most Ethiopian
/// names have three parts — and it was case-sensitive on top of that.
/// </summary>
public class BotCandidateSearchTests
{
    private const string First = "ETENESH";
    private const string Middle = "ACHALU";
    private const string Last = "TOLESA";

    private static bool Matches(string query) =>
        BotCandidateSearch.NameMatches(First, Middle, Last, null, query);

    [Fact]
    public void TheWholeThreePartNameMatches()
    {
        // The case from the chat screenshot.
        Matches("ETENESH ACHALU TOLESA").Should().BeTrue();
    }

    [Theory]
    [InlineData("etenesh achalu tolesa")]
    [InlineData("Etenesh Achalu Tolesa")]
    [InlineData("eTeNeSh")]
    public void CaseDoesNotMatter(string query)
    {
        // Postgres LIKE is case-sensitive, so the old predicate found nobody typing lower case.
        Matches(query).Should().BeTrue();
    }

    [Theory]
    [InlineData("ETENESH TOLESA")]   // skipping the middle name, as people do
    [InlineData("TOLESA ETENESH")]   // family name first
    [InlineData("achalu")]           // just the middle name
    [InlineData("  etenesh   tolesa  ")]
    public void PartialAndReorderedNamesMatch(string query)
    {
        Matches(query).Should().BeTrue();
    }

    [Theory]
    [InlineData("ALMAZ")]
    [InlineData("ETENESH KEBEDE")]  // one term right, one wrong — not this person
    public void SomebodyElseDoesNotMatch(string query)
    {
        Matches(query).Should().BeFalse();
    }

    [Fact]
    public void TheLocalScriptNameIsSearchableToo()
    {
        BotCandidateSearch.NameMatches("ETENESH", null, "TOLESA", "እتенеш ቶለሳ", "ቶለሳ")
            .Should().BeTrue();
    }

    [Theory]
    [InlineData("EQ1030621", true)]
    [InlineData("EP7798713", true)]
    [InlineData("ETENESH ACHALU TOLESA", false)]
    [InlineData("Almaz", false)]
    [InlineData("EQ", false)]              // too short to be a passport
    [InlineData("EQ-103/0621", false)]     // punctuation is not a passport number
    public void PassportsAreToldApartFromNames(string query, bool isPassport)
    {
        // A passport is matched exactly; treating one as a name would return everybody whose name
        // happens to contain those letters.
        BotCandidateSearch.LooksLikePassport(query).Should().Be(isPassport);
    }

    [Fact]
    public void SingleLettersAreIgnoredSoOneKeystrokeDoesNotMatchEverybody()
    {
        BotCandidateSearch.NameTerms("a b etenesh").Should().BeEquivalentTo(["etenesh"]);
    }

    [Fact]
    public void AnEmptyQueryMatchesNobody()
    {
        Matches("").Should().BeFalse();
        Matches("   ").Should().BeFalse();
    }
}
