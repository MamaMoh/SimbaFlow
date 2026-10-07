using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Code 128 is 107 patterns of six numbers, and a single digit wrong anywhere in that table
/// produces a barcode that still looks like a barcode and scans as something else — or as
/// nothing, which is better but no easier to spot on a printed page.
///
/// So the encoder is checked by decoding its own output: <see cref="Decode"/> is written from the
/// symbology rather than from the encoder, and reads the widths back the way a scanner would.
/// </summary>
public class Code128Tests
{
    [Theory]
    [InlineData("1900111222")]
    [InlineData("EP0000001")]
    [InlineData("E00201748")]
    [InlineData("0")]
    [InlineData("SF-2026/0001")]
    [InlineData("abcXYZ 789")]
    public void WhatIsEncodedIsWhatScansBack(string value)
    {
        Decode(Code128.Modules(value)).Should().Be(value);
    }

    [Fact]
    public void TheSymbolOpensWithStartBAndClosesWithTheStopPattern()
    {
        var modules = Code128.Modules("12345");

        modules.Take(6).Should().Equal(2, 1, 1, 2, 1, 4);
        modules.TakeLast(7).Should().Equal(2, 3, 3, 1, 1, 1, 2);
    }

    [Fact]
    public void ATransposedDigitChangesTheCheckSymbol()
    {
        // The check symbol weights by position precisely so that this is true. Without it a
        // scanner reads one number off a label that shows its last two digits the other way
        // round, and nobody finds out.
        var straight = Code128.Modules("1900111225");
        var swapped = Code128.Modules("1900111252");

        straight.Should().NotEqual(swapped);
    }

    [Fact]
    public void EachSymbolIsElevenModulesWide()
    {
        // Eleven per symbol, thirteen modules for the stop. Five characters plus start and check
        // is seven symbols.
        Code128.Modules("12345").Sum().Should().Be(7 * 11 + 13);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ሰላም")]        // Ethiopic — a name typed into a number field
    [InlineData("١٩٠٨")]        // Arabic-Indic digits
    public void SomethingTheSymbologyCannotCarryIsNotDrawnAtAll(string? value)
    {
        // A barcode that silently drops the characters it cannot encode is worse than no barcode:
        // it scans, and what it scans is wrong.
        Code128.Modules(value).Should().BeEmpty();
        Code128.Width(value).Should().Be(0);
    }

    [Fact]
    public void TheQuietZonesAreCountedInTheWidthButNotInTheBars()
    {
        Code128.Width("12345").Should().Be(Code128.Modules("12345").Sum() + 20);
    }

    /// <summary>
    /// Reads bar and space widths back into text, the way a scanner does: split into symbols of
    /// six elements, match each against the pattern table, verify the check symbol, and map the
    /// values through character set B.
    /// </summary>
    private static string Decode(IReadOnlyList<int> modules)
    {
        modules.Count.Should().BeGreaterThan(0);

        // The stop pattern is seven elements rather than six, so it is taken off the end first;
        // everything before it divides evenly into symbols.
        string.Concat(modules.TakeLast(7)).Should().Be("2331112");
        var body = modules.Take(modules.Count - 7).ToList();
        (body.Count % 6).Should().Be(0);

        var symbols = new List<int>();
        for (var i = 0; i < body.Count; i += 6)
            symbols.Add(Symbol(string.Concat(body.Skip(i).Take(6))));

        symbols[0].Should().Be(104, "the symbol should open with Start B");

        var check = symbols[^1];
        var expected = symbols[0];
        for (var i = 1; i < symbols.Count - 1; i++) expected += symbols[i] * i;
        check.Should().Be(expected % 103, "the check symbol should match the data");

        return string.Concat(symbols[1..^1].Select(v => (char)(v + ' ')));
    }

    private static int Symbol(string pattern)
    {
        var index = Table.IndexOf(pattern);
        index.Should().BeGreaterThanOrEqualTo(0, $"'{pattern}' should be a Code 128 symbol");
        return index;
    }

    /// <summary>
    /// The pattern table, written out again from the specification rather than shared with the
    /// encoder — a table that agrees with itself proves nothing.
    /// </summary>
    private static readonly List<string> Table =
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
        "211214", "211232",
    ];
}
