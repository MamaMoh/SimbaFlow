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
    private const string CvArabicName = "SimbaFlowCvArabic";

    /// <summary>
    /// Scheherazade New, regular and bold, built into this assembly (Fonts/, SIL Open Font
    /// License). The CV's Arabic is set in it because it is the free face closest to the Times New
    /// Roman Arabic of the CVs partners already know: simple, upright Naskh. Bold is registered as
    /// well, so the white headings on the coloured bars are drawn by a real bold rather than a
    /// faked one.
    /// </summary>
    private static readonly string[] CvArabicResources =
    [
        "ScheherazadeNew-Regular.ttf",
        "ScheherazadeNew-Bold.ttf",
    ];

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

    /// <summary>
    /// The CV's chain: the same, with Scheherazade New ahead of the general Arabic font. Only the
    /// CV changes; the contract and the visa form keep <see cref="Chain"/>. If the built-in font
    /// cannot be read this is simply <see cref="Chain"/>, so a CV still renders.
    /// </summary>
    public static string[] CvChain { get; }

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

        CvChain = TryRegisterEmbedded(CvArabicName, CvArabicResources)
            ? [Fonts.Lato, CvArabicName, .. chain.Skip(1)]
            : Chain;
    }

    /// <summary>
    /// Forces the static constructor to run. The chain is only read inside the render lambda, which
    /// QuestPDF invokes *after* its licence check — so a generator that touches DocumentFonts only
    /// there gets the licence set too late and throws. Generators call this from their own static
    /// constructor instead.
    /// </summary>
    public static void EnsureInitialized() { }

    /// <summary>
    /// Registers every one of these built-in font files under one family name, so QuestPDF picks
    /// the regular or the bold by weight. True when at least the first, the regular, was read.
    /// </summary>
    private static bool TryRegisterEmbedded(string name, string[] files)
    {
        var assembly = typeof(DocumentFonts).Assembly;
        var registered = false;

        foreach (var file in files)
        {
            var resource = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("." + file, StringComparison.Ordinal));
            if (resource is null) continue;

            try
            {
                using var stream = assembly.GetManifestResourceStream(resource);
                if (stream is null) continue;
                FontManager.RegisterFontWithCustomName(name, stream);
                registered = true;
            }
            catch
            {
                // An unreadable bold still leaves the regular; an unreadable regular leaves Chain.
            }
        }

        return registered;
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
