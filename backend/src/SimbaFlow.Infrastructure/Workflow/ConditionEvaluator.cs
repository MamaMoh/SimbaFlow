using System.Text.Json;

namespace SimbaFlow.Infrastructure.Workflow;

/// <summary>
/// Evaluates JSONB condition rules against the current workflow state.
/// Supports AND/OR groups with eq, neq, in, not_empty, empty operators.
/// </summary>
public static class ConditionEvaluator
{
    /// <summary>
    /// Outcome of a single rule, kept so callers can explain a block to the user
    /// instead of reporting a bare "conditions not met".
    /// </summary>
    private readonly record struct RuleOutcome(bool Passed, string Field, string Op, string? Expected, string? Actual);

    /// <summary>
    /// Evaluate conditions against the provided state values.
    /// Returns true if all conditions are met (or no conditions exist).
    /// </summary>
    public static bool Evaluate(
        JsonDocument conditions,
        Dictionary<string, string> stateValues,
        Dictionary<string, string?>? candidateFields = null)
        => EvaluateInternal(conditions, stateValues, candidateFields, out _);

    /// <summary>
    /// Evaluate and, when the result is false, produce a reason naming the field, the value it
    /// needs and the value it actually holds. Workflow buttons are gated on these conditions, so
    /// a user staring at a disabled "To LMIS" needs to be told it is waiting on the visa rather
    /// than left to guess.
    /// </summary>
    public static bool TryEvaluate(
        JsonDocument conditions,
        Dictionary<string, string> stateValues,
        Dictionary<string, string?>? candidateFields,
        out string? reason)
    {
        var met = EvaluateInternal(conditions, stateValues, candidateFields, out var outcomes);
        if (met)
        {
            reason = null;
            return true;
        }

        // AND: the first failing rule is the blocker. OR: every branch failed, so list them.
        var failed = outcomes.Where(o => !o.Passed).ToList();
        reason = failed.Count == 0
            ? "Conditions not met"
            : string.Join(" or ", failed.Select(Describe));
        return false;
    }

    private static string Describe(RuleOutcome outcome)
    {
        var actual = string.IsNullOrEmpty(outcome.Actual) ? "not set" : outcome.Actual;
        var field = Humanise(outcome.Field);

        return outcome.Op switch
        {
            "eq" => $"{field} must be {outcome.Expected} (currently {actual})",
            "neq" => $"{field} must not be {outcome.Expected}",
            "in" => $"{field} must be one of {outcome.Expected} (currently {actual})",
            "not_empty" => $"{field} is required",
            "empty" => $"{field} must be cleared (currently {actual})",
            _ => $"{field} is not in the required state"
        };
    }

    /// <summary>Turns a rule field key such as "visa_submission_date" into "Visa submission date".</summary>
    private static string Humanise(string field)
    {
        var spaced = field.Replace('_', ' ').Trim();
        if (spaced.Length == 0)
            return field;
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    private static bool EvaluateInternal(
        JsonDocument conditions,
        Dictionary<string, string> stateValues,
        Dictionary<string, string?>? candidateFields,
        out List<RuleOutcome> outcomes)
    {
        outcomes = [];

        if (conditions.RootElement.ValueKind != JsonValueKind.Object)
            return true;

        if (!conditions.RootElement.TryGetProperty("rules", out var rulesElement))
            return true;

        if (rulesElement.GetArrayLength() == 0)
            return true;

        var op = "AND";
        if (conditions.RootElement.TryGetProperty("operator", out var opElement))
            op = opElement.GetString()?.ToUpperInvariant() ?? "AND";

        foreach (var rule in rulesElement.EnumerateArray())
        {
            var field = rule.GetProperty("field").GetString()!;
            var ruleOp = rule.GetProperty("op").GetString()!;

            // Resolve actual value from state or candidate fields
            string? actualValue = null;
            if (stateValues.TryGetValue(field, out var stateVal))
                actualValue = stateVal;
            else if (candidateFields?.TryGetValue(field, out var fieldVal) == true)
                actualValue = fieldVal;

            string? expected = null;
            bool result;

            switch (ruleOp)
            {
                case "eq":
                    expected = rule.GetProperty("value").GetString();
                    result = Matches(actualValue, expected);
                    break;
                case "neq":
                    expected = rule.GetProperty("value").GetString();
                    result = !Matches(actualValue, expected);
                    break;
                case "in":
                    var options = rule.GetProperty("value").EnumerateArray()
                        .Select(v => v.GetString()).ToList();
                    expected = string.Join(", ", options);
                    result = options.Any(v => Matches(actualValue, v));
                    break;
                case "not_empty":
                    result = !string.IsNullOrEmpty(actualValue);
                    break;
                case "empty":
                    result = string.IsNullOrEmpty(actualValue);
                    break;
                default:
                    result = false;
                    break;
            }

            outcomes.Add(new RuleOutcome(result, field, ruleOp, expected, actualValue));
        }

        return op == "AND"
            ? outcomes.All(r => r.Passed)
            : outcomes.Any(r => r.Passed);
    }

    /// <summary>
    /// Status values are written by humans through several different screens, so "Fit", "fit" and
    /// " Fit " all occur in practice. Comparing them ordinally silently strands candidates: the
    /// mirror never fires and nobody can see why.
    /// </summary>
    private static bool Matches(string? actual, string? expected) =>
        string.Equals(actual?.Trim(), expected?.Trim(), StringComparison.OrdinalIgnoreCase);
}
