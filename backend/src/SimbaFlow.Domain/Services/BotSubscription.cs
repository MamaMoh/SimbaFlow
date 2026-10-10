namespace SimbaFlow.Domain.Services;

/// <summary>
/// Who hears about what.
///
/// Everyone who linked the bot got every move in the agency. For an agency owner that is the
/// point; for an embassy officer it is thirty messages a day about other people's work, and the
/// thing people do about that is turn the bot off altogether.
///
/// Two controls, deliberately coarse: silence, and a list of stages. Anything finer is a
/// preferences screen nobody fills in.
/// </summary>
public static class BotSubscription
{
    /// <summary>Reads the stored list. Empty means everything.</summary>
    public static IReadOnlyList<string> Parse(string? watchStages) =>
        string.IsNullOrWhiteSpace(watchStages)
            ? []
            : [.. watchStages
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>What to store for a typed list. Null when the answer is "all of them".</summary>
    public static string? Format(IEnumerable<string> stageNames)
    {
        var names = stageNames
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return names.Count == 0 ? null : string.Join(", ", names);
    }

    /// <summary>"all", "none", "*" and an empty list all mean every stage.</summary>
    public static bool MeansEverything(string? argument)
    {
        var text = (argument ?? "").Trim();
        return text.Length == 0
            || text.Equals("all", StringComparison.OrdinalIgnoreCase)
            || text.Equals("*", StringComparison.Ordinal);
    }

    /// <summary>
    /// Should this person be told about something happening in this stage?
    ///
    /// A muted person hears nothing. Somebody watching nothing in particular hears everything.
    /// An event with no stage attached — a passport expiring, a digest — is never filtered out
    /// by a stage list, because no stage is not the same as the wrong stage.
    /// </summary>
    public static bool Wants(bool muted, string? watchStages, string? stageName)
    {
        if (muted) return false;

        var watched = Parse(watchStages);
        if (watched.Count == 0) return true;
        if (string.IsNullOrWhiteSpace(stageName)) return true;

        return watched.Any(w => stageName.Contains(w, StringComparison.OrdinalIgnoreCase)
                                || w.Contains(stageName, StringComparison.OrdinalIgnoreCase));
    }
}
