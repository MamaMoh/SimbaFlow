using System.Text.RegularExpressions;

namespace SimbaFlow.Domain.Services;

/// <summary>What a Saudi standard employment contract says about the placement.</summary>
/// <param name="SponsorIsCompany">
/// Whether the employer is a recruiting company rather than a household. The form is laid out
/// differently for each, and the identifier means a different thing — see the parser.
/// </param>
public sealed record SaudiContractDetails(
    string? ContractNumber = null,
    string? VisaNumber = null,
    string? SponsorName = null,
    string? SponsorIdNumber = null,
    string? SponsorPhone = null,
    string? SponsorAddress = null,
    bool SponsorIsCompany = false)
{
    /// <summary>Nothing was recognised — the file was not one of these contracts.</summary>
    public bool IsEmpty =>
        ContractNumber is null && VisaNumber is null && SponsorName is null &&
        SponsorIdNumber is null && SponsorPhone is null && SponsorAddress is null;
}

/// <summary>
/// Reads the Saudi standard employment contract for Ethiopian domestic workers.
///
/// The desk was retyping the contract number, the visa number and the sponsor's details out of a
/// PDF that already contains them, for every candidate. Those are long digit strings being copied
/// by eye into the record the visa file is built from — the one place a transposed digit is both
/// easy and expensive.
///
/// The form comes in two shapes and they are not interchangeable:
///
///   • A household employs the worker. Section A is the person — name, national ID, their own
///     address and mobile — and the Saudi recruiting agency appears below them as their
///     representative.
///   • A company employs the worker. There is no person; section A *is* the recruiting agency,
///     and what sits where the national ID was is the agency's licence number.
///
/// So "the first Name: after A." is the sponsor in both, but the identifier beneath it means a
/// national ID in one and a licence in the other, which is why <see cref="SaudiContractDetails"/>
/// says which shape it read rather than leaving the caller to guess from the digits.
///
/// This takes text, not a file. Pulling the English column out of a two-column bilingual PDF is a
/// separate problem with its own failure modes, and keeping it out of here is what lets the shapes
/// above be tested without a PDF.
/// </summary>
public static class SaudiContractParser
{
    private static readonly Regex ContractNumber =
        new(@"CONTRACT\s*#\s*([0-9]{4,})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex VisaNumber =
        new(@"VISA\s*NUMBER\s*#\s*([0-9]{4,})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>The heading that opens the agency's own block, below an individual employer.</summary>
    private const string AgencyHeading = "Represented in the Kingdom of Saudi";

    public static SaudiContractDetails Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new SaudiContractDetails();

        var lines = text
            .Split('\n')
            .Select(l => Regex.Replace(l, @"\s+", " ").Trim())
            .Where(l => l.Length > 0)
            .ToList();

        var joined = string.Join("\n", lines);
        var contract = ContractNumber.Match(joined);
        var visa = VisaNumber.Match(joined);

        var block = SponsorBlock(lines, out var isCompany);

        return new SaudiContractDetails(
            ContractNumber: contract.Success ? contract.Groups[1].Value : null,
            VisaNumber: visa.Success ? visa.Groups[1].Value : null,
            SponsorName: Field(block, "Name"),
            SponsorIdNumber: isCompany
                ? Field(block, "License no")
                : Field(block, "National ID Number"),
            SponsorPhone: Phone(block),
            SponsorAddress: Address(block),
            SponsorIsCompany: isCompany);
    }

    /// <summary>
    /// The lines describing the employer, and which of the two shapes this contract is.
    ///
    /// Section A opens both, but on the company form its heading *is* the agency heading, so the
    /// shape is decided by that rather than by anything further down. The block then runs to the
    /// agency's own block on the individual form, and to section B on the company one — taking the
    /// whole of section A on an individual form would swallow the agency's licence and telephone
    /// and report them as the sponsor's.
    /// </summary>
    private static List<string> SponsorBlock(List<string> lines, out bool isCompany)
    {
        isCompany = false;

        var start = lines.FindIndex(l => Regex.IsMatch(l, @"^A\s*[\.\)]"));
        if (start < 0) return [];

        // The heading can wrap across two or three lines, so look at section A's opening lines
        // rather than only the one carrying the "A.".
        var opening = string.Join(" ", lines.Skip(start).Take(4));
        isCompany = opening.Contains(AgencyHeading, StringComparison.OrdinalIgnoreCase);

        var end = isCompany
            ? lines.FindIndex(start + 1, l => Regex.IsMatch(l, @"^B\s*[\.\)]"))
            : lines.FindIndex(start + 1,
                l => l.Contains(AgencyHeading, StringComparison.OrdinalIgnoreCase)
                     || l.Contains("Hereinafter called the Employer", StringComparison.OrdinalIgnoreCase));

        if (end < 0) end = lines.Count;
        return lines.GetRange(start, end - start);
    }

    /// <summary>
    /// The value of "<paramref name="label"/>:", including any lines it wraps onto.
    ///
    /// A long name runs onto the next line with nothing to mark it as a continuation, so anything
    /// that is not itself a labelled line belongs to the value above it. Without that, a sponsor
    /// called SAAD ABDULLAH MOHAMMED ALHARBI is filed as SAAD ABDULLAH MOHAMMED.
    /// </summary>
    private static string? Field(List<string> block, string label)
    {
        var prefix = label + ":";
        var at = block.FindIndex(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (at < 0) return null;

        var parts = new List<string> { block[at][prefix.Length..].Trim() };

        for (var i = at + 1; i < block.Count; i++)
        {
            var next = block[i];
            if (next.Contains(':') || IsHeading(next)) break;
            parts.Add(next);
        }

        var value = string.Join(" ", parts.Where(p => p.Length > 0)).Trim();
        return value.Length == 0 ? null : value;
    }

    /// <summary>An unlabelled line that opens a group rather than continuing a value.</summary>
    private static bool IsHeading(string line) =>
        line.Equals("Address", StringComparison.OrdinalIgnoreCase) ||
        line.Equals("Contact Numbers", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The number to call the sponsor on.
    ///
    /// Mobile first: the household form carries both and fills Telephone with a literal "0" when
    /// there isn't one, which is worse than blank because it looks like an answer.
    /// </summary>
    private static string? Phone(List<string> block)
    {
        foreach (var label in new[] { "Mobile", "Telephone", "Contact No" })
        {
            var value = Field(block, label);
            if (value is null) continue;

            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (digits.Length >= 7) return value;
        }

        return null;
    }

    private static string? Address(List<string> block)
    {
        var parts = new[] { Field(block, "Street"), Field(block, "City") }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        var joined = string.Join(", ", parts);
        return joined.Length == 0 ? null : joined;
    }
}
