namespace SimbaFlow.Domain.Services;

/// <summary>
/// How long a placement runs for.
///
/// The length is recorded as a plain number of years and printed as words, because the form used
/// to take free text and collected "2 Years", "2 years", "2yrs", "24 months" and "Two years" for
/// the same contract — a column nothing could count and a CV that read differently depending on
/// who typed it.
///
/// Rows written before that are left alone rather than migrated: anything already carrying words
/// is printed back exactly as it was signed, and only a bare number is given its unit here.
/// </summary>
public static class ContractTerm
{
    /// <summary>The number of years, where the stored value starts with one.</summary>
    public static int? Years(string? stored)
    {
        var digits = new string((stored ?? "").Trim().TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var years) && years > 0 ? years : null;
    }

    /// <summary>What a document prints: "2 Years" from "2", and anything else as written.</summary>
    public static string Label(string? stored, string whenEmpty = "")
    {
        var text = (stored ?? "").Trim();
        if (text.Length == 0)
            return whenEmpty;
        if (text.Any(char.IsLetter) || Years(text) is not int years)
            return text;
        return years == 1 ? "1 Year" : $"{years} Years";
    }
}
