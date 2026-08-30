using FluentAssertions;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Workflow;
using System.Text.Json;
using Xunit;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// These pin down what an event replay can and cannot tell you about board membership.
///
/// The answer is: not enough on its own. Entering a stage and leaving it are both StageTransitioned
/// events carrying no visibility payload, so a replay only ever sees mirror activations and
/// deactivations. That is why the candidate's stored column is the authority for which boards a
/// candidate appears on, and why the reader must take that column rather than merge into it —
/// merging could only add, so candidates accumulated every board they had ever been on.
///
/// The pre-existing model test covered the engine's in-memory cleanup, which was always correct.
/// The gap was between that cleanup and what a later read could reconstruct.
/// </summary>
public class WorkflowReplayTests
{
    private static readonly Guid Embassy = Guid.NewGuid();
    private static readonly Guid Lmis = Guid.NewGuid();
    private static readonly Guid CaseExec = Guid.NewGuid();

    private static WorkflowEvent Mirror(long seq, WorkflowEventType type, Guid target) => new()
    {
        SequenceNumber = seq,
        EventType = type,
        FromStageId = Embassy,
        ToStageId = target,
        Data = JsonDocument.Parse("{}")
    };

    private static WorkflowEvent Transition(long seq, Guid from, Guid to) => new()
    {
        SequenceNumber = seq,
        EventType = WorkflowEventType.StageTransitioned,
        FromStageId = from,
        ToStageId = to,
        Data = JsonDocument.Parse("{}")
    };

    [Fact]
    public void MirrorActivationShowsTheCandidateOnTheTargetBoard()
    {
        var state = WorkflowState.Initial();
        state.Apply(Mirror(1, WorkflowEventType.MirrorViewActivated, Lmis));

        state.VisibleInStages.Should().Contain(Lmis);
    }

    [Fact]
    public void MovingOffAStageDoesNotByItselfClearItsMirrors()
    {
        // The fact the whole design rests on: a transition says nothing about visibility, so a
        // replay cannot know the candidate stopped being mirrored here.
        var state = WorkflowState.Initial();
        state.Apply(Mirror(1, WorkflowEventType.MirrorViewActivated, Lmis));
        state.Apply(Transition(2, Embassy, Lmis));

        state.VisibleInStages.Should().Contain(Lmis);
    }

    [Fact]
    public void RecordedDeactivationTakesTheCandidateOffTheBoard()
    {
        var state = WorkflowState.Initial();
        state.Apply(Mirror(1, WorkflowEventType.MirrorViewActivated, Lmis));
        state.Apply(Mirror(2, WorkflowEventType.MirrorViewActivated, CaseExec));
        state.Apply(Mirror(3, WorkflowEventType.MirrorViewDeactivated, CaseExec));
        state.Apply(Mirror(4, WorkflowEventType.MirrorViewDeactivated, Lmis));
        state.Apply(Transition(5, Embassy, Lmis));

        // Deactivations are the only thing that removes a stage from a replay, which is what keeps
        // the timeline honest about when a candidate left each board.
        state.VisibleInStages.Should().BeEmpty();
    }

    [Fact]
    public void ReplayingTheWholeEmbassyToArrivalRunLeavesNoStaleBoards()
    {
        var state = WorkflowState.Initial();
        state.Apply(Mirror(1, WorkflowEventType.MirrorViewActivated, Lmis));       // fit + book done
        state.Apply(Mirror(2, WorkflowEventType.MirrorViewActivated, CaseExec));   // visa submitted
        state.Apply(Mirror(3, WorkflowEventType.MirrorViewDeactivated, CaseExec)); // visa issued
        state.Apply(Mirror(4, WorkflowEventType.MirrorViewDeactivated, Lmis));     // transition clears it
        state.Apply(Transition(5, Embassy, Lmis));

        state.VisibleInStages.Should().NotContain(Lmis);
        state.VisibleInStages.Should().NotContain(CaseExec);
        state.VisibleInStages.Should().NotContain(Embassy);
    }
}
