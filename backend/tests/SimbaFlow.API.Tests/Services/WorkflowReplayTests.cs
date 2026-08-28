using FluentAssertions;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Workflow;
using System.Text.Json;
using Xunit;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Board membership is rebuilt by replaying the event stream, and the reader then unions the
/// candidate's stored column into the result. That union means the visible set can only ever grow
/// during a replay: anything the engine removed without writing an event comes back on the next
/// read, and the next status update persists it. So every removal has to be in the stream.
///
/// These cover the replay itself. The existing model test covered the in-memory cleanup, which was
/// always right — the gap was between that cleanup and what the stream recorded.
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
        // A StageTransitioned event carries no visibility information, so the engine has to write
        // the deactivations explicitly. This is the fact the fix depends on — if it ever changes,
        // the extra events become redundant rather than load-bearing.
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

        // Nothing stale survives, so the union with the stored column is a no-op rather than a
        // resurrection of every board this candidate ever appeared on.
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
