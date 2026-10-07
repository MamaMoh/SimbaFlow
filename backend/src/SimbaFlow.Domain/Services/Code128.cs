namespace SimbaFlow.Domain.Services;

/// <summary>
/// Code 128 barcodes, as bar and space widths in modules.
///
/// The Saudi consular form carries the visa number and the passport number as barcodes, and the
/// embassy desk scans them rather than reading them — which is the whole point, because those are
/// the two numbers on the page that a human copies wrong. A form without them is retyped at the
/// counter.
///
/// Code 128 is what the official form uses. It was worth establishing rather than assuming: the
/// bars are drawn as vector rectangles with no text behind them, so the symbology had to be read
/// off the widths. Four distinct bar widths and eleven modules per symbol is Code 128; Code 39,
/// the other candidate, has two widths and would need half again as many bars for ten digits.
///
/// Character set B throughout — all of printable ASCII, so a passport number's letters and a visa
/// number's digits encode the same way with no mode switching to get wrong. Set C would pack
/// digit pairs into one symbol and make the digit-only barcode about half as wide, which is worth
/// nothing here: the header has the room, and a scanner reads both identically.
///
/// This returns widths, not a picture. What draws them knows about page units; this knows about
/// the symbology, and can be checked by decoding its own output.
/// </summary>
public static class Code128
{
    /// <summary>Bar and space widths, in modules, for each of the 107 symbols.</summary>
    private static readonly string[] Patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312",
        "132212", "221213", "221312", "231212", "112232", "122132", "122231", "113222",
        "123122", "123221", "223211", "221132", "221231", "213212", "223112", "312131",
        "311222", "321122", "321221", "312212", "322112", "322211", "212123", "212321",
        "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121",
        "313121", "211331", "231131", "213113", "213311", "213131", "311123", "311321",
        "331121", "312113", "312311", "332111", "314111", "221411", "431111", "111224",
        "111422", "121124", "121421", "141122", "141221", "112214", "112412", "122114",
        "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112",
        "421211", "212141", "214121", "412121", "111143", "111341", "131141", "114113",
        "114311", "411113", "411311", "113141", "114131", "311141", "411131", "211412",
        "211214", "211232", "2331112",
    ];

    private const int StartB = 104;
    private const int Stop = 106;

    /// <summary>
    /// Scanners need clear space either side or they read a truncated symbol. Ten modules is the
    /// specification's minimum.
    /// </summary>
    public const int QuietZone = 10;

    /// <summary>
    /// The widths of each bar and space, in modules, starting with a bar and alternating.
    /// The quiet zones are not included — whatever draws this has to leave them.
    ///
    /// Returns nothing for a value the symbology cannot carry, so a candidate with no visa number
    /// yet, or one typed with a character outside printable ASCII, prints a form without a
    /// barcode rather than a barcode that scans as something else.
    /// </summary>
    public static IReadOnlyList<int> Modules(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        var text = value.Trim();
        if (text.Any(c => c is < ' ' or > '~')) return [];

        var symbols = new List<int> { StartB };
        foreach (var ch in text) symbols.Add(ch - ' ');

        // The check symbol weights each character by its position, counting the start symbol as
        // position zero — so a transposition changes it.
        var checksum = StartB;
        for (var i = 1; i < symbols.Count; i++) checksum += symbols[i] * i;
        symbols.Add(checksum % 103);
        symbols.Add(Stop);

        var modules = new List<int>(symbols.Count * 6 + 1);
        foreach (var symbol in symbols)
            foreach (var width in Patterns[symbol])
                modules.Add(width - '0');

        return modules;
    }

    /// <summary>The symbol's total width in modules, quiet zones included.</summary>
    public static int Width(string? value)
    {
        var modules = Modules(value);
        return modules.Count == 0 ? 0 : modules.Sum() + QuietZone * 2;
    }
}
