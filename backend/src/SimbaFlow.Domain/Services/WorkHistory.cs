using SimbaFlow.Domain.Entities.Candidates;

namespace SimbaFlow.Domain.Services;

/// <summary>
/// A candidate's postings abroad, as the printed forms want them.
///
/// The CV has one Country row and one Period row, and a candidate can have worked in three
/// places. So the two rows are lists that line up by position: "Lebanon, Kuwait" against
/// "2, 3" reads as two years in Lebanon and three in Kuwait, which is how anyone reads two
/// columns side by side.
///
/// Positional, which is why the countries are not de-duplicated here even though
/// <see cref="Candidate.WorksIn"/> does de-duplicate them for the list page. Two separate
/// postings to Saudi Arabia printed as one country against "2, 3" would have the reader pairing
/// the wrong numbers with the wrong places.
///
/// Nothing is invented. A candidate who has never worked abroad gets an empty string and the
/// form leaves the row blank — it used to fall back to the country they are being *sent* to,
/// which printed a first-time traveller as already experienced there.
/// </summary>
public static class WorkHistory
{
    public static IReadOnlyList<CandidateWorkExperience> Postings(Candidate candidate) =>
        [.. candidate.WorkExperiences
            .Where(w => !w.IsDeleted && !string.IsNullOrWhiteSpace(w.Country))
            .OrderBy(w => w.SortOrder)];

    /// <summary>Where they have worked, one entry per posting, in order.</summary>
    public static string Countries(Candidate candidate)
    {
        var postings = Postings(candidate);
        return postings.Count > 0
            ? string.Join(", ", postings.Select(p => p.Country.Trim()))
            // Records that predate the list keep their single value.
            : candidate.WorksIn?.Trim() ?? "";
    }

    /// <summary>
    /// How long each posting lasted, lined up with <see cref="Countries"/>.
    ///
    /// A posting whose term nobody recorded prints as a dash rather than being skipped: dropping
    /// it would shorten the list and shift every later number onto the wrong country.
    /// </summary>
    public static string Years(Candidate candidate)
    {
        var postings = Postings(candidate);
        if (postings.Count == 0)
            return candidate.ExperienceAbroadYears?.ToString() ?? "";

        return postings.Any(p => p.Years is not null)
            ? string.Join(", ", postings.Select(p => p.Years?.ToString() ?? "—"))
            : "";
    }

    /// <summary>Whether they have worked abroad at all.</summary>
    public static bool HasAny(Candidate candidate) =>
        Postings(candidate).Count > 0
        || !string.IsNullOrWhiteSpace(candidate.WorksIn)
        || candidate.ExperienceAbroadYears is > 0;
}
