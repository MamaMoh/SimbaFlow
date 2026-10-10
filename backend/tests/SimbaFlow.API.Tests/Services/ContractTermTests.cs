using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// The contract length is typed as a number and printed as words.
///
/// It used to be free text, so the column already holds "2 Years", "2yrs" and "Two years" for the
/// same two-year contract. Those are printed back as they were signed rather than reworded, and
/// only a bare number is given its unit — a CV that said just "2" under Contract Period would be
/// read as two months as readily as two years.
/// </summary>
public class ContractTermTests
{
    [Theory]
    [InlineData("2", "2 Years")]
    [InlineData("1", "1 Year")]
    [InlineData(" 3 ", "3 Years")]
    public void ANumberIsPrintedWithItsUnit(string stored, string printed) =>
        ContractTerm.Label(stored).Should().Be(printed);

    [Theory]
    [InlineData("2 Years")]
    [InlineData("Two years")]
    [InlineData("24 months")]
    public void AnAnswerAlreadyInWordsIsLeftAlone(string stored) =>
        ContractTerm.Label(stored).Should().Be(stored);

    [Fact]
    public void NothingRecordedPrintsTheCallersStandIn()
    {
        ContractTerm.Label(null).Should().BeEmpty();
        ContractTerm.Label("   ").Should().BeEmpty();
        ContractTerm.Label(null, "Two years").Should().Be("Two years");
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData("2 Years", 2)]
    [InlineData("Two years", null)]
    [InlineData("0", null)]
    [InlineData("", null)]
    public void TheNumberOfYearsIsReadWhereTheAnswerStartsWithOne(string stored, int? years) =>
        ContractTerm.Years(stored).Should().Be(years);
}
