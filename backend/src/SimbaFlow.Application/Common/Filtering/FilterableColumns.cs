using System.Globalization;
using System.Linq.Expressions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.Application.Common.Filtering;

/// <summary>
/// The columns of one board that may be filtered, and how.
///
/// A whitelist, and the only thing standing between a query string and the database. A field
/// name that is not registered here is dropped: nothing a caller sends is ever used to name a
/// column, build a string of SQL, or reach a property by reflection. Every comparison below is
/// built from an expression this code wrote, against a property this code named.
///
/// Registered per board rather than derived from the entity on purpose. Most of what is on a
/// candidate has no business being filtered from a list — and a column that is computed, or
/// that comes from somewhere other than the row, cannot be filtered in the database at all.
/// Those columns get no funnel rather than a funnel that quietly does nothing.
/// </summary>
public sealed class FilterableColumns<T>
{
    private readonly Dictionary<string, Func<TableFilter, Expression<Func<T, bool>>?>> _fields =
        new(StringComparer.OrdinalIgnoreCase);

    public FilterableColumns<T> Text(string field, Expression<Func<T, string?>> selector)
    {
        _fields[field] = f => FilterPredicates.Text(selector, f);
        return this;
    }

    public FilterableColumns<T> Number(string field, Expression<Func<T, int?>> selector)
    {
        _fields[field] = f => FilterPredicates.Number(selector, f);
        return this;
    }

    public FilterableColumns<T> Money(string field, Expression<Func<T, decimal?>> selector)
    {
        _fields[field] = f => FilterPredicates.Money(selector, f);
        return this;
    }

    public FilterableColumns<T> Date(string field, Expression<Func<T, DateOnly?>> selector)
    {
        _fields[field] = f => FilterPredicates.Date(selector, f);
        return this;
    }

    public FilterableColumns<T> Timestamp(string field, Expression<Func<T, DateTime?>> selector)
    {
        _fields[field] = f => FilterPredicates.Timestamp(selector, f);
        return this;
    }

    /// <summary>
    /// A column held as an enum and shown as its name.
    ///
    /// Matched by resolving the typed text to the enum values whose names answer it, and then
    /// comparing against those — rather than turning the column into a string in the database,
    /// which Postgres stores as an integer and cannot compare without reading every row.
    /// "Contains" and "starts with" work the same way, so a half-typed "run" still finds
    /// Runaway.
    /// </summary>
    public FilterableColumns<T> Enum<TEnum>(string field, Expression<Func<T, TEnum>> selector)
        where TEnum : struct, System.Enum
    {
        _fields[field] = f => FilterPredicates.Enum(selector, f);
        return this;
    }

    /// <summary>
    /// A column whose own handling the board supplies.
    ///
    /// For the ones that are not a comparison against a column: an age held as a date of birth,
    /// a name spread over three columns. The board knows how its own data is shaped.
    /// </summary>
    public FilterableColumns<T> Custom(
        string field, Func<TableFilter, Expression<Func<T, bool>>?> build)
    {
        _fields[field] = build;
        return this;
    }

    public bool Knows(string field) => _fields.ContainsKey(field);

    /// <summary>
    /// Narrows <paramref name="query"/> by every filter this board recognises.
    ///
    /// Filters it does not recognise are skipped in silence, as are ones whose value will not
    /// parse — "greater than banana" on an age column widens the list to everything rather than
    /// failing the request, which is what a half-typed box should do.
    /// </summary>
    public IQueryable<T> Apply(IQueryable<T> query, IEnumerable<TableFilter> filters)
    {
        foreach (var filter in filters)
        {
            if (!_fields.TryGetValue(filter.Field, out var build)) continue;

            var predicate = build(filter);
            if (predicate is not null) query = query.Where(predicate);
        }

        return query;
    }
}

/// <summary>
/// One filter turned into one predicate.
///
/// The expression is grown around the selector's own body and reuses its parameter, so no
/// rebinding is needed and what EF sees is the same member access the board wrote.
/// </summary>
public static class FilterPredicates
{
    private static Expression<Func<T, bool>> Lambda<T>(Expression body, ParameterExpression p) =>
        Expression.Lambda<Func<T, bool>>(body, p);

    private static readonly System.Reflection.MethodInfo ToLower =
        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private static readonly System.Reflection.MethodInfo StringContains =
        typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
    private static readonly System.Reflection.MethodInfo StringStartsWith =
        typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;

    public static Expression<Func<T, bool>>? Text<T>(
        Expression<Func<T, string?>> selector, TableFilter filter)
    {
        var p = selector.Parameters[0];
        var value = selector.Body;
        var nullConst = Expression.Constant(null, typeof(string));
        var notNull = Expression.NotEqual(value, nullConst);

        // Lowered on both sides rather than compared with ILike: both become the same scan in
        // Postgres, and this one also runs on the in-memory provider the tests use. A search
        // that silently matches nothing is exactly the failure worth having tests for.
        Expression Lowered() => Expression.Call(value, ToLower);
        Expression Needle() => Expression.Constant(filter.Value!.ToLowerInvariant(), typeof(string));

        Expression? body = filter.Operator switch
        {
            FilterOperator.Equals =>
                Expression.AndAlso(notNull, Expression.Equal(Lowered(), Needle())),
            FilterOperator.NotEquals =>
                Expression.OrElse(
                    Expression.Equal(value, nullConst),
                    Expression.NotEqual(Lowered(), Needle())),
            FilterOperator.Contains =>
                Expression.AndAlso(notNull, Expression.Call(Lowered(), StringContains, Needle())),
            FilterOperator.StartsWith =>
                Expression.AndAlso(notNull, Expression.Call(Lowered(), StringStartsWith, Needle())),
            // Nothing recorded covers both null and "", because a form that writes an empty box
            // and one that writes nothing at all are the same thing to the person reading it.
            FilterOperator.Blank =>
                Expression.OrElse(
                    Expression.Equal(value, nullConst),
                    Expression.Equal(value, Expression.Constant(string.Empty, typeof(string)))),
            FilterOperator.NotBlank =>
                Expression.AndAlso(
                    notNull,
                    Expression.NotEqual(value, Expression.Constant(string.Empty, typeof(string)))),
            _ => null,
        };

        return body is null ? null : Lambda<T>(body, p);
    }

    public static Expression<Func<T, bool>>? Enum<T, TEnum>(
        Expression<Func<T, TEnum>> selector, TableFilter filter)
        where TEnum : struct, System.Enum
    {
        var p = selector.Parameters[0];
        var value = selector.Body;

        // A column that is always one of a fixed set is never blank. Saying so beats offering a
        // funnel that quietly matches nothing.
        if (filter.Operator == FilterOperator.Blank)
            return Lambda<T>(Expression.Constant(false), p);
        if (filter.Operator == FilterOperator.NotBlank)
            return Lambda<T>(Expression.Constant(true), p);

        if (string.IsNullOrWhiteSpace(filter.Value)) return null;
        var needle = filter.Value.Trim();

        Func<string, bool> answers = filter.Operator switch
        {
            FilterOperator.Equals or FilterOperator.NotEquals =>
                name => name.Equals(needle, StringComparison.OrdinalIgnoreCase),
            FilterOperator.Contains =>
                name => name.Contains(needle, StringComparison.OrdinalIgnoreCase),
            FilterOperator.StartsWith =>
                name => name.StartsWith(needle, StringComparison.OrdinalIgnoreCase),
            _ => _ => false,
        };

        var wanted = System.Enum.GetValues<TEnum>().Where(v => answers(v.ToString()!)).ToList();
        var negate = filter.Operator == FilterOperator.NotEquals;

        // Nothing is called that. An empty "is one of" is false, and its negation is everything.
        if (wanted.Count == 0)
            return Lambda<T>(Expression.Constant(negate), p);

        Expression body = wanted
            .Select(v => (Expression)Expression.Equal(value, Expression.Constant(v, typeof(TEnum))))
            .Aggregate(Expression.OrElse);

        return Lambda<T>(negate ? Expression.Not(body) : body, p);
    }

    public static Expression<Func<T, bool>>? Number<T>(
        Expression<Func<T, int?>> selector, TableFilter filter) =>
        Comparable<T, int>(selector, filter, s =>
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                ? v : null);

    public static Expression<Func<T, bool>>? Money<T>(
        Expression<Func<T, decimal?>> selector, TableFilter filter) =>
        Comparable<T, decimal>(selector, filter, s =>
            decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
                ? v : null);

    public static Expression<Func<T, bool>>? Date<T>(
        Expression<Func<T, DateOnly?>> selector, TableFilter filter) =>
        Comparable<T, DateOnly>(selector, filter, s =>
            DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var v)
                ? v : null);

    /// <summary>
    /// A moment compared as a day.
    ///
    /// The value from the screen is a date, and a timestamp is almost never exactly midnight, so
    /// comparing them directly makes "equals today" match nothing and "on or before today" miss
    /// everything that happened today. Each operator is widened to the day instead.
    /// </summary>
    public static Expression<Func<T, bool>>? Timestamp<T>(
        Expression<Func<T, DateTime?>> selector, TableFilter filter)
    {
        var p = selector.Parameters[0];
        var value = selector.Body;
        var hasValue = Expression.Property(value, "HasValue");
        var inner = Expression.Property(value, "Value");

        static DateTime? Day(string? raw) =>
            DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                : null;

        static Expression Const(DateTime d) => Expression.Constant(d, typeof(DateTime));

        if (filter.Operator == FilterOperator.Blank)
            return Lambda<T>(Expression.Not(hasValue), p);
        if (filter.Operator == FilterOperator.NotBlank)
            return Lambda<T>(hasValue, p);

        if (Day(filter.Value) is not DateTime from) return null;
        var nextDay = from.AddDays(1);

        Expression? body = filter.Operator switch
        {
            FilterOperator.Equals => Expression.AndAlso(
                Expression.GreaterThanOrEqual(inner, Const(from)),
                Expression.LessThan(inner, Const(nextDay))),
            FilterOperator.NotEquals => Expression.OrElse(
                Expression.LessThan(inner, Const(from)),
                Expression.GreaterThanOrEqual(inner, Const(nextDay))),
            FilterOperator.GreaterThan => Expression.GreaterThanOrEqual(inner, Const(nextDay)),
            FilterOperator.GreaterOrEqual => Expression.GreaterThanOrEqual(inner, Const(from)),
            FilterOperator.LessThan => Expression.LessThan(inner, Const(from)),
            FilterOperator.LessOrEqual => Expression.LessThan(inner, Const(nextDay)),
            FilterOperator.Between when Day(filter.Value2) is DateTime to => Expression.AndAlso(
                Expression.GreaterThanOrEqual(inner, Const(from)),
                Expression.LessThan(inner, Const(to.AddDays(1)))),
            _ => null,
        };

        return body is null ? null : Lambda<T>(Expression.AndAlso(hasValue, body), p);
    }

    private static Expression<Func<T, bool>>? Comparable<T, TValue>(
        Expression<Func<T, TValue?>> selector,
        TableFilter filter,
        Func<string?, TValue?> parse)
        where TValue : struct
    {
        var p = selector.Parameters[0];
        var value = selector.Body;
        var hasValue = Expression.Property(value, "HasValue");
        var inner = Expression.Property(value, "Value");

        if (filter.Operator == FilterOperator.Blank)
            return Lambda<T>(Expression.Not(hasValue), p);
        if (filter.Operator == FilterOperator.NotBlank)
            return Lambda<T>(hasValue, p);

        if (parse(filter.Value) is not TValue from) return null;
        var constant = Expression.Constant(from, typeof(TValue));

        Expression? body = filter.Operator switch
        {
            FilterOperator.Equals => Expression.Equal(inner, constant),
            FilterOperator.NotEquals => Expression.NotEqual(inner, constant),
            FilterOperator.GreaterThan => Expression.GreaterThan(inner, constant),
            FilterOperator.GreaterOrEqual => Expression.GreaterThanOrEqual(inner, constant),
            FilterOperator.LessThan => Expression.LessThan(inner, constant),
            FilterOperator.LessOrEqual => Expression.LessThanOrEqual(inner, constant),
            FilterOperator.Between when parse(filter.Value2) is TValue to => Expression.AndAlso(
                Expression.GreaterThanOrEqual(inner, constant),
                Expression.LessThanOrEqual(inner, Expression.Constant(to, typeof(TValue)))),
            _ => null,
        };

        return body is null ? null : Lambda<T>(Expression.AndAlso(hasValue, body), p);
    }
}
