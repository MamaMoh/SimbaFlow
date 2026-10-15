using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Candidates.Commands;
using SimbaFlow.API.Features.Embassy.Commands;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Bringing a candidate back from the Inactive list.
///
/// An Unfit medical and Delete both ended a file for good: one misclick, and the only way back
/// was a database edit.
/// </summary>
public class ReactivateCandidateTests
{
    private static (TenantDbContext Db, WorkflowEngineService Engine, ICurrentUserService User) CreateSut(
        params string[] permissions)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase($"reactivate_{Guid.NewGuid()}")
            .Options;

        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(Guid.NewGuid().ToString());
        user.UserName.Returns("tester");
        user.TenantId.Returns(Guid.NewGuid());
        user.Roles.Returns(["AgencyOwner"]);
        user.HasPermission(Arg.Any<string>()).Returns(ci => permissions.Contains(ci.Arg<string>()));

        var db = new TenantDbContext(options, user);
        return (db, new WorkflowEngineService(db, user), user);
    }

    private static async Task<(WorkflowStage Intake, WorkflowStage Embassy, Candidate Candidate)> SeedAsync(
        TenantDbContext db)
    {
        var def = new WorkflowDefinition { TenantId = Guid.NewGuid(), Name = "Test", IsActive = true };
        var intake = new WorkflowStage
        {
            WorkflowDefinitionId = def.Id, Name = "Intake", SortOrder = 1,
            IsInitialStage = true, StageType = StageType.Simple
        };
        var embassy = new WorkflowStage
        {
            WorkflowDefinitionId = def.Id, Name = "Embassy", SortOrder = 2, StageType = StageType.ParallelTrack
        };
        embassy.Statuses =
        [
            new() { WorkflowStageId = embassy.Id, Name = "Pending", TrackName = "medical", SortOrder = 1 },
            new() { WorkflowStageId = embassy.Id, Name = "Booked", TrackName = "medical", SortOrder = 2 },
            new() { WorkflowStageId = embassy.Id, Name = "Fit", TrackName = "medical", SortOrder = 3, IsTerminal = true },
            new() { WorkflowStageId = embassy.Id, Name = "Unfit", TrackName = "medical", SortOrder = 4, IsTerminal = true },
        ];
        def.Stages = [intake, embassy];

        var candidate = new Candidate
        {
            FirstName = "Mekiya",
            LastName = "Kebede",
            PassportNumber = "EQ2623576",
            DateOfBirth = new DateOnly(1995, 5, 5),
            Gender = Gender.Female,
            CurrentStageId = embassy.Id,
            CurrentStageName = embassy.Name,
            VisibleInStages = [embassy.Id],
            Status = CandidateStatus.Active,
            StageEnteredAt = DateTime.UtcNow
        };

        db.WorkflowDefinitions.Add(def);
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync();
        return (intake, embassy, candidate);
    }

    private static async Task RecordUnfitAsync(
        TenantDbContext db, WorkflowEngineService engine, ICurrentUserService user, Guid candidateId)
    {
        await engine.UpdateStatusAsync(candidateId, "medical", "Booked", Guid.Parse(user.UserId!), "tester");
        var result = await new RecordMedicalResultHandler(db, engine, user, Substitute.For<ICandidateNotifier>())
            .Handle(new RecordMedicalResultCommand(candidateId, "Unfit"), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AnUnfitCandidateComesBackWithTheMedicalBookedAgain()
    {
        var (db, engine, user) = CreateSut("embassy.update");
        var (_, _, candidate) = await SeedAsync(db);
        await RecordUnfitAsync(db, engine, user, candidate.Id);
        (await db.Candidates.FindAsync(candidate.Id))!.Status.Should().Be(CandidateStatus.Inactive);

        var result = await new ReactivateCandidateHandler(db, engine, user)
            .Handle(new ReactivateCandidateCommand(candidate.Id, "wrong row"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var reloaded = await db.Candidates.FindAsync(candidate.Id);
        reloaded!.Status.Should().Be(CandidateStatus.Active);
        (await engine.GetCurrentStateAsync(candidate.Id)).StatusValues["medical"].Should().Be("Booked",
            "the desk records the right result from where it was before");

        var last = await db.WorkflowEvents.Where(e => e.CandidateId == candidate.Id)
            .OrderByDescending(e => e.SequenceNumber).FirstAsync();
        last.EventType.Should().Be(WorkflowEventType.Reactivated);
        last.Notes.Should().Contain("wrong row");
        db.WorkflowEvents.Select(e => e.SequenceNumber).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task AnUnfitCandidateAlreadyBackInIntakeIsSimplyActiveAgain()
    {
        var (db, engine, user) = CreateSut("candidate.update");
        var (intake, _, candidate) = await SeedAsync(db);
        await RecordUnfitAsync(db, engine, user, candidate.Id);
        candidate.CurrentStageId = intake.Id;
        candidate.CurrentStageName = intake.Name;
        candidate.VisibleInStages = [intake.Id];
        await db.SaveChangesAsync();

        var result = await new ReactivateCandidateHandler(db, engine, user)
            .Handle(new ReactivateCandidateCommand(candidate.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        (await db.Candidates.FindAsync(candidate.Id))!.Status.Should().Be(CandidateStatus.Active);
    }

    [Fact]
    public async Task ARemovedCandidateIsRestored()
    {
        var (db, engine, user) = CreateSut("candidate.delete");
        var (intake, _, candidate) = await SeedAsync(db);
        candidate.CurrentStageId = intake.Id;
        candidate.CurrentStageName = intake.Name;
        await db.SaveChangesAsync();

        (await new DeleteCandidateHandler(db, user)
            .Handle(new DeleteCandidateCommand(candidate.Id), CancellationToken.None)).IsSuccess.Should().BeTrue();

        var result = await new ReactivateCandidateHandler(db, engine, user)
            .Handle(new ReactivateCandidateCommand(candidate.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var reloaded = await db.Candidates.FindAsync(candidate.Id);
        reloaded!.IsDeleted.Should().BeFalse();
        reloaded.Status.Should().Be(CandidateStatus.Active);
        reloaded.DeletedBy.Should().BeNull();

        var types = await db.WorkflowEvents.Where(e => e.CandidateId == candidate.Id)
            .OrderBy(e => e.SequenceNumber).Select(e => e.EventType).ToListAsync();
        types.Should().ContainInOrder(WorkflowEventType.Archived, WorkflowEventType.Reactivated);
    }

    [Fact]
    public async Task RestoringARemovedCandidateTakesThePermissionThatRemovedIt()
    {
        var (db, engine, user) = CreateSut("embassy.update", "candidate.update");
        var (_, _, candidate) = await SeedAsync(db);
        candidate.IsDeleted = true;
        candidate.Status = CandidateStatus.Archived;
        await db.SaveChangesAsync();

        var result = await new ReactivateCandidateHandler(db, engine, user)
            .Handle(new ReactivateCandidateCommand(candidate.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
        (await db.Candidates.FindAsync(candidate.Id))!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task AnActiveCandidateHasNothingToUndo()
    {
        var (db, engine, user) = CreateSut("candidate.delete", "candidate.update");
        var (_, _, candidate) = await SeedAsync(db);

        var result = await new ReactivateCandidateHandler(db, engine, user)
            .Handle(new ReactivateCandidateCommand(candidate.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }
}
