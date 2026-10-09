using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Features.Workflow;

/// <summary>What one value on the board will be set back to nothing.</summary>
public record ClearedValue(string Track, string Value);

/// <summary>
/// Where a candidate would go if their last move were undone, and what it costs.
/// </summary>
public record RewindPlan(
    Guid FromStageId,
    string FromStageName,
    Guid ToStageId,
    string ToStageName,
    IReadOnlyList<ClearedValue> Clears);

/// <summary>
/// Undoing a candidate's last move between stages.
///
/// A candidate pushed into Embassy by a mis-click had nowhere to go but all the way back to
/// intake, which threw away every stage they had legitimately passed through on the way. One step
/// back is the correction people actually want.
///
/// Where "back" is comes from the candidate's own history rather than from the stage order. The
/// pipeline is not a line — stages mirror onto one another and agencies reorder them — so the
/// stage before this one by sort order is not necessarily the stage this candidate came from. The
/// event stream knows exactly, because the move that brought them here recorded it.
///
/// It is not free, which is why planning it is separate from doing it. The statuses entered
/// against the stage being left are cleared: they describe work in that stage, and leaving them
/// behind would put a candidate in LMIS carrying a medical result they have not been sent for.
/// What is *not* touched is the candidate's own record — visa numbers, sponsor details, uploaded
/// documents all stay, because those are facts about the person rather than progress through a
/// stage. The same plan is shown to whoever is confirming and then executed, so the warning and
/// the outcome cannot disagree.
/// </summary>
internal static class StageRewind
{
    internal static async Task<(RewindPlan? Plan, string? Error, int Status)> PlanAsync(
        ITenantDbContext context,
        IWorkflowEngineService engine,
        Guid candidateId,
        CancellationToken ct)
    {
        var candidate = await context.Candidates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == candidateId && !c.IsDeleted, ct);
        if (candidate is null) return (null, "Candidate not found", 404);

        if (candidate.CurrentStageId is not Guid currentStageId)
            return (null, "This candidate is not on the pipeline yet.", 400);

        // The move that put them here. Not the stage before this one in the configured order:
        // those differ as soon as anyone reorders the pipeline or skips a stage.
        var arrivals = await context.WorkflowEvents
            .AsNoTracking()
            .Where(e => e.CandidateId == candidateId
                        && e.EventType == WorkflowEventType.StageTransitioned
                        && e.FromStageId != null
                        && e.ToStageId == currentStageId)
            .OrderByDescending(e => e.SequenceNumber)
            .ToListAsync(ct);

        // Skipping the rewinds themselves. A rewind records the move it made, which arrives at
        // this stage like any other — so reading the latest arrival would find the undo and send
        // the candidate back where they were just brought from. Two rewinds in a row would swap
        // them between the same two stages for ever instead of walking back.
        var arrival = arrivals.FirstOrDefault(e => !IsRewind(e));

        if (arrival?.FromStageId is not Guid previousStageId)
            return (null, "There is no earlier stage on this candidate's history to go back to.", 400);

        var previous = await context.WorkflowStages
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == previousStageId && !s.IsDeleted, ct);

        if (previous is null)
            return (null,
                $"The stage they came from ({arrival.FromStageName ?? "unknown"}) no longer exists.", 400);

        var current = await context.WorkflowStages
            .AsNoTracking()
            .Include(s => s.Statuses)
            .Include(s => s.ParallelTracks)
            .FirstOrDefaultAsync(s => s.Id == currentStageId, ct);

        var state = await engine.GetCurrentStateAsync(candidateId, ct);

        var clears = TracksOf(current)
            .Select(track => new
            {
                Track = track,
                Value = state.StatusValues.TryGetValue(track, out var v) ? v : null,
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => new ClearedValue(x.Track, x.Value!))
            .ToList();

        return (new RewindPlan(
            currentStageId,
            candidate.CurrentStageName ?? current?.Name ?? "this stage",
            previous.Id,
            previous.Name,
            clears), null, 200);
    }

    private static bool IsRewind(WorkflowEvent evt) =>
        evt.Data.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
        && evt.Data.RootElement.TryGetProperty("rewound", out var flag)
        && flag.ValueKind == System.Text.Json.JsonValueKind.True;

    /// <summary>
    /// Every key a stage writes its progress under.
    ///
    /// Two shapes of stage write two ways. A parallel-track stage names its tracks; a simple one
    /// defines statuses with no track at all and they land under the conventional "status" key.
    /// A stage can do both, so both are collected.
    /// </summary>
    private static IReadOnlyList<string> TracksOf(WorkflowStage? stage)
    {
        if (stage is null) return [];

        var tracks = new List<string>();

        foreach (var track in stage.ParallelTracks.Where(t => !t.IsDeleted))
            tracks.Add(track.TrackName);

        foreach (var status in stage.Statuses.Where(s => !s.IsDeleted))
            tracks.Add(string.IsNullOrWhiteSpace(status.TrackName) ? DefaultTrack : status.TrackName);

        return [.. tracks
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Where a stage with no named tracks keeps its status. Matches the boards.</summary>
    private const string DefaultTrack = "status";

    /// <summary>
    /// Writes the move. The caller saves — the whole rewind is one transaction or none of it, so
    /// a half-applied one cannot leave a candidate in the old stage with its statuses wiped.
    /// </summary>
    internal static async Task ApplyAsync(
        ITenantDbContext context,
        Candidate candidate,
        RewindPlan plan,
        Guid userId,
        string userName,
        string? reason,
        CancellationToken ct)
    {
        var sequence = await context.WorkflowEvents
            .Where(e => e.CandidateId == candidate.Id)
            .Select(e => (long?)e.SequenceNumber)
            .MaxAsync(ct) ?? 0;

        // Cleared through the stream, not just on the candidate row: state is replayed from these
        // events, so a value only removed from the denormalised column comes back on the next
        // read.
        foreach (var cleared in plan.Clears)
        {
            context.WorkflowEvents.Add(new WorkflowEvent
            {
                CandidateId = candidate.Id,
                SequenceNumber = ++sequence,
                EventType = WorkflowEventType.StatusUpdated,
                Data = System.Text.Json.JsonDocument.Parse(
                    System.Text.Json.JsonSerializer.Serialize(new
                    {
                        trackName = cleared.Track,
                        oldValue = cleared.Value,
                        newValue = "",
                        rewound = true,
                    })),
                UserId = userId,
                UserName = userName,
                Notes = reason,
            });
        }

        context.WorkflowEvents.Add(new WorkflowEvent
        {
            CandidateId = candidate.Id,
            SequenceNumber = ++sequence,
            EventType = WorkflowEventType.StageTransitioned,
            FromStageId = plan.FromStageId,
            FromStageName = plan.FromStageName,
            ToStageId = plan.ToStageId,
            ToStageName = plan.ToStageName,
            Data = System.Text.Json.JsonDocument.Parse(
                System.Text.Json.JsonSerializer.Serialize(new { rewound = true })),
            UserId = userId,
            UserName = userName,
            Notes = reason,
        });

        candidate.CurrentStageId = plan.ToStageId;
        candidate.CurrentStageName = plan.ToStageName;
        candidate.StageEnteredAt = DateTime.UtcNow;
        // Mirrors of the stage being left go with it, or the candidate stays on a board they are
        // no longer in.
        candidate.VisibleInStages = [plan.ToStageId];
        candidate.CurrentStatusValues = Without(candidate.CurrentStatusValues, plan.Clears);
    }

    /// <summary>The denormalised status bag with the rewound keys taken out.</summary>
    private static System.Text.Json.JsonDocument? Without(
        System.Text.Json.JsonDocument? values, IReadOnlyList<ClearedValue> cleared)
    {
        if (values is null || values.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            return values;

        var dropped = cleared.Select(c => c.Track).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var kept = values.RootElement.EnumerateObject()
            .Where(p => !dropped.Contains(p.Name))
            .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");

        return System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(kept));
    }
}
