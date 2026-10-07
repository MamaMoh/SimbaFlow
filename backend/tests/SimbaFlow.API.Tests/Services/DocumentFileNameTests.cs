using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// What a generated document is called when it is downloaded.
///
/// The rules here are about what a person sees in their Downloads folder, so they are worth
/// pinning: the whole reason the names changed is that twenty files called cv_{guid}.pdf are
/// indistinguishable, and a rule that quietly drops the name would put us back there.
/// </summary>
public class DocumentFileNameTests
{
    [Fact]
    public void ADocumentIsNamedAfterTheCandidateAndWhatItIs()
    {
        DocumentFileName.For("Zinet Yibre Abebaw", "CV")
            .Should().Be("Zinet Yibre Abebaw - CV.pdf");
    }

    [Fact]
    public void AnAmharicNameIsKeptRatherThanTransliterated()
    {
        // Content-Disposition carries UTF-8 by RFC 6266, so there is nothing to protect here from.
        DocumentFileName.For("ዘይነት ይብሬ አበባው", "Contract")
            .Should().Be("ዘይነት ይብሬ አበባው - Contract.pdf");
    }

    [Theory]
    [InlineData("Ann/Marie O'Neill")]
    [InlineData("Ann\\Marie O'Neill")]
    [InlineData("Ann:Marie O'Neill")]
    [InlineData("Ann*Marie O'Neill")]
    [InlineData("Ann?Marie O'Neill")]
    [InlineData("Ann\"Marie O'Neill")]
    [InlineData("Ann<Marie O'Neill")]
    [InlineData("Ann>Marie O'Neill")]
    [InlineData("Ann|Marie O'Neill")]
    public void ACharacterThatWouldBreakAPathIsDropped(string name)
    {
        DocumentFileName.For(name, "CV").Should().Be("AnnMarie O'Neill - CV.pdf");
    }

    [Fact]
    public void ANewlineInTheNameCannotSplitTheHeader()
    {
        DocumentFileName.For("Zinet\r\nAbebaw", "CV").Should().Be("Zinet Abebaw - CV.pdf");
    }

    [Fact]
    public void StrayWhitespaceDoesNotChangeTheName()
    {
        DocumentFileName.For("  Zinet   Yibre  Abebaw  ", "CV")
            .Should().Be("Zinet Yibre Abebaw - CV.pdf");
    }

    [Fact]
    public void ATrailingDotIsRemovedBecauseWindowsRefusesIt()
    {
        DocumentFileName.For("Zinet Abebaw Jr.", "CV").Should().Be("Zinet Abebaw Jr - CV.pdf");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("///")]
    public void WithoutANameTheDocumentIsStillNamedAfterWhatItIs(string? name)
    {
        // A candidate with no usable name is a data problem, not a reason to produce "- CV.pdf".
        DocumentFileName.For(name, "CV").Should().Be("CV.pdf");
    }

    [Fact]
    public void AnAbsurdlyLongNameIsCutRatherThanRefused()
    {
        var name = new string('a', 500);

        var result = DocumentFileName.For(name, "CV");

        result.Should().EndWith(" - CV.pdf");
        result.Length.Should().BeLessThan(120);
    }
}
