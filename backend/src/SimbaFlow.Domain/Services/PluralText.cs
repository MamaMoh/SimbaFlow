namespace SimbaFlow.Domain.Services;

/// <summary>
/// Counted nouns in text a person reads.
///
/// "3 day(s) ago" is what gets written when a count and a noun meet and nobody
/// wants to write the branch twice. The count is always in hand at that point,
/// so the word can simply agree with it.
/// </summary>
public static class PluralText
{
    /// <summary>"1 day", "3 days". Pass <paramref name="plural"/> for an irregular noun.</summary>
    public static string Count(int count, string singular, string? plural = null) =>
        $"{count} {Word(count, singular, plural)}";

    /// <summary>The noun alone, agreeing with <paramref name="count"/>.</summary>
    public static string Word(int count, string singular, string? plural = null) =>
        count == 1 ? singular : plural ?? singular + "s";

    /// <summary>
    /// "a", "a and b", "a, b and c" — a list inside a sentence.
    ///
    /// Joining with commas throughout reads as a machine reciting fields; the last "and" is what
    /// makes it a sentence someone can act on.
    /// </summary>
    public static string List(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}",
    };
}
