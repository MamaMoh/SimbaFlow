using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;
using SimbaFlow.Infrastructure.Persistence.Seeds;

namespace SimbaFlow.API.Tests.Behaviors;

/// <summary>
/// The pipeline is the agency's to shape, and a restart is not a vote.
///
/// The upgrader exists to give an agency provisioned before a feature shipped the stages and
/// steps that feature needs. It used to do that by name alone, which meant an agency that had
/// deliberately removed one got it back on the next deploy — the workflow was configurable only
/// until somebody restarted the API.
/// </summary>
public class WorkflowUpgraderRespectsTheAgencyTests : IDisposable
{
    private readonly TenantDbContext _context;
    private readonly WorkflowDefinitionUpgrader _upgrader = new(NullLogger<WorkflowDefinitionUpgrader>.Instance);
    private readonly Guid _tenantId = Guid.NewGuid();

    public WorkflowUpgraderRespectsTheAgencyTests()
    {
        _context = new TenantDbContext(
            new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Substitute.For<ICurrentUserService>());
    }

    private WorkflowDefinition GivenAPipeline()
    {
        var definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            Name = "Default Workflow",
            Version = 1,
            IsActive = true,
        };

        _context.WorkflowDefinitions.Add(definition);
        _context.WorkflowStages.AddRange(
            Stage(definition.Id, "Embassy", 3),
            Stage(definition.Id, "LMIS", 5));
        _context.SaveChanges();
        return definition;
    }

    private static WorkflowStage Stage(Guid definitionId, string name, int order, bool deleted = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            WorkflowDefinitionId = definitionId,
            Name = name,
            SortOrder = order,
            StageType = StageType.Simple,
            IsDeleted = deleted,
        };

    private Task Upgrade() => _upgrader.EnsureUnit3ArtifactsAsync(_context, _tenantId);

    private int LiveStagesNamed(string name) =>
        _context.WorkflowStages.Count(s => !s.IsDeleted && s.Name == name);

    [Fact]
    public async Task AnAgencyThatPredatesTheFeatureGetsTheStage()
    {
        // The case the upgrader is for: nothing of that name has ever existed here.
        GivenAPipeline();

        await Upgrade();

        LiveStagesNamed("Case Executive").Should().Be(1);
    }

    [Fact]
    public async Task AStageTheAgencyRemovedStaysRemoved()
    {
        var definition = GivenAPipeline();
        _context.WorkflowStages.Add(Stage(definition.Id, "Case Executive", 4, deleted: true));
        await _context.SaveChangesAsync();

        await Upgrade();

        LiveStagesNamed("Case Executive").Should().Be(0);
    }

    [Fact]
    public async Task NoStageMeansNoMirrorOntoIt()
    {
        // The mirror that feeds the Case Executive board has nothing to point at once the board
        // is gone, and creating one would be a rule referring to a stage nobody can open.
        var definition = GivenAPipeline();
        _context.WorkflowStages.Add(Stage(definition.Id, "Case Executive", 4, deleted: true));
        await _context.SaveChangesAsync();

        await Upgrade();

        var caseExecutiveIds = _context.WorkflowStages
            .Where(s => s.Name == "Case Executive").Select(s => s.Id).ToList();
        _context.MirrorViewRules
            .Any(m => caseExecutiveIds.Contains(m.TargetStageId))
            .Should().BeFalse();

        // The Embassy → LMIS mirror is untouched by any of this.
        _context.MirrorViewRules.Count(m => !m.IsDeleted).Should().Be(1);
    }

    [Fact]
    public async Task AMirrorTheAgencyRemovedStaysRemoved()
    {
        GivenAPipeline();
        var embassy = _context.WorkflowStages.Single(s => s.Name == "Embassy");
        var lmis = _context.WorkflowStages.Single(s => s.Name == "LMIS");
        _context.MirrorViewRules.Add(new MirrorViewRule
        {
            Id = Guid.NewGuid(),
            WorkflowStageId = embassy.Id,
            TargetStageId = lmis.Id,
            IsDeleted = true,
            Conditions = System.Text.Json.JsonDocument.Parse("{}"),
        });
        await _context.SaveChangesAsync();

        await Upgrade();

        _context.MirrorViewRules
            .Count(m => !m.IsDeleted && m.WorkflowStageId == embassy.Id && m.TargetStageId == lmis.Id)
            .Should().Be(0);
    }

    [Fact]
    public async Task RunningTwiceChangesNothingTheSecondTime()
    {
        GivenAPipeline();

        await Upgrade();
        await Upgrade();

        LiveStagesNamed("Case Executive").Should().Be(1);
        _context.MirrorViewRules.Count(m => !m.IsDeleted).Should().Be(2);
    }

    public void Dispose() => _context.Dispose();
}
