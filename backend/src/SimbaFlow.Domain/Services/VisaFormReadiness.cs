using SimbaFlow.Domain.Entities.Candidates;

namespace SimbaFlow.Domain.Services;

/// <summary>
/// Whether a candidate's enjaze form can be printed yet.
///
/// The form is handed to a consulate, and a consulate hands back one with holes in it. Before
/// this, the button was always live: the desk printed an enjaze the day the candidate was
/// registered, got a sheet with an empty visa number and no barcode where the E number goes, and
/// found out at the counter.
///
/// What is checked is the visa block — the things that are blank until somebody types them, and
/// that the form exists to carry. Not the biographical fields: those are asked for at intake, and
/// a missing religion is a form that is wrong in one line rather than a form that cannot be used.
/// Nationality and place of issue are not here either, because they are filled in from the
/// agency's own country when nobody has said otherwise.
///
/// The names are the ones the desk sees on their own screens, because the only use for this list
/// is telling someone what to go and fill in.
/// </summary>
public static class VisaFormReadiness
{
    public static IReadOnlyList<string> Missing(Candidate candidate) => Missing(
        candidate.VisaNumber,
        candidate.ENumber,
        candidate.SponsorName,
        candidate.SponsorIdNumber,
        candidate.PassportNumber,
        candidate.PassportIssueDate?.ToString(),
        candidate.PassportExpiryDate?.ToString());

    /// <summary>
    /// The same check against values read straight out of a projection, where the dates are
    /// already strings and there is no candidate to hand. Only emptiness is asked of them, so
    /// whatever format they are in does not matter.
    /// </summary>
    public static IReadOnlyList<string> Missing(
        string? visaNumber,
        string? eNumber,
        string? sponsorName,
        string? sponsorIdNumber,
        string? passportNumber,
        string? passportIssueDate,
        string? passportExpiryDate)
    {
        var missing = new List<string>();

        void Need(string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) missing.Add(label);
        }

        Need("Visa number", visaNumber);
        Need("E number", eNumber);
        Need("Sponsor name", sponsorName);
        Need("Sponsor ID", sponsorIdNumber);
        Need("Passport number", passportNumber);
        Need("Passport date of issue", passportIssueDate);
        Need("Passport expiry date", passportExpiryDate);

        return missing;
    }

    public static bool IsReady(Candidate candidate) => Missing(candidate).Count == 0;

    /// <summary>One sentence naming what is missing, for an error or a tooltip.</summary>
    public static string Explain(IReadOnlyList<string> missing) =>
        missing.Count == 0
            ? ""
            : $"The visa form needs {PluralText.List(missing)} before it can be printed.";
}
