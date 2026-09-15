namespace SimbaFlow.Domain.Services;

/// <summary>
/// The CV layouts an agency can choose between.
///
/// Kept as strings rather than an enum because the set grows as agencies bring in the forms their
/// own partners insist on, and a stored enum value is awkward to extend once it is in JSON.
/// </summary>
public static class CvTemplates
{
    /// <summary>The bilingual Application for Employment table the Gulf agencies circulate.</summary>
    public const string Enjaz = "enjaz";

    /// <summary>A single-column profile sheet: large portrait, plain English headings.</summary>
    public const string Profile = "profile";

    public const string Default = Enjaz;

    public static readonly IReadOnlyList<(string Value, string Name, string Description)> All =
    [
        (Enjaz, "Enjaz application",
            "Bilingual English and Arabic table. The form Gulf partners and embassies expect."),
        (Profile, "Profile sheet",
            "One column, large photo, English headings. Easier to read when a partner just wants to see the candidate."),
    ];

    /// <summary>Falls back to the default rather than failing on a value we no longer recognise.</summary>
    public static string Normalise(string? value) =>
        All.Any(t => t.Value == value) ? value! : Default;
}
