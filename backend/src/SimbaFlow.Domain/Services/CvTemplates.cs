namespace SimbaFlow.Domain.Services;

/// <summary>
/// The CV forms an agency can choose between.
///
/// Numbered the way the agencies number them — they ask for "the 1st layout" — and kept as strings
/// rather than an enum because the set grows as agencies bring in the forms their own partners
/// insist on, and a stored enum value is awkward to extend once it sits in JSON.
/// </summary>
public static class CvTemplates
{
    public const string Layout1 = "layout1";
    public const string Layout2 = "layout2";
    public const string Layout3 = "layout3";
    public const string Layout4 = "layout4";
    public const string Layout5 = "layout5";
    public const string Layout7 = "layout7";

    public const string Default = Layout3;

    public static readonly IReadOnlyList<(string Value, string Name, string Description)> All =
    [
        (Layout1, "1st layout",
            "Full-body photo down the left with name, address and passport beneath it; personal details, contract and skills on the right."),
        (Layout2, "2nd layout",
            "Wide header with headshot, applicant details beside a full-body photo, and skills across the full width."),
        (Layout3, "3rd layout",
            "Application for Employment: summary at the top, applicant and passport side by side, photo on the right."),
        (Layout4, "4th layout",
            "Headshot in the letterhead, full-body photo with work and skills on the left, passport and applicant details on the right."),
        (Layout5, "5th layout",
            "Job Application: photo, letterhead and placement summary across the top, personal details left, photo and skill ticks right."),
        (Layout7, "7th layout",
            "Candidate's Resume: both photos stacked left, one personal-information table right, skills as a tick grid along the bottom."),
    ];

    /// <summary>Falls back to the default rather than failing on a value we no longer recognise.</summary>
    public static string Normalise(string? value) =>
        All.Any(t => t.Value == value) ? value! : Default;
}
