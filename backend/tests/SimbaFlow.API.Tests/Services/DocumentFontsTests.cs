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
}
