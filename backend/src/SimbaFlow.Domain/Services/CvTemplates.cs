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

    /// <summary>
    /// The layouts, by the number the agencies call them. Only the number: the picker shows each
    /// page as a picture, and a paragraph describing a layout you are already looking at is three
    /// lines of reading between you and the next one.
    /// </summary>
    public static readonly IReadOnlyList<(string Value, string Name)> All =
    [
        (Layout1, "1st layout"),
        (Layout2, "2nd layout"),
        (Layout3, "3rd layout"),
        (Layout4, "4th layout"),
        (Layout5, "5th layout"),
        (Layout7, "7th layout"),
    ];

    /// <summary>Falls back to the default rather than failing on a value we no longer recognise.</summary>
    public static string Normalise(string? value) =>
        All.Any(t => t.Value == value) ? value! : Default;
}
