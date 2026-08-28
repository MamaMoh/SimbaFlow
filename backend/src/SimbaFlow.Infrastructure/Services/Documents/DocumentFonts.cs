using QuestPDF.Drawing;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace SimbaFlow.Infrastructure.Services.Documents;

/// <summary>
/// Font chain for the generated documents.
///
/// QuestPDF bundles Lato, which covers Latin and nothing else. The contract and the visa form are
/// bilingual and candidate names are often Amharic, so Lato alone renders every Arabic and Ethiopic
/// character as an empty box — silently, because a missing glyph is not an error. The runtime image
/// installs Noto Arabic and Noto Ethiopic (see Dockerfile); this registers whatever is present and
/// exposes them as a fallback chain, so text is drawn by the first family that actually has the
/// glyph.
///
/// If a font is missing the chain simply gets shorter — generation still succeeds, which is why
/// <see cref="MissingScripts"/> is exposed for the startup check to report on rather than leaving
/// the gap invisible.
/// </summary>
public static class DocumentFonts
{
    private const string ArabicName = "SimbaFlowArabic";
    private const string EthiopicName = "SimbaFlowEthiopic";

    // Alpine (runtime image) first, then macOS and Debian for local development.
    private static readonly string[] ArabicPaths =
    [
        "/usr/share/fonts/noto/NotoNaskhArabic-Regular.ttf",
        "/usr/share/fonts/noto/NotoSansArabic-Regular.ttf",
        "/usr/share/fonts/truetype/noto/NotoNaskhArabic-Regular.ttf",
        "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
        "/Library/Fonts/Arial Unicode.ttf",
        "C:/Windows/Fonts/arialuni.ttf",
    ];

    private static readonly string[] EthiopicPaths =
    [
        "/usr/share/fonts/noto/NotoSansEthiopic-Regular.ttf",
        "/usr/share/fonts/truetype/noto/NotoSansEthiopic-Regular.ttf",
        "/System/Library/Fonts/Supplemental/Kefa.ttc",
        "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
        "/Library/Fonts/Arial Unicode.ttf",
        "C:/Windows/Fonts/ebrima.ttf",
    ];

    /// <summary>Families in fallback order — pass straight to TextStyle.FontFamily.</summary>
    public static string[] Chain { get; }

    /// <summary>Scripts with no font on this machine, for the startup diagnostic.</summary>
    public static IReadOnlyList<string> MissingScripts { get; }

    static DocumentFonts()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var chain = new List<string> { Fonts.Lato };
        var missing = new List<string>();

        if (TryRegister(ArabicName, ArabicPaths)) chain.Add(ArabicName);
        else missing.Add("Arabic");

        if (TryRegister(EthiopicName, EthiopicPaths)) chain.Add(EthiopicName);
        else missing.Add("Ethiopic");

        Chain = [.. chain];
        MissingScripts = missing;
    }

    private static bool TryRegister(string name, string[] paths)
    {
        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;
            try
            {
                using var stream = File.OpenRead(path);
                FontManager.RegisterFontWithCustomName(name, stream);
                return true;
            }
            catch
            {
                // Unreadable or an unsupported container (.ttc) — try the next candidate.
            }
        }

        return false;
    }
}
