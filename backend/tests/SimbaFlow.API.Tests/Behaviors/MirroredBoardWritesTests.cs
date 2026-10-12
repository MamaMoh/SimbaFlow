using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// A mirrored row can be worked on from the board it is mirrored onto.
///
/// That is the entire purpose of a mirror: the LMIS desk registers the insurance while the
/// embassy is still finishing the medical, and the candidate does not leave the Embassy board
/// to let them. But the engine validated the track against the one stage the candidate was
/// standing in, so every LMIS action on a mirrored row came back "Unknown track 'insurance' for
/// stage 'Embassy'" — the endpoint said a mirrored row was fair game and the engine disagreed.
/// </summary>
public class MirroredBoardWritesTests : IDisposable
{
    private readonly TenantDbContext _context;
    private readonly WorkflowEngineService _engine;
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly Guid _userId = Guid.NewGuid();

    private WorkflowStage _embassy = null!;
    private WorkflowStage _lmis = null!;
    private Candidate _candidate = null!;

    public MirroredBoardWritesTests()
    {
        _context = new TenantDbContext(
            new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Substitute.For<ICurrentUserService>());

        _user.UserId.Returns(_userId.ToString());
        _user.UserName.Returns("desk");
        _engine = new WorkflowEngineService(_context, _user);

        var definition = Guid.NewGuid();

        // Embassy runs its tracks side by side; LMIS owns insurance and milestone. Between them
        // they are the pair the real pipeline mirrors.
        _embassy = new WorkflowStage
        {
            Id = Guid.NewGuid(), WorkflowDefinitionId = definition,
            Name = "Embassy", SortOrder = 3, StageType = StageType.ParallelTrack,
            ParallelTracks =
            [
                new ParallelTrackDefinition { TrackName = "medical", CompletionStatus = "Fit", SortOrder = 1 },
                new ParallelTrackDefinition { TrackName = "tasheer", CompletionStatus = "Book Done", SortOrder = 2 },
            ],
            Statuses =
            [
                Status("Pending", "medical"), Status("Fit", "medical"),
                Status("Pending", "tasheer"), Status("Book Done", "tasheer"),
            ],
        };

        _lmis = new WorkflowStage
        {
            Id = Guid.NewGuid(), WorkflowDefinitionId = definition,
            Name = "LMIS", SortOrder = 5, StageType = StageType.MilestoneSequence,
            Statuses =
            [
                Status("Insurance Unpaid", "insurance"),
                Status("Insurance Paid", "insurance"),
                Status("Available", "insurance"),
                Status("Uploaded", "milestone"),
                Status("Issued", "milestone"),
            ],
        };

        _candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            FirstName = "Sample", LastName = "Candidate",
            PassportNumber = "EP0202020",
            // Standing in Embassy, showing on LMIS — exactly what the mirror produces.
            CurrentStageId = _embassy.Id,
            VisibleInStages = [_lmis.Id],
        };

        _context.WorkflowStages.AddRange(_embassy, _lmis);
        _context.Candidates.Add(_candidate);
        _context.SaveChanges();
    }

    private static WorkflowStageStatus Status(string name, string track) =>
        new() { Id = Guid.NewGuid(), Name = name, TrackName = track, SortOrder = 1 };

    [Fact]
    public async Task TheMirroredBoardsOwnTrackCanBeWritten()
    {
        var result = await _engine.UpdateStatusAsync(
            _candidate.Id, "insurance", "Insurance Paid", _userId, "desk");

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    [Fact]
    public async Task WritingItDoesNotMoveThemOffTheBoardTheyAreStandingOn()
    {
        // The whole point is that the embassy keeps them while LMIS works. If this ever starts
        // relocating candidates, the Embassy board quietly empties.
        await _engine.UpdateStatusAsync(_candidate.Id, "insurance", "Insurance Paid", _userId, "desk");

        var after = await _context.Candidates.FirstAsync(c => c.Id == _candidate.Id);
        after.CurrentStageId.Should().Be(_embassy.Id);
        after.VisibleInStages.Should().Contain(_lmis.Id);
    }

    [Fact]
    public async Task TheStageTheyAreStandingInStillWorks()
    {
        var result = await _engine.UpdateStatusAsync(
            _candidate.Id, "medical", "Fit", _userId, "desk");

        result.IsSuccess.Should().BeTrue(result.Error);
    }

    [Fact]
    public async Task AValueThatBoardDoesNotOfferIsStillRefused()
    {
        // Reaching the mirrored board's tracks must not mean reaching past its statuses.
        var result = await _engine.UpdateStatusAsync(
            _candidate.Id, "insurance", "Completely Made Up", _userId, "desk");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid status");
    }

    [Fact]
    public async Task ATrackNoBoardTheyAreOnDeclaresIsStillRefused()
    {
        var result = await _engine.UpdateStatusAsync(
            _candidate.Id, "shoe_size", "11", _userId, "desk");

        result.IsSuccess.Should().BeFalse();
        // And the refusal says where it looked, rather than naming one board of the two.
        result.Error.Should().Contain("Embassy").And.Contain("LMIS");
    }

    [Fact]
    public async Task ACandidateOnOneBoardOnlyIsUnaffected()
    {
        var alone = new Candidate
        {
            Id = Guid.NewGuid(), FirstName = "Only", LastName = "Here",
            PassportNumber = "EP0303030", CurrentStageId = _embassy.Id,
        };
        _context.Candidates.Add(alone);
        await _context.SaveChangesAsync();

        var result = await _engine.UpdateStatusAsync(alone.Id, "insurance", "Insurance Paid", _userId, "desk");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("insurance").And.Contain("Embassy");
    }

    public void Dispose() => _context.Dispose();
}
