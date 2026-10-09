namespace SimbaFlow.Domain.Services;

/// <summary>
/// Whether a candidate may be removed from the list, and what to say when they may not.
///
/// Deleting from the candidates page used to work at any point in the pipeline, which meant the
/// intake clerk could remove someone the embassy desk was in the middle of booking — the record
/// disappeared from under a colleague, along with the medical and tasheer work already recorded
/// against it. Nobody downstream was asked, and nobody was told.
///
/// So removal belongs to the stage a record starts in. Past that, the person holding the record
/// is whoever works the stage it is standing in, and the way to retract a candidate is to walk
/// them back one stage at a time — which asks for confirmation and says what it clears. By the
/// time they are back at intake they can be deleted, and by then the desks that had them have
/// seen them leave.
/// </summary>
public static class CandidateRemoval
{
    /// <summary>
    /// Why this candidate cannot be deleted, or null when they can.
    ///
    /// <paramref name="stageName"/> is null for a candidate on no stage at all, which happens
    /// when an agency has no workflow configured yet. Those are deletable: there is no other
    /// desk to take them away from.
    /// </summary>
    public static string? Refusal(string? stageName, bool isInitialStage)
    {
        if (string.IsNullOrWhiteSpace(stageName) || isInitialStage) return null;

        return $"{stageName} is working on this candidate, so they cannot be deleted here. " +
               "Move them back to the previous stage until they reach the first one, then delete.";
    }
}
