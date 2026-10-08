using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Getting a name into the three boxes a form asks for.
///
/// The Tasheer sheet is rejected when a name does not match the passport, and the ways a name can
/// be written are the ways this can go wrong.
/// </summary>
public class PersonNameTests
{
    [Theory]
    [InlineData("ALMAZ DEBELA KEBEDE", "ALMAZ", "DEBELA", "KEBEDE")]
    [InlineData("ALMAZ KEBEDE", "ALMAZ", "", "KEBEDE")]
    [InlineData("ALMAZ", "ALMAZ", "", "")]
    public void TheNameGoesIntoThreeBoxes(string full, string first, string second, string last)
    {
        PersonName.Split(full).Should().Be(new NameParts(first, second, last));
    }

    [Fact]
    public void AFourthNameStaysInTheMiddle()
    {
        // Not every name is three words, and the sheet has three boxes. Dropping the extra would
        // put a name on the form that is not the one in the passport.
        PersonName.Split("ALMAZ DEBELA HAILE KEBEDE")
            .Should().Be(new NameParts("ALMAZ", "DEBELA HAILE", "KEBEDE"));
    }

    [Theory]
    [InlineData("  ALMAZ   DEBELA  KEBEDE  ")]
    public void TypedInSpacingDoesNotReachTheSheet(string full)
    {
        // Names arrive out of a text box, and a double space between two of them would otherwise
        // become an empty word and shift every part along by one.
        PersonName.Split(full).Should().Be(new NameParts("ALMAZ", "DEBELA", "KEBEDE"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoNameIsThreeEmptyBoxes(string? full)
    {
        PersonName.Split(full).Should().Be(new NameParts("", "", ""));
    }
}
