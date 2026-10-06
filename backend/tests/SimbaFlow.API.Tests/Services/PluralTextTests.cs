using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Counted nouns in text a person reads.
///
/// My work listed "49 day(s) in Embassy" and Compliance said "Passport expires in
/// 1 day(s)". The count was in hand both times, so the word could have agreed
/// with it; "(s)" is only ever a note that nobody wrote the branch.
/// </summary>
public class PluralTextTests
{
    [Theory]
    [InlineData(0, "0 days")]
    [InlineData(1, "1 day")]
    [InlineData(2, "2 days")]
    [InlineData(49, "49 days")]
    public void CountAgreesWithTheNumber(int count, string expected) =>
        PluralText.Count(count, "day").Should().Be(expected);

    [Fact]
    public void AnIrregularNounCanSupplyItsOwnPlural() =>
        PluralText.Count(2, "person", "people").Should().Be("2 people");

    [Fact]
    public void OneStillTakesTheSingularIrregular() =>
        PluralText.Count(1, "person", "people").Should().Be("1 person");

    [Fact]
    public void WordLeavesOutTheNumberForSentencesThatPlaceItThemselves() =>
        PluralText.Word(3, "day").Should().Be("days");

    /// <summary>A negative count reads as a quantity, so it takes the plural.</summary>
    [Fact]
    public void ANegativeCountIsPlural() =>
        PluralText.Count(-3, "day").Should().Be("-3 days");
}
