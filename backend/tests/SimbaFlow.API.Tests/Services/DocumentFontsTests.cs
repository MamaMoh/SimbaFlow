using FluentAssertions;
using QuestPDF.Helpers;
using SimbaFlow.Infrastructure.Services.Documents;
using Xunit;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// The generated contract and visa form are bilingual, and candidate names are often Amharic. A
/// missing glyph is not an error in QuestPDF — it draws an empty box — so a font gap ships silently
/// and is only visible by looking at a rendered PDF. These assert the chain is wired up.
/// </summary>
public class DocumentFontsTests
{
    [Fact]
    public void LatinIsAlwaysFirstInTheChain()
    {
        // Lato ships with QuestPDF, so the chain is never empty however bare the host is.
        DocumentFonts.Chain.Should().NotBeEmpty();
        DocumentFonts.Chain[0].Should().Be(Fonts.Lato);
    }

    [Fact]
    public void ChainHasNoDuplicatesSoFallbackTerminates()
    {
        DocumentFonts.Chain.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void EveryMissingScriptIsReportedRatherThanSilentlyDropped()
    {
        // A host without the Noto fonts still generates documents; it must say which scripts will
        // render as empty boxes instead of leaving that invisible.
        var expected = 3 - DocumentFonts.Chain.Length;
        DocumentFonts.MissingScripts.Should().HaveCount(expected);
    }

    [Fact]
    public void TheCvSetsItsArabicInTheBuiltInFont()
    {
        // Scheherazade New is built into the assembly rather than installed, so unlike the Noto
        // fonts it is there on every host. If it ever stops being read, CvChain quietly falls back
        // to Chain and the CV changes typeface without anyone deciding it should.
        DocumentFonts.CvChain[0].Should().Be(Fonts.Lato);
        DocumentFonts.CvChain.Should().Contain("SimbaFlowCvArabic");
        DocumentFonts.CvChain.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void TheCvKeepsEveryOtherScriptTheDocumentsHave()
    {
        // The CV's own Arabic goes in front; Ethiopic and the general Arabic stay behind it for
        // anything Scheherazade does not draw, such as an Amharic name.
        DocumentFonts.CvChain.Should().Contain(DocumentFonts.Chain);
    }
}
