using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Tests.Properties;

/// <summary>
/// Property-based tests for the workflow engine core (state apply + conditions).
/// </summary>
public class WorkflowEngineProperties
{
    /// <summary>
    /// Property: replaying the same ordered event sequence twice yields identical stage/status/visibility.
    /// </summary>
    [Property(MaxTest = 50)]
    public bool EventReplay_IsIdempotent(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var events = GenerateEventSequence(rng, count: 5 + rng.Next(10));

        var state1 = WorkflowState.Initial();
        foreach (var e in events) state1.Apply(e);

        var state2 = WorkflowState.Initial();
        foreach (var e in events) state2.Apply(e);

        return state1.StageId == state2.StageId
            && state1.StageName == state2.StageName
            && state1.StatusValues.OrderBy(kv => kv.Key)
                .SequenceEqual(state2.StatusValues.OrderBy(kv => kv.Key))
            && state1.VisibleInStages.SetEquals(state2.VisibleInStages);
    }

    /// <summary>
    /// Property: snapshot round-trip preserves stage, status values, and visibility.
    /// </summary>
    [Property(MaxTest = 40)]
    public bool Snapshot_RoundTrip_PreservesState(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var stageId = Guid.NewGuid();
        var stageName = $"Stage{rng.Next(1, 9)}";
        var status = new Dictionary<string, string>
        {
            ["medical"] = rng.Next(2) == 0 ? "Fit" : "Pending",
            ["tasheer"] = rng.Next(2) == 0 ? "Book Done" : "Booked"
        };
        var visible = Enumerable.Range(0, rng.Next(0, 3)).Select(_ => Guid.NewGuid()).ToArray();

        var snapshot = new WorkflowSnapshot
        {
            CandidateId = Guid.NewGuid(),
            SequenceNumber = rng.Next(1, 100),
            StageId = stageId,
            StageName = stageName,
            StatusValues = JsonDocument.Parse(JsonSerializer.Serialize(status)),
            VisibleInStages = visible
        };

        var state = WorkflowState.FromSnapshot(snapshot);

        return state.StageId == stageId
            && state.StageName == stageName
            && status.All(kv => state.StatusValues.TryGetValue(kv.Key, out var v) && v == kv.Value)
            && state.VisibleInStages.SetEquals(visible);
    }

    /// <summary>
    /// Property: condition evaluation is deterministic for the same inputs.
    /// </summary>
    [Property(MaxTest = 80)]
    public bool ConditionEvaluation_IsDeterministic(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var medical = rng.Next(2) == 0 ? "Fit" : "Unfit";
        var tasheer = rng.Next(2) == 0 ? "Book Done" : "Pending";
        var conditions = JsonDocument.Parse("""
            {"operator":"AND","rules":[
              {"field":"medical","op":"eq","value":"Fit"},
              {"field":"tasheer","op":"eq","value":"Book Done"}
            ]}
            """);
        var values = new Dictionary<string, string>
        {
            ["medical"] = medical,
            ["tasheer"] = tasheer
        };

        var a = ConditionEvaluator.Evaluate(conditions, values);
        var b = ConditionEvaluator.Evaluate(conditions, values);
        return a == b;
    }

    /// <summary>
    /// Property: AND of contradictory eq rules never succeeds.
    /// </summary>
    [Property(MaxTest = 30)]
    public bool ContradictoryAnd_NeverSucceeds(NonEmptyString valueA, NonEmptyString valueB)
    {
        if (valueA.Get == valueB.Get) return true;

        var conditions = JsonDocument.Parse($$"""
            {"operator":"AND","rules":[
              {"field":"x","op":"eq","value":{{JsonSerializer.Serialize(valueA.Get)}}},
              {"field":"x","op":"eq","value":{{JsonSerializer.Serialize(valueB.Get)}}}
            ]}
            """);

        return !ConditionEvaluator.Evaluate(conditions, new Dictionary<string, string> { ["x"] = valueA.Get });
    }

    /// <summary>
    /// Property: applying StageTransitioned then StatusUpdated yields both stage and status (stateful model).
    /// </summary>
    [Property(MaxTest = 40)]
    public bool StatefulModel_TransitionThenStatus_Composes(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var fromId = Guid.NewGuid();
        var toId = Guid.NewGuid();
        var track = "medical";
        var value = rng.Next(2) == 0 ? "Fit" : "Pending";

        var state = WorkflowState.Initial();
        state.Apply(new WorkflowEvent
        {
            EventType = WorkflowEventType.StageTransitioned,
            FromStageId = fromId,
            FromStageName = "A",
            ToStageId = toId,
            ToStageName = "B",
            Data = JsonDocument.Parse("{}"),
            SequenceNumber = 1,
            CandidateId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserName = "t"
        });
        state.Apply(new WorkflowEvent
        {
            EventType = WorkflowEventType.StatusUpdated,
            ToStageId = toId,
            ToStageName = "B",
            Data = JsonDocument.Parse(JsonSerializer.Serialize(new { trackName = track, newValue = value })),
            SequenceNumber = 2,
            CandidateId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserName = "t"
        });

        return state.StageId == toId
            && state.StageName == "B"
            && state.StatusValues.TryGetValue(track, out var v)
            && v == value;
    }

    /// <summary>
    /// Property: mirror activate then deactivate restores prior visibility set membership for that target.
    /// </summary>
    [Property(MaxTest = 30)]
    public bool MirrorActivateDeactivate_RemovesTarget(PositiveInt seed)
    {
        var target = Guid.NewGuid();
        var state = WorkflowState.Initial();
        state.StageId = Guid.NewGuid();
        state.StageName = "Embassy";

        state.Apply(new WorkflowEvent
        {
            EventType = WorkflowEventType.MirrorViewActivated,
            ToStageId = target,
            ToStageName = "LMIS",
            Data = JsonDocument.Parse("{}"),
            SequenceNumber = 1,
            CandidateId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserName = "t"
        });
        if (!state.VisibleInStages.Contains(target)) return false;

        state.Apply(new WorkflowEvent
        {
            EventType = WorkflowEventType.MirrorViewDeactivated,
            ToStageId = target,
            ToStageName = "LMIS",
            Data = JsonDocument.Parse("{}"),
            SequenceNumber = 2,
            CandidateId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserName = "t"
        });

        return !state.VisibleInStages.Contains(target);
    }

    private static List<WorkflowEvent> GenerateEventSequence(Random rng, int count)
    {
        var events = new List<WorkflowEvent>();
        Guid? stageId = null;
        string? stageName = null;

        for (var i = 1; i <= count; i++)
        {
            var kind = rng.Next(4);
            switch (kind)
            {
                case 0:
                {
                    stageId = Guid.NewGuid();
                    stageName = $"S{rng.Next(1, 9)}";
                    events.Add(new WorkflowEvent
                    {
                        EventType = WorkflowEventType.StageTransitioned,
                        ToStageId = stageId,
                        ToStageName = stageName,
                        Data = JsonDocument.Parse("{}"),
                        SequenceNumber = i,
                        CandidateId = Guid.NewGuid(),
                        UserId = Guid.NewGuid(),
                        UserName = "gen"
                    });
                    break;
                }
                case 1:
                {
                    events.Add(new WorkflowEvent
                    {
                        EventType = WorkflowEventType.StatusUpdated,
                        ToStageId = stageId,
                        ToStageName = stageName,
                        Data = JsonDocument.Parse(JsonSerializer.Serialize(new
                        {
                            trackName = rng.Next(2) == 0 ? "medical" : "visa",
                            newValue = rng.Next(2) == 0 ? "Fit" : "Issued"
                        })),
                        SequenceNumber = i,
                        CandidateId = Guid.NewGuid(),
                        UserId = Guid.NewGuid(),
                        UserName = "gen"
                    });
                    break;
                }
                case 2:
                {
                    events.Add(new WorkflowEvent
                    {
                        EventType = WorkflowEventType.MirrorViewActivated,
                        ToStageId = Guid.NewGuid(),
                        ToStageName = "LMIS",
                        Data = JsonDocument.Parse("{}"),
                        SequenceNumber = i,
                        CandidateId = Guid.NewGuid(),
                        UserId = Guid.NewGuid(),
                        UserName = "gen"
                    });
                    break;
                }
                default:
                {
                    var target = Guid.NewGuid();
                    events.Add(new WorkflowEvent
                    {
                        EventType = WorkflowEventType.MirrorViewActivated,
                        ToStageId = target,
                        ToStageName = "X",
                        Data = JsonDocument.Parse("{}"),
                        SequenceNumber = i,
                        CandidateId = Guid.NewGuid(),
                        UserId = Guid.NewGuid(),
                        UserName = "gen"
                    });
                    if (rng.Next(2) == 0)
                    {
                        i++;
                        if (i > count) break;
                        events.Add(new WorkflowEvent
                        {
                            EventType = WorkflowEventType.MirrorViewDeactivated,
                            ToStageId = target,
                            ToStageName = "X",
                            Data = JsonDocument.Parse("{}"),
                            SequenceNumber = i,
                            CandidateId = Guid.NewGuid(),
                            UserId = Guid.NewGuid(),
                            UserName = "gen"
                        });
                    }
                    break;
                }
            }
        }

        return events;
    }
}
