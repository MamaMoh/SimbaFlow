using System.Text.Json;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Embassy.Commands;
using SimbaFlow.API.Features.Embassy.Validators;
using SimbaFlow.API.Features.Lmis.Commands;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Example-based tests for Unit 3 Embassy / LMIS engine + command behaviors (TEST-30–37).
/// </summary>
public class EmbassyLmisServiceTests
{
    private static (TenantDbContext Db, WorkflowEngineService Engine, Guid UserId, ICurrentUserService User)
        CreateSut()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase($"embassy_lmis_{Guid.NewGuid()}")
            .Options;

        var userId = Guid.NewGuid();
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(userId.ToString());
        currentUser.UserName.Returns("tester");
        currentUser.TenantId.Returns(Guid.NewGuid());
        currentUser.Roles.Returns(["AgencyOwner", "EmbassyOfficer", "CaseExecutive"]);

        var db = new TenantDbContext(options, currentUser);
        var engine = new WorkflowEngineService(db, currentUser);
        return (db, engine, userId, currentUser);
    }

    private static async Task<(
        WorkflowStage Embassy,
        WorkflowStage CaseExec,
        WorkflowStage Lmis,
        WorkflowStage Ticket,
        WorkflowTransitionRule ToLmis,
        Candidate Candidate)> SeedUnit3Async(TenantDbContext db)
    {
        var def = new WorkflowDefinition
        {
            TenantId = Guid.NewGuid(),
            Name = "Unit3",
            IsActive = true
        };

        var embassy = new WorkflowStage
        {
            WorkflowDefinitionId = def.Id,
            Name = "Embassy",
            SortOrder = 1,
            IsInitialStage = true,
            StageType = StageType.ParallelTrack
        };
        var caseExec = new WorkflowStage
        {
            WorkflowDefinitionId = def.Id,
            Name = "Case Executive",
            SortOrder = 2,
            StageType = StageType.Simple
        };
        var lmis = new WorkflowStage
        {
            WorkflowDefinitionId = def.Id,
            Name = "LMIS",
            SortOrder = 3,
            StageType = StageType.MilestoneSequence
        };
        var ticket = new WorkflowStage
        {
            WorkflowDefinitionId = def.Id,
            Name = "Ticket",
            SortOrder = 4,
            StageType = StageType.Simple
        };

        embassy.Statuses =
        [
            new() { WorkflowStageId = embassy.Id, Name = "Pending", TrackName = "medical", SortOrder = 1 },
            new() { WorkflowStageId = embassy.Id, Name = "Booked", TrackName = "medical", SortOrder = 2 },
            new() { WorkflowStageId = embassy.Id, Name = "Fit", TrackName = "medical", SortOrder = 3, IsTerminal = true },
            new() { WorkflowStageId = embassy.Id, Name = "Pending", TrackName = "tasheer", SortOrder = 1 },
            new() { WorkflowStageId = embassy.Id, Name = "Booked", TrackName = "tasheer", SortOrder = 2 },
            new() { WorkflowStageId = embassy.Id, Name = "Book Done", TrackName = "tasheer", SortOrder = 3, IsTerminal = true },
            new() { WorkflowStageId = embassy.Id, Name = "Ready", TrackName = "visa", SortOrder = 1 },
            new() { WorkflowStageId = embassy.Id, Name = "Submitted", TrackName = "visa", SortOrder = 2 },
            new() { WorkflowStageId = embassy.Id, Name = "Issued", TrackName = "visa", SortOrder = 3, IsTerminal = true },
            new() { WorkflowStageId = embassy.Id, Name = "Rejected", TrackName = "visa", SortOrder = 4, IsTerminal = true }
        ];
        embassy.MirrorViewRules =
        [
            new MirrorViewRule
            {
                WorkflowStageId = embassy.Id,
                TargetStageId = lmis.Id,
                IsActive = true,
                Conditions = JsonDocument.Parse("""
                    {"operator":"AND","rules":[
                      {"field":"medical","op":"eq","value":"Fit"},
                      {"field":"tasheer","op":"eq","value":"Book Done"}
                    ]}
                    """)
            },
            new MirrorViewRule
            {
                WorkflowStageId = embassy.Id,
                TargetStageId = caseExec.Id,
                IsActive = true,
                Conditions = JsonDocument.Parse("""
                    {"operator":"OR","rules":[
                      {"field":"visa","op":"eq","value":"Ready"},
                      {"field":"visa","op":"eq","value":"Submitted"}
                    ]}
                    """)
            }
        ];

        var toLmis = new WorkflowTransitionRule
        {
            WorkflowDefinitionId = def.Id,
            SourceStageId = embassy.Id,
            TargetStageId = lmis.Id,
            ButtonLabel = "To LMIS",
            SortOrder = 1,
            Conditions = JsonDocument.Parse("""
                {"operator":"AND","rules":[{"field":"visa","op":"eq","value":"Issued"}]}
                """),
            RequiredFields = [],
            AllowedRoles = [],
            RemoveFromSource = true,
            IsActive = true,
            SourceStage = embassy,
            TargetStage = lmis
        };

        def.Stages = [embassy, caseExec, lmis, ticket];
        def.TransitionRules = [toLmis];

        var candidate = new Candidate
        {
            FirstName = "Mekiya",
            LastName = "Kebede",
            PassportNumber = "EQ2623576",
            DateOfBirth = new DateOnly(1995, 5, 5),
            Gender = Gender.Female,
            CurrentStageId = embassy.Id,
            CurrentStageName = embassy.Name,
            Status = CandidateStatus.Active,
            StageEnteredAt = DateTime.UtcNow
        };

        db.WorkflowDefinitions.Add(def);
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync();

        return (embassy, caseExec, lmis, ticket, toLmis, candidate);
    }

    [Fact]
    public async Task MedicalUpdate_DoesNotMutateTasheer_TEST30()
    {
        var (db, engine, userId, _) = CreateSut();
        var (_, _, _, _, _, candidate) = await SeedUnit3Async(db);

        await engine.UpdateStatusAsync(candidate.Id, "tasheer", "Booked", userId, "tester");
        await engine.UpdateStatusAsync(candidate.Id, "medical", "Booked", userId, "tester");
        await engine.UpdateStatusAsync(candidate.Id, "medical", "Fit", userId, "tester");

        var state = await engine.GetCurrentStateAsync(candidate.Id);
        state.StatusValues["medical"].Should().Be("Fit");
        state.StatusValues["tasheer"].Should().Be("Booked");
    }

    [Fact]
    public async Task LmisMirror_ActivatesOnlyWhenFitAndBookDone_TEST31()
    {
        var (db, engine, userId, _) = CreateSut();
        var (_, _, lmis, _, _, candidate) = await SeedUnit3Async(db);

        await engine.UpdateStatusAsync(candidate.Id, "medical", "Fit", userId, "tester");
        var mid = await engine.GetCurrentStateAsync(candidate.Id);
        mid.VisibleInStages.Should().NotContain(lmis.Id);

        await engine.UpdateStatusAsync(candidate.Id, "tasheer", "Book Done", userId, "tester");
        var done = await engine.GetCurrentStateAsync(candidate.Id);
        done.VisibleInStages.Should().Contain(lmis.Id);
        done.StageId.Should().Be(candidate.CurrentStageId); // still Embassy primary after reload
    }

    [Fact]
    public async Task CaseExecutiveMirror_WhenVisaReady_TEST32()
    {
        var (db, engine, userId, _) = CreateSut();
        var (_, caseExec, _, _, _, candidate) = await SeedUnit3Async(db);

        await engine.UpdateStatusAsync(candidate.Id, "medical", "Fit", userId, "tester");
        await engine.UpdateStatusAsync(candidate.Id, "tasheer", "Book Done", userId, "tester");
        await engine.UpdateStatusAsync(candidate.Id, "visa", "Ready", userId, "tester");

        var state = await engine.GetCurrentStateAsync(candidate.Id);
        state.VisibleInStages.Should().Contain(caseExec.Id);
        state.StatusValues["visa"].Should().Be("Ready");
    }

    [Fact]
    public async Task MilestoneSkip_Rejected_TEST33()
    {
        var (db, engine, userId, user) = CreateSut();
        var (_, _, lmis, _, _, candidate) = await SeedUnit3Async(db);

        candidate.CurrentStageId = lmis.Id;
        candidate.CurrentStageName = lmis.Name;
        candidate.VisibleInStages = [lmis.Id];
        await db.SaveChangesAsync();

        await engine.UpdateStatusAsync(candidate.Id, "insurance", "Available", userId, "tester");

        var handler = new AdvanceLmisMilestoneHandler(db, engine, user);
        var skip = await handler.Handle(
            new AdvanceLmisMilestoneCommand(candidate.Id, "Issued"), CancellationToken.None);

        skip.IsSuccess.Should().BeFalse();
        skip.Error.Should().Contain("Uploaded");

        var state = await engine.GetCurrentStateAsync(candidate.Id);
        state.StatusValues.Should().NotContainKey("milestone");
    }

    [Fact]
    public void RejectionWithoutReason_FailsValidation_TEST34()
    {
        var validator = new RecordVisaOutcomeValidator();
        var result = validator.TestValidate(new RecordVisaOutcomeCommand(
            Guid.NewGuid(), "Rejected", RejectionReason: null));
        result.ShouldHaveValidationErrorFor(x => x.RejectionReason);
    }

    [Fact]
    public async Task Resubmit_PreservesRejectionEventHistory_TEST35()
    {
        var (db, engine, userId, user) = CreateSut();
        var (_, _, _, _, _, candidate) = await SeedUnit3Async(db);

        await engine.UpdateStatusAsync(candidate.Id, "visa", "Submitted", userId, "tester");
        var outcome = new RecordVisaOutcomeHandler(db, engine, user);
        (await outcome.Handle(
            new RecordVisaOutcomeCommand(candidate.Id, "Rejected", "Docs incomplete"),
            CancellationToken.None)).IsSuccess.Should().BeTrue();

        var resubmit = new ResubmitVisaHandler(db, engine, user);
        (await resubmit.Handle(new ResubmitVisaCommand(candidate.Id), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        var events = await db.WorkflowEvents
            .Where(e => e.CandidateId == candidate.Id)
            .OrderBy(e => e.SequenceNumber)
            .ToListAsync();

        events.Should().Contain(e =>
            e.EventType == WorkflowEventType.StatusUpdated &&
            e.Data != null &&
            e.Data.RootElement.ToString().Contains("Rejected", StringComparison.OrdinalIgnoreCase));

        var state = await engine.GetCurrentStateAsync(candidate.Id);
        state.StatusValues["visa"].Should().Be("Ready");
    }

    [Fact]
    public async Task ToLmis_ClearsEmbassyAndCaseExecutiveVisibility_TEST36()
    {
        var (db, engine, userId, _) = CreateSut();
        var (embassy, caseExec, lmis, _, toLmis, candidate) = await SeedUnit3Async(db);

        await engine.UpdateStatusAsync(candidate.Id, "medical", "Fit", userId, "tester");
        await engine.UpdateStatusAsync(candidate.Id, "tasheer", "Book Done", userId, "tester");
        await engine.UpdateStatusAsync(candidate.Id, "visa", "Ready", userId, "tester");

        var atReady = await engine.GetCurrentStateAsync(candidate.Id);
        atReady.VisibleInStages.Should().Contain(caseExec.Id);
        atReady.VisibleInStages.Should().Contain(lmis.Id);

        await engine.UpdateStatusAsync(candidate.Id, "visa", "Submitted", userId, "tester");
        await engine.UpdateStatusAsync(candidate.Id, "visa", "Issued", userId, "tester");

        // Issued deactivates Case Executive mirror (Ready|Submitted only); LMIS preview may remain
        var before = await engine.GetCurrentStateAsync(candidate.Id);
        before.VisibleInStages.Should().NotContain(caseExec.Id);
        before.StageId.Should().Be(embassy.Id);

        var result = await engine.ExecuteTransitionAsync(candidate.Id, toLmis.Id, userId, "tester");
        result.IsSuccess.Should().BeTrue(result.Error);

        var after = await engine.GetCurrentStateAsync(candidate.Id);
        after.StageId.Should().Be(lmis.Id);
        after.VisibleInStages.Should().NotContain(embassy.Id);
        after.VisibleInStages.Should().NotContain(caseExec.Id);

        var updated = await db.Candidates.FindAsync(candidate.Id);
        updated!.CurrentStageId.Should().Be(lmis.Id);
        updated.VisibleInStages.Should().NotContain(embassy.Id);
        updated.VisibleInStages.Should().NotContain(caseExec.Id);
    }

    [Fact]
    public async Task InsurancePaid_SetsAvailable_TEST37()
    {
        var (db, engine, userId, user) = CreateSut();
        var (_, _, lmis, _, _, candidate) = await SeedUnit3Async(db);

        candidate.CurrentStageId = lmis.Id;
        candidate.CurrentStageName = lmis.Name;
        candidate.VisibleInStages = [lmis.Id];
        await db.SaveChangesAsync();

        var handler = new RecordInsurancePaidHandler(db, engine, user);
        var result = await handler.Handle(
            new RecordInsurancePaidCommand(candidate.Id, DateOnly.FromDateTime(DateTime.UtcNow)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);

        var state = await engine.GetCurrentStateAsync(candidate.Id);
        state.StatusValues["insurance"].Should().Be("Available");

        var events = await db.WorkflowEvents
            .Where(e => e.CandidateId == candidate.Id && e.EventType == WorkflowEventType.StatusUpdated)
            .ToListAsync();
        events.Count.Should().BeGreaterThanOrEqualTo(2);
    }
}
