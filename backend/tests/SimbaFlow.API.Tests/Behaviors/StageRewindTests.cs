using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Workflow.Commands;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// Putting a candidate back in the stage they came from.
///
/// A mis-click used to be uncorrectable: the only way back was all the way to intake, which threw
/// away every stage the candidate had legitimately passed through, so people left them in the
/// wrong stage instead and the boards stopped meaning anything.
///
/// The real engine is used rather than a stand-in, because the thing most likely to be wrong is
/// that a cleared status comes back: state is replayed from the event stream, and a value only
/// removed from the denormalised column reappears on the next read.
/// </summary>
public class StageRewindTests : IDisposable
{
    private readonly TenantDbContext _context;
    private readonly WorkflowEngineService _engine;
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly Guid _userId = Guid.NewGuid();

    private WorkflowStage _intake = null!;
    private WorkflowStage _embassy = null!;
    private Candidate _candidate = null!;
    private long _sequence;

    public StageRewindTests()
    {
        _context = new TenantDbContext(
            new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Substitute.For<ICurrentUserService>());

        _user.UserId.Returns(_userId.ToString());
        _user.UserName.Returns("desk");
        _engine = new WorkflowEngineService(_context, _user);
    }

    private void GivenPipeline()
    {
        var definition = Guid.NewGuid();

        _intake = new WorkflowStage
        {
            Id = Guid.NewGuid(), WorkflowDefinitionId = definition,
            Name = "New Contracts", SortOrder = 1, IsInitialStage = true,
        };

        _embassy = new WorkflowStage
        {
            Id = Guid.NewGuid(), WorkflowDefinitionId = definition,
            Name = "Embassy", SortOrder = 2, StageType = StageType.ParallelTrack,
        };
        _embassy.ParallelTracks.Add(new ParallelTrackDefinition
        {
            WorkflowStageId = _embassy.Id, TrackName = "medical", CompletionStatus = "Fit",
        });
        _embassy.ParallelTracks.Add(new ParallelTrackDefinition
        {
            WorkflowStageId = _embassy.Id, TrackName = "tasheer", CompletionStatus = "Done",
        });

        _context.WorkflowStages.AddRange(_intake, _embassy);

        _candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            FirstName = "ALMAZ",
            LastName = "KEBEDE",
            PassportNumber = "EP0000001",
            DateOfBirth = new DateOnly(2002, 7, 9),
            CurrentStageId = _embassy.Id,
            CurrentStageName = _embassy.Name,
            VisibleInStages = [_embassy.Id],
        };
        _context.Candidates.Add(_candidate);

        Event(WorkflowEventType.Registered, to: _intake);
        Event(WorkflowEventType.StageTransitioned, from: _intake, to: _embassy);
        _context.SaveChanges();
    }

    private void Event(
        WorkflowEventType type, WorkflowStage? from = null, WorkflowStage? to = null, object? data = null)
    {
        _context.WorkflowEvents.Add(new WorkflowEvent
        {
            CandidateId = _candidate.Id,
            SequenceNumber = ++_sequence,
            EventType = type,
            FromStageId = from?.Id,
            FromStageName = from?.Name,
            ToStageId = to?.Id,
            ToStageName = to?.Name,
            Data = JsonDocument.Parse(JsonSerializer.Serialize(data ?? new { })),
            UserId = _userId,
            UserName = "desk",
        });
    }

    private void GivenStatus(string track, string value)
    {
        Event(WorkflowEventType.StatusUpdated,
            data: new { trackName = track, oldValue = (string?)null, newValue = value });
        _context.SaveChanges();
    }

    private Task<SimbaFlow.Application.Common.Models.Result<MoveBackPreviewDto>> Preview() =>
        new GetMoveBackPreviewHandler(_context, _engine)
            .Handle(new GetMoveBackPreviewQuery(_candidate.Id), default);

    private Task<SimbaFlow.Application.Common.Models.Result<MoveBackPreviewDto>> MoveBack(string? reason = null) =>
        new MoveBackStageHandler(_context, _engine, _user)
            .Handle(new MoveBackStageCommand(_candidate.Id, reason), default);

    [Fact]
    public async Task ThePreviewNamesWhereTheyWouldGoAndWhatItCosts()
    {
        GivenPipeline();
        GivenStatus("medical", "Fit");
        GivenStatus("tasheer", "Booked");

        var preview = await Preview();

        preview.IsSuccess.Should().BeTrue();
        preview.Data!.FromStageName.Should().Be("Embassy");
        preview.Data.ToStageName.Should().Be("New Contracts");
        preview.Data.Clears.Select(c => $"{c.Track}={c.Value}")
            .Should().BeEquivalentTo("medical=Fit", "tasheer=Booked");
    }

    [Fact]
    public async Task ThePreviewChangesNothing()
    {
        GivenPipeline();
        GivenStatus("medical", "Fit");

        await Preview();

        _candidate.CurrentStageId.Should().Be(_embassy.Id);
        (await _engine.GetCurrentStateAsync(_candidate.Id)).StatusValues["medical"].Should().Be("Fit");
    }

    [Fact]
    public async Task TheCandidateGoesBackToTheStageTheyCameFrom()
    {
        GivenPipeline();

        (await MoveBack()).IsSuccess.Should().BeTrue();

        _candidate.CurrentStageId.Should().Be(_intake.Id);
        _candidate.CurrentStageName.Should().Be("New Contracts");
        _candidate.VisibleInStages.Should().Equal(_intake.Id);
    }

    [Fact]
    public async Task TheStagesStatusesAreGoneAndStayGone()
    {
        // The one worth pinning. State is replayed from the stream, so clearing the column alone
        // would read back as though nothing had happened.
        GivenPipeline();
        GivenStatus("medical", "Fit");
        GivenStatus("tasheer", "Booked");

        await MoveBack();

        var state = await _engine.GetCurrentStateAsync(_candidate.Id);
        state.StatusValues.GetValueOrDefault("medical", "").Should().BeEmpty();
        state.StatusValues.GetValueOrDefault("tasheer", "").Should().BeEmpty();
        state.StageId.Should().Be(_intake.Id);
    }

    [Fact]
    public async Task TheMoveIsOnTheTimelineLikeAnyOther()
    {
        // A correction is still something that happened to this candidate. Hiding it leaves the
        // timeline unable to explain why they are back where they were.
        GivenPipeline();

        await MoveBack("moved by mistake");

        var last = await _context.WorkflowEvents
            .Where(e => e.CandidateId == _candidate.Id)
            .OrderByDescending(e => e.SequenceNumber)
            .FirstAsync();

        last.EventType.Should().Be(WorkflowEventType.StageTransitioned);
        last.FromStageId.Should().Be(_embassy.Id);
        last.ToStageId.Should().Be(_intake.Id);
        last.Notes.Should().Be("moved by mistake");
        last.UserName.Should().Be("desk");
    }

    [Fact]
    public async Task TheCandidatesOwnRecordIsUntouched()
    {
        // Stage progress is cleared; facts about the person are not. A visa number does not stop
        // being true because somebody corrected which board they are on.
        GivenPipeline();
        _candidate.VisaNumber = "1900111222";
        _candidate.SponsorName = "SAAD ABDULLAH MOHAMMED ALHARBI";
        await _context.SaveChangesAsync();

        await MoveBack();

        _candidate.VisaNumber.Should().Be("1900111222");
        _candidate.SponsorName.Should().Be("SAAD ABDULLAH MOHAMMED ALHARBI");
    }

    [Fact]
    public async Task ACandidateWhoHasNeverMovedHasNowhereToGoBackTo()
    {
        GivenPipeline();
        _candidate.CurrentStageId = _intake.Id;
        _candidate.CurrentStageName = _intake.Name;
        await _context.SaveChangesAsync();

        var result = await Preview();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no earlier stage");
    }

    [Fact]
    public async Task AStageThatHasSinceBeenDeletedIsRefusedRatherThanGuessedAt()
    {
        GivenPipeline();
        _intake.IsDeleted = true;
        await _context.SaveChangesAsync();

        var result = await Preview();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("no longer exists");
    }

    [Fact]
    public async Task GoingBackTwiceWalksBackTwoStages()
    {
        // Each move records where it came from, so undoing one leaves the one before it intact.
        var ticket = new WorkflowStage
        {
            Id = Guid.NewGuid(), Name = "Ticket", SortOrder = 3,
        };
        GivenPipeline();
        _context.WorkflowStages.Add(ticket);
        Event(WorkflowEventType.StageTransitioned, from: _embassy, to: ticket);
        _candidate.CurrentStageId = ticket.Id;
        _candidate.CurrentStageName = ticket.Name;
        await _context.SaveChangesAsync();

        (await MoveBack()).Data!.ToStageName.Should().Be("Embassy");
        (await MoveBack()).Data!.ToStageName.Should().Be("New Contracts");
    }

    public void Dispose() => _context.Dispose();
}
