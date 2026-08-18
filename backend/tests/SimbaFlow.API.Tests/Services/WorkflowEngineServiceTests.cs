using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Tests.Services;

public class WorkflowEngineServiceTests
{
    private static (TenantDbContext Db, WorkflowEngineService Engine, Guid UserId) CreateSut(
        string? userName = "tester",
        IReadOnlyList<string>? roles = null)
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase($"wf_{Guid.NewGuid()}")
            .Options;

        var userId = Guid.NewGuid();
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(userId.ToString());
        currentUser.UserName.Returns(userName);
        currentUser.TenantId.Returns(Guid.NewGuid());
        currentUser.Roles.Returns(roles ?? ["AgencyOwner"]);

        var db = new TenantDbContext(options, currentUser);
        var engine = new WorkflowEngineService(db, currentUser);
        return (db, engine, userId);
    }

    private static async Task<(WorkflowDefinition Def, WorkflowStage Intake, WorkflowStage Embassy, WorkflowTransitionRule Rule, Candidate Candidate)>
        SeedBasicAsync(TenantDbContext db)
    {
        var def = new WorkflowDefinition
        {
            TenantId = Guid.NewGuid(),
            Name = "Test",
            IsActive = true
        };

        var intake = new WorkflowStage
        {
            WorkflowDefinitionId = def.Id,
            Name = "Intake",
            SortOrder = 1,
            IsInitialStage = true,
            StageType = StageType.Simple
        };
        var embassy = new WorkflowStage
        {
            WorkflowDefinitionId = def.Id,
            Name = "Embassy",
            SortOrder = 2,
            StageType = StageType.ParallelTrack
        };
        embassy.Statuses =
        [
            new WorkflowStageStatus { WorkflowStageId = embassy.Id, Name = "Pending", TrackName = "medical", SortOrder = 1 },
            new WorkflowStageStatus { WorkflowStageId = embassy.Id, Name = "Fit", TrackName = "medical", SortOrder = 2, IsTerminal = true },
            new WorkflowStageStatus { WorkflowStageId = embassy.Id, Name = "Pending", TrackName = "tasheer", SortOrder = 1 },
            new WorkflowStageStatus { WorkflowStageId = embassy.Id, Name = "Book Done", TrackName = "tasheer", SortOrder = 2, IsTerminal = true }
        ];
        embassy.ParallelTracks =
        [
            new ParallelTrackDefinition { WorkflowStageId = embassy.Id, TrackName = "medical", CompletionStatus = "Fit", SortOrder = 1 },
            new ParallelTrackDefinition { WorkflowStageId = embassy.Id, TrackName = "tasheer", CompletionStatus = "Book Done", SortOrder = 2 }
        ];
        embassy.MirrorViewRules =
        [
            new MirrorViewRule
            {
                WorkflowStageId = embassy.Id,
                TargetStageId = Guid.NewGuid(), // placeholder; set after LMIS created if needed
                IsActive = true,
                Conditions = JsonDocument.Parse("""
                    {"operator":"AND","rules":[
                      {"field":"medical","op":"eq","value":"Fit"},
                      {"field":"tasheer","op":"eq","value":"Book Done"}
                    ]}
                    """)
            }
        ];

        var lmis = new WorkflowStage
        {
            WorkflowDefinitionId = def.Id,
            Name = "LMIS",
            SortOrder = 3,
            StageType = StageType.MilestoneSequence
        };
        embassy.MirrorViewRules.First().TargetStageId = lmis.Id;

        var rule = new WorkflowTransitionRule
        {
            WorkflowDefinitionId = def.Id,
            SourceStageId = intake.Id,
            TargetStageId = embassy.Id,
            ButtonLabel = "To Embassy",
            SortOrder = 1,
            Conditions = JsonDocument.Parse("{}"),
            RequiredFields = [],
            AllowedRoles = [],
            RemoveFromSource = true,
            IsActive = true,
            SourceStage = intake,
            TargetStage = embassy
        };

        def.Stages = [intake, embassy, lmis];
        def.TransitionRules = [rule];

        var candidate = new Candidate
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            PassportNumber = "P12345",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = Gender.Female,
            CurrentStageId = intake.Id,
            CurrentStageName = intake.Name,
            Status = CandidateStatus.Active
        };

        db.WorkflowDefinitions.Add(def);
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync();

        return (def, intake, embassy, rule, candidate);
    }

    [Fact]
    public async Task GetCurrentState_FallsBackToCandidateDenormalizedFields()
    {
        var (db, engine, _) = CreateSut();
        var (_, intake, _, _, candidate) = await SeedBasicAsync(db);

        var state = await engine.GetCurrentStateAsync(candidate.Id);

        state.StageId.Should().Be(intake.Id);
        state.StageName.Should().Be("Intake");
    }

    [Fact]
    public async Task ExecuteTransition_MovesCandidateAndAppendsEvent()
    {
        var (db, engine, userId) = CreateSut();
        var (_, _, embassy, rule, candidate) = await SeedBasicAsync(db);

        var result = await engine.ExecuteTransitionAsync(
            candidate.Id, rule.Id, userId, "tester");

        result.IsSuccess.Should().BeTrue(result.Error);

        var updated = await db.Candidates.FindAsync(candidate.Id);
        updated!.CurrentStageId.Should().Be(embassy.Id);
        updated.CurrentStageName.Should().Be("Embassy");

        var events = await db.WorkflowEvents.Where(e => e.CandidateId == candidate.Id).ToListAsync();
        events.Should().ContainSingle(e => e.EventType == WorkflowEventType.StageTransitioned);
    }

    [Fact]
    public async Task ExecuteTransition_WrongStage_Fails()
    {
        var (db, engine, userId) = CreateSut();
        var (_, _, embassy, rule, candidate) = await SeedBasicAsync(db);
        candidate.CurrentStageId = embassy.Id;
        candidate.CurrentStageName = embassy.Name;
        await db.SaveChangesAsync();

        var result = await engine.ExecuteTransitionAsync(
            candidate.Id, rule.Id, userId, "tester");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("source stage");
    }

    [Fact]
    public async Task UpdateStatus_UpdatesValuesAndMayActivateMirror()
    {
        var (db, engine, userId) = CreateSut();
        var (_, _, embassy, _, candidate) = await SeedBasicAsync(db);
        candidate.CurrentStageId = embassy.Id;
        candidate.CurrentStageName = embassy.Name;
        await db.SaveChangesAsync();

        var r1 = await engine.UpdateStatusAsync(candidate.Id, "medical", "Fit", userId, "tester");
        r1.IsSuccess.Should().BeTrue(r1.Error);

        var r2 = await engine.UpdateStatusAsync(candidate.Id, "tasheer", "Book Done", userId, "tester");
        r2.IsSuccess.Should().BeTrue(r2.Error);

        var updated = await db.Candidates.FindAsync(candidate.Id);
        updated!.VisibleInStages.Should().NotBeEmpty();

        var state = await engine.GetCurrentStateAsync(candidate.Id);
        state.StatusValues["medical"].Should().Be("Fit");
        state.StatusValues["tasheer"].Should().Be("Book Done");
        state.VisibleInStages.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAvailableActions_ReturnsEnabledTransitionFromCurrentStage()
    {
        var (db, engine, _) = CreateSut();
        var (_, _, _, rule, candidate) = await SeedBasicAsync(db);

        var actions = await engine.GetAvailableActionsAsync(candidate.Id, ["AgencyOwner"]);

        actions.Should().ContainSingle(a => a.TransitionRuleId == rule.Id && a.IsEnabled);
    }

    [Fact]
    public async Task ExecuteTransition_RoleRestricted_Fails()
    {
        var (db, engine, userId) = CreateSut(roles: ["FieldAgent"]);
        var (_, _, _, rule, candidate) = await SeedBasicAsync(db);
        rule.AllowedRoles = ["AgencyOwner"];
        await db.SaveChangesAsync();

        var result = await engine.ExecuteTransitionAsync(
            candidate.Id, rule.Id, userId, "agent");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("role");
    }
}
