using System.Text.Json;

namespace SimbaFlow.Domain.Services;

/// <summary>
/// What a person asked of one column, on its way from the filter row to the database.
///
/// Boards are paginated on the server, so a filter that only narrowed the rows already fetched
/// would answer a different question from the one being asked — an agency with two thousand
/// people on the books, filtering a page of twenty. These travel with the query instead.
/// </summary>
public enum FilterOperator
{
    Equals,
    NotEquals,
    /// <summary>Text only. The one people mean when they type part of a name.</summary>
    Contains,
    StartsWith,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
    /// <summary>Needs both bounds, and includes them.</summary>
    Between,
    /// <summary>Nothing recorded — null, or an empty string.</summary>
    Blank,
    NotBlank,
}

/// <param name="Field">The column's name in the board's own whitelist, never a column name.</param>
public sealed record TableFilter(
    string Field,
    FilterOperator Operator,
    string? Value = null,
    string? Value2 = null);

/// <summary>
/// Reading the filter row off a query string.
///
/// Pure, and deliberately forgiving: anything that does not parse is dropped rather than
/// refused. A stale bookmark, a column removed in a release, a hand-edited URL — none of those
/// are worth a 400 in front of somebody trying to look at a list, and a dropped filter shows
/// as a wider result rather than an error. What is *not* forgiving is which fields may be
/// filtered at all: that is the board's whitelist, applied after this, and a field nobody
/// listed never reaches the database.
/// </summary>
public static class TableFilters
{
    /// <summary>A guard on a hand-written URL, not a limit anyone reaches through the screen.</summary>
    public const int MaxFilters = 20;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static FilterOperator? ParseOperator(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "eq" or "equals" => FilterOperator.Equals,
        "neq" or "notequals" => FilterOperator.NotEquals,
        "contains" => FilterOperator.Contains,
        "startswith" => FilterOperator.StartsWith,
        "gt" => FilterOperator.GreaterThan,
        "gte" => FilterOperator.GreaterOrEqual,
        "lt" => FilterOperator.LessThan,
        "lte" => FilterOperator.LessOrEqual,
        "between" => FilterOperator.Between,
        "blank" => FilterOperator.Blank,
        "notblank" => FilterOperator.NotBlank,
        _ => null,
    };

    /// <summary>Whether this operator reads a value at all.</summary>
    public static bool NeedsValue(FilterOperator op) =>
        op is not (FilterOperator.Blank or FilterOperator.NotBlank);

    /// <summary>Whether this operator reads a second value.</summary>
    public static bool NeedsSecondValue(FilterOperator op) => op is FilterOperator.Between;

    private sealed record Wire(string? Field, string? Op, string? Value, string? Value2);

    /// <summary>
    /// The filters in a `filters=[…]` query parameter.
    ///
    /// Returns an empty list for absent, empty or malformed input. A filter missing the value
    /// its operator needs is dropped too — a half-typed "greater than" with no number yet is
    /// the ordinary state of the box while somebody is still typing in it, and it should widen
    /// the list to everything rather than narrow it to nothing.
    /// </summary>
    public static IReadOnlyList<TableFilter> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];

        Wire[]? wire;
        try
        {
            wire = JsonSerializer.Deserialize<Wire[]>(raw, Json);
        }
        catch (JsonException)
        {
            return [];
        }

        if (wire is null) return [];

        var parsed = new List<TableFilter>();
        foreach (var w in wire)
        {
            if (parsed.Count >= MaxFilters) break;
            if (string.IsNullOrWhiteSpace(w.Field)) continue;

            var op = ParseOperator(w.Op);
            if (op is null) continue;

            if (NeedsValue(op.Value) && string.IsNullOrWhiteSpace(w.Value)) continue;
            if (NeedsSecondValue(op.Value) && string.IsNullOrWhiteSpace(w.Value2)) continue;

            parsed.Add(new TableFilter(w.Field.Trim(), op.Value, w.Value?.Trim(), w.Value2?.Trim()));
        }

        return parsed;
    }
}
