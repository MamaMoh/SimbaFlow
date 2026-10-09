using System.Text.RegularExpressions;

namespace SimbaFlow.Domain.Services;

/// <summary>
/// Deciding when two partner agencies are the same agency.
///
/// The catalog is shared across every agency on the platform, and the same foreign recruiter gets
/// entered by each of them in turn — so it filled up with "Example Recruitment", "EXAMPLE
/// RECRUITMENT" and "Example  Recruitment" as three separate partners. Candidates then get linked
/// to whichever copy the person in front of them happened to pick, and the partner's own
/// letterhead is on one of the three.
///
/// Sameness is the name and the address together: one recruiter can have branches, and two
/// branches of the same company at different addresses are two partners to place candidates
/// through. Two rows with the same name and the same address are one partner typed twice.
///
/// What is ignored is only how it was typed — case, leading and trailing space, and runs of
/// whitespace inside. Nothing cleverer: stripping punctuation or dropping words like "Est." and
/// "Co." would start merging agencies that really are different, and a catalog that silently
/// refuses a real partner is worse than one with a duplicate in it.
/// </summary>
public static class PartnerIdentity
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>The comparable form of one field.</summary>
    public static string Normalise(string? value) =>
        value is null ? "" : Whitespace.Replace(value.Trim(), " ").ToUpperInvariant();

    /// <summary>Whether these two are the same partner typed twice.</summary>
    public static bool SameAs(string? name, string? address, string? otherName, string? otherAddress) =>
        Normalise(name) == Normalise(otherName) && Normalise(address) == Normalise(otherAddress);
}
