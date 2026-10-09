using FluentAssertions;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// Who may delete a candidate, and from where.
///
/// The rule protects a colleague's work rather than the record: once a candidate has moved on,
/// the desk standing over them is not the one looking at the candidates list.
/// </summary>
public class CandidateRemovalTests
{
    [Fact]
    public void ACandidateStillAtTheFirstStageCanBeDeleted()
    {
        CandidateRemoval.Refusal("Intake", isInitialStage: true).Should().BeNull();
    }

    [Fact]
    public void ACandidateOnNoStageAtAllCanBeDeleted()
    {
        // No workflow configured yet. There is no other desk to take them away from.
        CandidateRemoval.Refusal(null, isInitialStage: false).Should().BeNull();
        CandidateRemoval.Refusal("   ", isInitialStage: false).Should().BeNull();
    }

    [Fact]
    public void ACandidateFurtherDownThePipelineCannotBeDeletedHere()
    {
        var refusal = CandidateRemoval.Refusal("Embassy", isInitialStage: false);

        refusal.Should().NotBeNull();
        refusal.Should().Contain("Embassy", "the refusal names the desk that holds them");
        refusal.Should().Contain("Move them back", "and says what to do instead");
    }
}
