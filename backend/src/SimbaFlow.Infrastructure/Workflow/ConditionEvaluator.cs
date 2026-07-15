using System.Text.Json;

namespace SimbaFlow.Infrastructure.Workflow;

/// <summary>
/// Evaluates JSONB condition rules against the current workflow state.
/// Supports AND/OR groups with eq, neq, in, not_empty, empty operators.
/// </summary>
public static class ConditionEvaluator
{
    /// <summary>
    /// Evaluate conditions against the provided state values.
    /// Returns true if all conditions are met (or no conditions exist).
    /// </summary>
    public static bool Evaluate(JsonDocument conditions, Dictionary<string, string> stateValues, Dictionary<string, string?>? candidateFields = null)
    {
        if (conditions.RootElement.ValueKind != JsonValueKind.Object)
            return true;

        if (!conditions.RootElement.TryGetProperty("rules", out var rulesElement))
            return true;

        if (rulesElement.GetArrayLength() == 0)
            return true;

        var op = "AND";
        if (conditions.RootElement.TryGetProperty("operator", out var opElement))
            op = opElement.GetString()?.ToUpperInvariant() ?? "AND";

        var results = new List<bool>();

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

            var result = ruleOp switch
            {
                "eq" => actualValue == rule.GetProperty("value").GetString(),
                "neq" => actualValue != rule.GetProperty("value").GetString(),
                "in" => rule.GetProperty("value").EnumerateArray()
                    .Any(v => v.GetString() == actualValue),
                "not_empty" => !string.IsNullOrEmpty(actualValue),
                "empty" => string.IsNullOrEmpty(actualValue),
                _ => false
            };

            results.Add(result);
        }

        return op == "AND"
            ? results.All(r => r)
            : results.Any(r => r);
    }
}
