using System.Text.Json;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Arrival.Commands;
using SimbaFlow.API.Features.Travel.Commands;
using SimbaFlow.API.Features.Travel.Validators;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Services;
using SimbaFlow.Infrastructure.Workflow;
using Microsoft.Extensions.Logging.Abstractions;

namespace SimbaFlow.API.Tests.Services;

/// <summary>
/// Example-based tests for Unit 4 Travel / Arrival / Exception (TEST-40–52).
/// </summary>
public class TravelArrivalServiceTests
{
    private static (TenantDbContext Db, WorkflowEngineService Engine, Guid UserId, ICurrentUserService User)
        CreateSut()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase($"travel_arrival_{Guid.NewGuid()}")
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var userId = Guid.NewGuid();
        var currentUser = Substitute.For<ICurrentUserService>();
        currentUser.UserId.Returns(userId.ToString());
        currentUser.UserName.Returns("tester");
        currentUser.TenantId.Returns(Guid.NewGuid());
        currentUser.Roles.Returns(["AgencyOwner"]);

        var db = new TenantDbContext(options, currentUser);
        var engine = new WorkflowEngineService(db, currentUser);
        return (db, engine, userId, currentUser);
    }

    private static async Task<(
        WorkflowStage Ticket,
        WorkflowStage Departure,
        WorkflowStage Arrival,
        WorkflowStage Commission,
        WorkflowTransitionRule ToDeparture,
        WorkflowTransitionRule ToArrival,
        WorkflowTransitionRule BackToTicket,
        WorkflowTransitionRule AddToCommission,
        Candidate Candidate)> SeedUnit4Async(TenantDbContext db)
    {
        var def = new WorkflowDefinition
        {
            TenantId = Guid.NewGuid(),
            Name = "Unit4",
            IsActive = true
        };

        var ticket = new WorkflowStage
        {
            WorkflowDefinitionId = def.Id,
            Name = "Ticket",
            SortOrder = 1,
            IsInitialStage = true,
            StageType = StageType.Simple
        };
        var departure = new WorkflowStage
        {
            WorkflowDefinitionId = def.Id,
            Name = "Departure",
            SortOrder = 2,
            StageType = StageType.Simple
        };
        var arrival = new WorkflowStage
        {
            WorkflowDefinitionId = def.Id,
            Name = "Arrival",
            SortOrder = 3,
            StageType = StageType.Simple
        };
        var commission = new WorkflowStage
        {
            WorkflowDefinitionId = def.Id,
            Name = "Commission",
            SortOrder = 4,
            StageType = StageType.Simple,
            IsFinalStage = true
        };

        ticket.Statuses =
        [
            new() { WorkflowStageId = ticket.Id, Name = "Pending", TrackName = "ticket_status", SortOrder = 1 },
            new() { WorkflowStageId = ticket.Id, Name = "Booking Complete", TrackName = "ticket_status", SortOrder = 2, IsTerminal = true }
        ];
        departure.Statuses =
        [
            new() { WorkflowStageId = departure.Id, Name = "Awaiting", TrackName = "notification_status", SortOrder = 1 },
            new() { WorkflowStageId = departure.Id, Name = "Notified", TrackName = "notification_status", SortOrder = 2 },
            new() { WorkflowStageId = departure.Id, Name = "Departed", TrackName = "departure_status", SortOrder = 3, IsTerminal = true },
            new() { WorkflowStageId = departure.Id, Name = "Not Departed", TrackName = "departure_status", SortOrder = 4 }
        ];
        arrival.Statuses =
        [
            new() { WorkflowStageId = arrival.Id, Name = "Pending", SortOrder = 1 },
            new() { WorkflowStageId = arrival.Id, Name = "Arrived", SortOrder = 2, IsTerminal = true },
            new() { WorkflowStageId = arrival.Id, Name = "Returned", SortOrder = 3, IsTerminal = true },
            new() { WorkflowStageId = arrival.Id, Name = "Runaway", SortOrder = 4, IsTerminal = true }
        ];

        var toDeparture = new WorkflowTransitionRule
        {
            WorkflowDefinitionId = def.Id,
            SourceStageId = ticket.Id,
            TargetStageId = departure.Id,
            ButtonLabel = "To Departure",
            RequiredFields = ["ticket_status", "destination", "flight_date"],
            RemoveFromSource = true,
            IsActive = true,
            Conditions = JsonDocument.Parse("{}")
        };
        var toArrival = new WorkflowTransitionRule
        {
            WorkflowDefinitionId = def.Id,
            SourceStageId = departure.Id,
            TargetStageId = arrival.Id,
            ButtonLabel = "To Arrival",
            RemoveFromSource = true,
            IsActive = true,
            Conditions = JsonDocument.Parse(
                """{"operator":"AND","rules":[{"field":"departure_status","op":"eq","value":"Departed"}]}""")
        };
        var backToTicket = new WorkflowTransitionRule
        {
            WorkflowDefinitionId = def.Id,
            SourceStageId = departure.Id,
            TargetStageId = ticket.Id,
            ButtonLabel = "Back to Ticket",
            RemoveFromSource = true,
            IsActive = true,
            Conditions = JsonDocument.Parse(
                """{"operator":"AND","rules":[{"field":"departure_status","op":"eq","value":"Not Departed"}]}""")
        };
        var addToCommission = new WorkflowTransitionRule
        {
            WorkflowDefinitionId = def.Id,
            SourceStageId = arrival.Id,
            TargetStageId = commission.Id,
            ButtonLabel = "Add to Commission",
            RemoveFromSource = false,
            IsActive = true,
            Conditions = JsonDocument.Parse("{}")
        };

        var candidate = new Candidate
        {
            FirstName = "Sara",
            LastName = "Bekele",
            PassportNumber = "EP9990001",
            DateOfBirth = new DateOnly(1998, 1, 1),
            Gender = Gender.Female,
            PartnerName = "Addis",
            CountryOfTravel = "Saudi Arabia",
            CurrentStageId = ticket.Id,
            CurrentStageName = ticket.Name,
            Status = CandidateStatus.Active,
            StageEnteredAt = DateTime.UtcNow
        };

        db.WorkflowDefinitions.Add(def);
        db.WorkflowStages.AddRange(ticket, departure, arrival, commission);
        db.WorkflowTransitionRules.AddRange(toDeparture, toArrival, backToTicket, addToCommission);
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync();

        return (ticket, departure, arrival, commission, toDeparture, toArrival, backToTicket, addToCommission, candidate);
    }

    [Fact]
    public void BookTicket_WithoutDestination_FailsValidation_TEST40()
    {
        var validator = new BookTicketValidator();
        var result = validator.TestValidate(new BookTicketCommand(
            Guid.NewGuid(), "", DateOnly.FromDateTime(DateTime.UtcNow)));
        result.ShouldHaveValidationErrorFor(x => x.Destination);
    }

    [Fact]
    public async Task BookTicket_SetsBookingCompleteAndFields_TEST40()
    {
        var (db, engine, _, user) = CreateSut();
        var (_, _, _, _, _, _, _, _, candidate) = await SeedUnit4Async(db);

        var handler = new BookTicketHandler(db, engine, user);
        var result = await handler.Handle(
            new BookTicketCommand(candidate.Id, "Riyadh", new DateOnly(2026, 8, 1), "TK-1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        var state = await engine.GetCurrentStateAsync(candidate.Id);
        state.StatusValues["ticket_status"].Should().Be("Booking Complete");
        state.StatusValues["destination"].Should().Be("Riyadh");
        state.StatusValues["flight_date"].Should().Be("2026-08-01");
    }

    [Fact]
    public async Task ToDeparture_RequiresBookingFields_TEST41()
    {
        var (db, engine, userId, _) = CreateSut();
        var (_, _, _, _, toDeparture, _, _, _, candidate) = await SeedUnit4Async(db);

        var fail = await engine.ExecuteTransitionAsync(candidate.Id, toDeparture.Id, userId, "tester");
        fail.IsSuccess.Should().BeFalse();

        await engine.UpdateStatusAsync(
            candidate.Id, "ticket_status", "Booking Complete", userId, "tester",
            metadata: new Dictionary<string, string>
            {
                ["destination"] = "Dubai",
                ["flight_date"] = "2026-09-01"
            });

        var ok = await engine.ExecuteTransitionAsync(candidate.Id, toDeparture.Id, userId, "tester");
        ok.IsSuccess.Should().BeTrue(ok.Error);

        var updated = await db.Candidates.FindAsync(candidate.Id);
        updated!.CurrentStageName.Should().Be("Departure");
    }

    [Fact]
    public void NotDeparted_RequiresOutcome_TEST43()
    {
        var validator = new RecordNotDepartedValidator();
        var result = validator.TestValidate(new RecordNotDepartedCommand(
            Guid.NewGuid(), "MissedFlight", ""));
        result.ShouldHaveValidationErrorFor(x => x.Outcome);
    }

    [Fact]
    public async Task CancelDeparture_SetsCanceled_StaysOnDeparture_TEST42_TEST45()
    {
        var (db, engine, userId, user) = CreateSut();
        var (_, departure, _, _, toDeparture, _, _, _, candidate) = await SeedUnit4Async(db);

        await engine.UpdateStatusAsync(
            candidate.Id, "ticket_status", "Booking Complete", userId, "tester",
            metadata: new Dictionary<string, string>
            {
                ["destination"] = "Jeddah",
                ["flight_date"] = "2026-07-25"
            });
        (await engine.ExecuteTransitionAsync(candidate.Id, toDeparture.Id, userId, "tester"))
            .IsSuccess.Should().BeTrue();

        var handler = new RecordNotDepartedHandler(db, engine, user);
        var result = await handler.Handle(
            new RecordNotDepartedCommand(candidate.Id, "Immigration", "CancelDeparture"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);

        var state = await engine.GetCurrentStateAsync(candidate.Id);
        state.StatusValues["departure_status"].Should().Be("Not Departed");
        state.StatusValues["canceled"].Should().Be("true");
        state.StageId.Should().Be(departure.Id);

        // Countdown exclusion model
        var include = false;
        var canceled = string.Equals(state.StatusValues.GetValueOrDefault("canceled"), "true",
            StringComparison.OrdinalIgnoreCase);
        (include || !canceled).Should().BeFalse();
    }

    [Fact]
    public async Task BackToTicket_ResetsPrimaryAndTicketPending_TEST44()
    {
        var (db, engine, userId, user) = CreateSut();
        var (ticket, _, _, _, toDeparture, _, _, _, candidate) = await SeedUnit4Async(db);

        await engine.UpdateStatusAsync(
            candidate.Id, "ticket_status", "Booking Complete", userId, "tester",
            metadata: new Dictionary<string, string>
            {
                ["destination"] = "Doha",
                ["flight_date"] = "2026-08-10"
            });
        (await engine.ExecuteTransitionAsync(candidate.Id, toDeparture.Id, userId, "tester"))
            .IsSuccess.Should().BeTrue();

        var handler = new RecordNotDepartedHandler(db, engine, user);
        (await handler.Handle(
            new RecordNotDepartedCommand(candidate.Id, "MissedFlight", "BackToTicket"),
            CancellationToken.None)).IsSuccess.Should().BeTrue();

        var updated = await db.Candidates.FindAsync(candidate.Id);
        updated!.CurrentStageId.Should().Be(ticket.Id);

        var state = await engine.GetCurrentStateAsync(candidate.Id);
        state.StatusValues["ticket_status"].Should().Be("Pending");
    }

    [Fact]
    public async Task ConfirmDeparted_MovesToArrival_TEST46()
    {
        var (db, engine, userId, user) = CreateSut();
        var (_, _, arrival, _, toDeparture, _, _, _, candidate) = await SeedUnit4Async(db);

        await engine.UpdateStatusAsync(
            candidate.Id, "ticket_status", "Booking Complete", userId, "tester",
            metadata: new Dictionary<string, string>
            {
                ["destination"] = "Kuwait",
                ["flight_date"] = "2026-08-15"
            });
        (await engine.ExecuteTransitionAsync(candidate.Id, toDeparture.Id, userId, "tester"))
            .IsSuccess.Should().BeTrue();
        await engine.UpdateStatusAsync(candidate.Id, "notification_status", "Notified", userId, "tester");

        var handler = new ConfirmDepartedHandler(db, engine, user);
        (await handler.Handle(new ConfirmDepartedCommand(candidate.Id), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        var updated = await db.Candidates.FindAsync(candidate.Id);
        updated!.CurrentStageId.Should().Be(arrival.Id);

        var state = await engine.GetCurrentStateAsync(candidate.Id);
        state.StatusValues["departure_status"].Should().Be("Departed");
        state.StatusValues["arrival"].Should().Be("Pending");
    }

    [Fact]
    public async Task MarkNotified_CallsNoOpNotifier_TEST47()
    {
        var (db, engine, userId, user) = CreateSut();
        var (_, _, _, _, toDeparture, _, _, _, candidate) = await SeedUnit4Async(db);

        await engine.UpdateStatusAsync(
            candidate.Id, "ticket_status", "Booking Complete", userId, "tester",
            metadata: new Dictionary<string, string>
            {
                ["destination"] = "Muscat",
                ["flight_date"] = "2026-08-20"
            });
        (await engine.ExecuteTransitionAsync(candidate.Id, toDeparture.Id, userId, "tester"))
            .IsSuccess.Should().BeTrue();

        var notifier = Substitute.For<ICandidateNotifier>();
        var handler = new MarkNotifiedHandler(db, engine, user, notifier);
        (await handler.Handle(new MarkNotifiedCommand(candidate.Id), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        await notifier.Received(1).NotifyAsync(candidate.Id, "departure.notified", Arg.Any<CancellationToken>());
        // NoOp implementation is registered in DI; substitute proves call site only
        var state = await engine.GetCurrentStateAsync(candidate.Id);
        state.StatusValues["notification_status"].Should().Be("Notified");
    }

    [Fact]
    public async Task AddToCommission_KeepsArrivalAndCreatesShell_TEST48_TEST49()
    {
        var (db, engine, userId, user) = CreateSut();
        var (_, _, arrival, commission, toDeparture, toArrival, _, _, candidate) = await SeedUnit4Async(db);

        await engine.UpdateStatusAsync(
            candidate.Id, "ticket_status", "Booking Complete", userId, "tester",
            metadata: new Dictionary<string, string>
            {
                ["destination"] = "Riyadh",
                ["flight_date"] = "2026-08-01"
            });
        (await engine.ExecuteTransitionAsync(candidate.Id, toDeparture.Id, userId, "tester")).IsSuccess.Should().BeTrue();
        await engine.UpdateStatusAsync(candidate.Id, "notification_status", "Notified", userId, "tester");
        await engine.UpdateStatusAsync(candidate.Id, "departure_status", "Departed", userId, "tester");
        (await engine.ExecuteTransitionAsync(candidate.Id, toArrival.Id, userId, "tester")).IsSuccess.Should().BeTrue();
        await engine.UpdateStatusAsync(candidate.Id, "arrival", "Arrived", userId, "tester");

        var handler = new AddToCommissionHandler(db, engine, user);
        (await handler.Handle(new AddToCommissionCommand(candidate.Id), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        var updated = await db.Candidates.FindAsync(candidate.Id);
        // Primary moves to Commission; Arrival remains visible (RemoveFromSource=false)
        updated!.CurrentStageId.Should().Be(commission.Id);
        updated.VisibleInStages.Should().Contain(arrival.Id);
        updated.VisibleInStages.Should().Contain(commission.Id);

        var shells = await db.Commissions.Where(c => c.CandidateId == candidate.Id && !c.IsDeleted).ToListAsync();
        shells.Should().HaveCount(1);

        (await handler.Handle(new AddToCommissionCommand(candidate.Id), CancellationToken.None))
            .IsSuccess.Should().BeTrue();
        (await db.Commissions.CountAsync(c => c.CandidateId == candidate.Id && !c.IsDeleted))
            .Should().Be(1);
    }

    [Fact]
    public async Task FlagException_CreatesOpenCase_TEST50()
    {
        var (db, engine, userId, user) = CreateSut();
        var (_, _, _, _, toDeparture, toArrival, _, _, candidate) = await SeedUnit4Async(db);

        await engine.UpdateStatusAsync(
            candidate.Id, "ticket_status", "Booking Complete", userId, "tester",
            metadata: new Dictionary<string, string>
            {
                ["destination"] = "Riyadh",
                ["flight_date"] = "2026-08-01"
            });
        (await engine.ExecuteTransitionAsync(candidate.Id, toDeparture.Id, userId, "tester")).IsSuccess.Should().BeTrue();
        await engine.UpdateStatusAsync(candidate.Id, "departure_status", "Departed", userId, "tester");
        (await engine.ExecuteTransitionAsync(candidate.Id, toArrival.Id, userId, "tester")).IsSuccess.Should().BeTrue();
        await engine.UpdateStatusAsync(candidate.Id, "arrival", "Pending", userId, "tester");

        var handler = new FlagExceptionHandler(db, engine, user);
        (await handler.Handle(new FlagExceptionCommand(candidate.Id, "Returned"), CancellationToken.None))
            .IsSuccess.Should().BeTrue();

        var cases = await db.ExceptionCases.Where(e => e.CandidateId == candidate.Id).ToListAsync();
        cases.Should().HaveCount(1);
        cases[0].Status.Should().Be(ExceptionStatus.Open);
        cases[0].Type.Should().Be(ExceptionType.Returned);

        var second = await handler.Handle(new FlagExceptionCommand(candidate.Id, "Runaway"), CancellationToken.None);
        second.IsSuccess.Should().BeFalse();
        second.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task AddToCommission_BlockedWhenOpenException_TEST52()
    {
        var (db, engine, userId, user) = CreateSut();
        var (_, _, _, _, toDeparture, toArrival, _, _, candidate) = await SeedUnit4Async(db);

        await engine.UpdateStatusAsync(
            candidate.Id, "ticket_status", "Booking Complete", userId, "tester",
            metadata: new Dictionary<string, string>
            {
                ["destination"] = "Riyadh",
                ["flight_date"] = "2026-08-01"
            });
        (await engine.ExecuteTransitionAsync(candidate.Id, toDeparture.Id, userId, "tester")).IsSuccess.Should().BeTrue();
        await engine.UpdateStatusAsync(candidate.Id, "departure_status", "Departed", userId, "tester");
        (await engine.ExecuteTransitionAsync(candidate.Id, toArrival.Id, userId, "tester")).IsSuccess.Should().BeTrue();
        await engine.UpdateStatusAsync(candidate.Id, "arrival", "Arrived", userId, "tester");

        // Open exception while still Arrived (investigation can exist without re-flagging status)
        db.ExceptionCases.Add(new Domain.Entities.Travel.ExceptionCase
        {
            CandidateId = candidate.Id,
            Type = ExceptionType.Returned,
            Status = ExceptionStatus.Open,
            OpenedAt = DateTime.UtcNow,
            OpenedByUserId = userId
        });
        await db.SaveChangesAsync();

        var add = await new AddToCommissionHandler(db, engine, user).Handle(
            new AddToCommissionCommand(candidate.Id), CancellationToken.None);
        add.IsSuccess.Should().BeFalse();
        add.Error.Should().ContainEquivalentOf("exception");
    }

    [Fact]
    public void NoOpNotifier_Completes_TEST47()
    {
        var notifier = new NoOpCandidateNotifier(NullLogger<NoOpCandidateNotifier>.Instance);
        var task = notifier.NotifyAsync(Guid.NewGuid(), "departure.notified");
        task.IsCompletedSuccessfully.Should().BeTrue();
    }
}
