using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using SimbaFlow.API.Features.Workflow.Commands;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// Building the pipeline: adding steps, naming them, putting them in order, and wiring the
/// moves between them.
///
/// The screen that does this is the one place an agency can break its own system, so the rules
/// it enforces are the subject here rather than the clicking.
/// </summary>
public class WorkflowSetupTests : IDisposable
{
    private readonly TenantDbContext _context;
    private readonly WorkflowDefinition _definition;

    public WorkflowSetupTests()
    {
        _context = new TenantDbContext(
            new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Substitute.For<ICurrentUserService>());

        _definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Default Workflow",
            Version = 1,
            IsActive = true,
        };
        _context.WorkflowDefinitions.Add(_definition);
        _context.SaveChanges();
    }

    private WorkflowStage GivenStage(string name, int order, bool deleted = false)
    {
        var stage = new WorkflowStage
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = _definition.Id,
            Name = name,
            SortOrder = order,
            StageType = StageType.Simple,
            IsDeleted = deleted,
        };
        _context.WorkflowStages.Add(stage);
        _context.SaveChanges();
        return stage;
    }

    private WorkflowTransitionRule GivenMove(WorkflowStage from, WorkflowStage to, string label)
    {
        var rule = new WorkflowTransitionRule
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = _definition.Id,
            SourceStageId = from.Id,
            TargetStageId = to.Id,
            ButtonLabel = label,
            Conditions = JsonDocument.Parse("{}"),
            RequiredFields = [],
            AllowedRoles = [],
            IsActive = true,
        };
        _context.WorkflowTransitionRules.Add(rule);
        _context.SaveChanges();
        return rule;
    }

    // ── names ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task TwoStepsCannotShareAName()
    {
        // Every board the app ships with finds its step by name, and so does the address bar,
        // and so does the bot. Two called the same thing means half the app picks whichever the
        // database returned first.
        GivenStage("Embassy", 1);

        var result = await new CreateStageHandler(_context)
            .Handle(new CreateStageCommand("embassy", null, 2, 0), default);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        _context.WorkflowStages.Count(s => !s.IsDeleted).Should().Be(1);
    }

    [Fact]
    public async Task AStepCanKeepItsOwnNameWhileSomethingElseIsEdited()
    {
        var embassy = GivenStage("Embassy", 1);

        var result = await new UpdateStageHandler(_context)
            .Handle(new UpdateStageCommand(embassy.Id, "Embassy", "Now with a note", 1, 0), default);

        result.IsSuccess.Should().BeTrue();
        _context.WorkflowStages.Single().Description.Should().Be("Now with a note");
    }

    [Fact]
    public async Task RenamingOntoAnotherStepsNameIsRefused()
    {
        GivenStage("Embassy", 1);
        var lmis = GivenStage("LMIS", 2);

        var result = await new UpdateStageHandler(_context)
            .Handle(new UpdateStageCommand(lmis.Id, "Embassy", null, 2, 0), default);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
        _context.WorkflowStages.Single(s => s.Id == lmis.Id).Name.Should().Be("LMIS");
    }

    [Fact]
    public async Task ARemovedStepDoesNotHoldOntoItsName()
    {
        // Otherwise an agency that removes a step can never create one called that again.
        GivenStage("Screening", 1, deleted: true);

        var result = await new CreateStageHandler(_context)
            .Handle(new CreateStageCommand("Screening", null, 1, 0), default);

        result.IsSuccess.Should().BeTrue();
    }

    // ── order ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReorderingRenumbersEveryStepFromOne()
    {
        // Not "set this one to 2": two steps on the same number leave their order to the
        // database, and that order is the sidebar's.
        var a = GivenStage("Intake", 1);
        var b = GivenStage("Embassy", 2);
        var c = GivenStage("LMIS", 3);

        var result = await new ReorderStagesHandler(_context)
            .Handle(new ReorderStagesCommand([c.Id, a.Id, b.Id]), default);

        result.IsSuccess.Should().BeTrue();
        _context.WorkflowStages.OrderBy(s => s.SortOrder).Select(s => s.Name)
            .Should().Equal("LMIS", "Intake", "Embassy");
        _context.WorkflowStages.Select(s => s.SortOrder).Should().Equal(2, 3, 1);
    }

    [Fact]
    public async Task AnOrderMissingAStepIsRefused()
    {
        // The page was opened before somebody else added a step. Renumbering what this page
        // knows about would leave the new one at whatever number it was created with.
        var a = GivenStage("Intake", 1);
        GivenStage("Embassy", 2);

        var result = await new ReorderStagesHandler(_context)
            .Handle(new ReorderStagesCommand([a.Id]), default);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task AnOrderNamingTheSameStepTwiceIsRefused()
    {
        var a = GivenStage("Intake", 1);
        GivenStage("Embassy", 2);

        var result = await new ReorderStagesHandler(_context)
            .Handle(new ReorderStagesCommand([a.Id, a.Id]), default);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task RemovedStepsAreNotPartOfTheOrder()
    {
        var a = GivenStage("Intake", 1);
        var b = GivenStage("Embassy", 2);
        GivenStage("Gone", 3, deleted: true);

        var result = await new ReorderStagesHandler(_context)
            .Handle(new ReorderStagesCommand([b.Id, a.Id]), default);

        result.IsSuccess.Should().BeTrue();
    }

    // ── moves ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AMoveCannotLeadBackToWhereItStarts()
    {
        var embassy = GivenStage("Embassy", 1);

        var result = await new CreateTransitionRuleHandler(_context).Handle(
            new CreateTransitionRuleCommand(embassy.Id, embassy.Id, "Go", null, null, null, null, true),
            default);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task TwoButtonsOnOneBoardCannotSayTheSameThing()
    {
        // Whoever presses one is tossing a coin, and the audit trail records the label.
        var embassy = GivenStage("Embassy", 1);
        var lmis = GivenStage("LMIS", 2);
        var ticket = GivenStage("Ticket", 3);
        GivenMove(embassy, lmis, "Next");

        var result = await new CreateTransitionRuleHandler(_context).Handle(
            new CreateTransitionRuleCommand(embassy.Id, ticket.Id, "next", null, null, null, null, true),
            default);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task TheSameLabelOnADifferentBoardIsFine()
    {
        var embassy = GivenStage("Embassy", 1);
        var lmis = GivenStage("LMIS", 2);
        var ticket = GivenStage("Ticket", 3);
        GivenMove(embassy, lmis, "Next");

        var result = await new CreateTransitionRuleHandler(_context).Handle(
            new CreateTransitionRuleCommand(lmis.Id, ticket.Id, "Next", null, null, null, null, true),
            default);

        result.IsSuccess.Should().BeTrue();
    }

    // ── conditions ────────────────────────────────────────────────────────

    [Fact]
    public async Task ConditionsCanBeCorrectedAfterTheFact()
    {
        // They could not be. The editor had no field for them, so a condition set once could
        // only be changed by deleting the step and losing its history with it.
        var embassy = GivenStage("Embassy", 1);
        var lmis = GivenStage("LMIS", 2);
        var move = GivenMove(embassy, lmis, "To LMIS");

        var conditions = JsonDocument.Parse(
            """{"operator":"AND","rules":[{"field":"medical","op":"eq","value":"Fit"}]}""");

        var result = await new UpdateTransitionRuleHandler(_context).Handle(
            new UpdateTransitionRuleCommand(
                move.Id, "To LMIS", null, null, null, true, true, conditions.RootElement),
            default);

        result.IsSuccess.Should().BeTrue();
        _context.WorkflowTransitionRules.Single().Conditions.RootElement
            .GetProperty("rules")[0].GetProperty("value").GetString()
            .Should().Be("Fit");
    }

    [Fact]
    public async Task AnUpdateThatSaysNothingAboutConditionsLeavesThemAlone()
    {
        // Anything older than the editor does not send them, and silently clearing a step's
        // conditions would start offering it to everyone.
        var embassy = GivenStage("Embassy", 1);
        var lmis = GivenStage("LMIS", 2);
        var move = GivenMove(embassy, lmis, "To LMIS");
        move.Conditions = JsonDocument.Parse(
            """{"operator":"AND","rules":[{"field":"visa","op":"eq","value":"Issued"}]}""");
        await _context.SaveChangesAsync();

        var result = await new UpdateTransitionRuleHandler(_context).Handle(
            new UpdateTransitionRuleCommand(move.Id, "To LMIS", null, null, null, true, true),
            default);

        result.IsSuccess.Should().BeTrue();
        _context.WorkflowTransitionRules.Single().Conditions.RootElement
            .GetProperty("rules")[0].GetProperty("field").GetString()
            .Should().Be("visa");
    }

    public void Dispose() => _context.Dispose();
}
