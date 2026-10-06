namespace SimbaFlow.Domain.Services;

/// <summary>
/// How the bot decides what someone meant when they typed a name or a passport number.
///
/// "/status ETENESH ACHALU TOLESA" answered "Candidate not found" for a candidate who exists. The
/// query matched <c>FirstName + " " + LastName</c>, which leaves the middle name out, so every
/// three-part Ethiopian name — which is most of them — missed. It was also case-sensitive, because
/// Postgres <c>LIKE</c> is, so typing a name in lower case found nobody either.
///
/// The rules here are deliberately generous: staff type what they remember, not what is stored.
/// </summary>
public static class BotCandidateSearch
{
    /// <summary>
    /// How many candidates one search may return.
    ///
    /// Typing a first name is meant to produce everyone who has it, not a sample. The cap exists
    /// only so that a one-letter slip cannot try to list the whole agency; it sits far above any
    /// real first name's share of a book of candidates.
    /// </summary>
    public const int MaxResults = 200;

    /// <summary>
    /// A passport number is a single token of digits and letters with no spaces. Treating one as a
    /// name search would return every candidate whose name happens to contain those letters.
    /// </summary>
    public static bool LooksLikePassport(string? query)
    {
        var q = (query ?? string.Empty).Trim();
        if (q.Length < 5 || q.Length > 20) return false;
        if (q.Contains(' ')) return false;
        var hasDigit = false;
        foreach (var c in q)
        {
            if (char.IsDigit(c)) hasDigit = true;
            else if (!char.IsLetter(c)) return false;
        }
        return hasDigit;
    }

    /// <summary>
    /// The words of a name query, lowercased, with the noise removed.
    ///
    /// Every word has to appear somewhere in the candidate's name, in any order — so "tolesa
    /// etenesh" finds the same person as "Etenesh Achalu Tolesa", and a half-remembered
    /// "etenesh tolesa" finds her without the middle name.
    /// </summary>
    public static IReadOnlyList<string> NameTerms(string? query) =>
        (query ?? string.Empty)
            .Split([' ', '\t', ',', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Where(t => t.Length > 1)
            .Distinct()
            .Take(6)
            .ToList();

    /// <summary>
    /// True when every term of the query appears in the candidate's name.
    /// Mirrors the database predicate so the two cannot drift; the tests exercise this one.
    /// </summary>
    public static bool NameMatches(
        string? firstName, string? middleName, string? lastName, string? localFullName, string query)
    {
        var terms = NameTerms(query);
        if (terms.Count == 0) return false;

        var haystack = string.Join(
            ' ',
            new[] { firstName, middleName, lastName, localFullName }
                .Where(p => !string.IsNullOrWhiteSpace(p)))
            .ToLowerInvariant();

        return terms.All(haystack.Contains);
    }
}
