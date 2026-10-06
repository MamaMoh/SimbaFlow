namespace SimbaFlow.Domain.Services;

/// <summary>
/// Skills every agency starts with, matching the boolean columns on Candidate.
/// Custom skills live beside these in agency_skills; these keys must stay stable so a rename
/// in the UI cannot break the mapped columns.
/// </summary>
public static class BuiltInSkills
{
    public const string Cleaning = "cleaning";
    public const string Washing = "washing";
    public const string Cooking = "cooking";
    public const string BabySitting = "babysitting";
    public const string ChildCare = "childCare";
    public const string Ironing = "ironing";
    public const string Sewing = "sewing";
    public const string ArabicCooking = "arabicCooking";
    public const string Tutoring = "tutoring";
    public const string Computer = "computer";

    public sealed record Spec(string Key, string Name, int SortOrder, bool DefaultSelected);

    public static readonly IReadOnlyList<Spec> All =
    [
        new(Cleaning, "Cleaning", 0, true),
        new(Washing, "Washing", 1, true),
        new(Cooking, "Cooking", 2, false),
        new(BabySitting, "Baby Sitting", 3, false),
        new(ChildCare, "Child care", 4, false),
        new(Ironing, "Ironing", 5, false),
        new(Sewing, "Sewing", 6, false),
        new(ArabicCooking, "Arabic cooking", 7, false),
        new(Tutoring, "Tutoring", 8, false),
        new(Computer, "Computer", 9, false),
    ];

    public static bool IsBuiltInKey(string? key) =>
        !string.IsNullOrWhiteSpace(key) &&
        All.Any(s => s.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public static bool IsBuiltInName(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        All.Any(s => s.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
}
