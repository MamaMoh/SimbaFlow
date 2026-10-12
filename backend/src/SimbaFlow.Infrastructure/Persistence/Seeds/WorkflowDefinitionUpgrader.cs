using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.Infrastructure.Persistence.Seeds;

/// <summary>
/// Idempotently adds Unit 3/4 workflow artifacts to tenants that already have an active definition.
///
/// It fills gaps; it does not overrule the agency. A tenant provisioned before a feature shipped
/// is missing the stages and steps that feature needs, and this is what gives them to it. But an
/// agency that has deliberately removed one of those stages or steps has made a decision about
/// its own pipeline, and putting it back on the next restart would mean the workflow was only
/// configurable until the next deploy.
///
/// The two cases are told apart by the soft delete: a row the agency removed is still there with
/// IsDeleted set, where a tenant that predates the feature has no row at all.
/// </summary>
public interface IWorkflowDefinitionUpgrader
{
    Task EnsureUnit3ArtifactsAsync(ITenantDbContext context, Guid tenantId, CancellationToken ct = default);

    Task EnsureUnit3ArtifactsIntoSchemaAsync(
        string connectionString,
        string schemaName,
        Guid tenantId,
        CancellationToken ct = default);

    Task EnsureUnit4ArtifactsAsync(ITenantDbContext context, Guid tenantId, CancellationToken ct = default);

    Task EnsureUnit4ArtifactsIntoSchemaAsync(
        string connectionString,
        string schemaName,
        Guid tenantId,
        CancellationToken ct = default);
}

public class WorkflowDefinitionUpgrader : IWorkflowDefinitionUpgrader
{
    public const string CaseExecutiveStageName = "Case Executive";
    public const string EmbassyStageName = "Embassy";
    public const string LmisStageName = "LMIS";
    public const string TicketStageName = "Ticket";
    public const string DepartureStageName = "Departure";
    public const string ArrivalStageName = "Arrival";
    public const string CommissionStageName = "Commission";
    public const string AddToCommissionAction = "Add to Commission";
    public const string ToDepartureAction = "To Departure";
    public const string ToArrivalAction = "To Arrival";
    public const string BackToTicketAction = "Back to Ticket";

    private readonly ILogger<WorkflowDefinitionUpgrader> _logger;

    public WorkflowDefinitionUpgrader(ILogger<WorkflowDefinitionUpgrader> logger)
    {
        _logger = logger;
    }

    public async Task EnsureUnit3ArtifactsIntoSchemaAsync(
        string connectionString,
        string schemaName,
        Guid tenantId,
        CancellationToken ct = default)
    {
        await using var ctx = CreateContext(connectionString, schemaName, tenantId);
        await EnsureUnit3ArtifactsAsync(ctx, tenantId, ct);
    }

    public async Task EnsureUnit4ArtifactsIntoSchemaAsync(
        string connectionString,
        string schemaName,
        Guid tenantId,
        CancellationToken ct = default)
    {
        await using var ctx = CreateContext(connectionString, schemaName, tenantId);
        await EnsureUnit4ArtifactsAsync(ctx, tenantId, ct);
    }

    private static TenantDbContext CreateContext(string connectionString, string schemaName, Guid tenantId)
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SearchPath = schemaName
        };

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TenantDbContext(options, new UpgradeCurrentUser(tenantId));
    }

    public async Task EnsureUnit3ArtifactsAsync(ITenantDbContext context, Guid tenantId, CancellationToken ct = default)
    {
        var definition = await context.WorkflowDefinitions
            .Include(d => d.Stages)
            .FirstOrDefaultAsync(d => d.IsActive && !d.IsDeleted, ct);

        if (definition is null)
        {
            _logger.LogDebug("No active workflow definition for tenant {TenantId}; skip Unit 3 upgrade", tenantId);
            return;
        }

        var embassy = definition.Stages.FirstOrDefault(s =>
            !s.IsDeleted && s.Name.Equals(EmbassyStageName, StringComparison.OrdinalIgnoreCase));
        var lmis = definition.Stages.FirstOrDefault(s =>
            !s.IsDeleted && s.Name.Equals(LmisStageName, StringComparison.OrdinalIgnoreCase));

        if (embassy is null || lmis is null)
        {
            _logger.LogWarning(
                "Tenant {TenantId} workflow missing Embassy or LMIS stage; cannot apply Unit 3 upgrade",
                tenantId);
            return;
        }

        var caseExecutive = definition.Stages.FirstOrDefault(s =>
            !s.IsDeleted && s.Name.Equals(CaseExecutiveStageName, StringComparison.OrdinalIgnoreCase));

        if (caseExecutive is null && WasRemoved(definition, CaseExecutiveStageName))
        {
            _logger.LogDebug(
                "Tenant {TenantId} has removed the {Stage} stage; leaving it removed",
                tenantId, CaseExecutiveStageName);
        }
        else if (caseExecutive is null)
        {
            var stagesToBump = await context.WorkflowStages
                .Where(s => s.WorkflowDefinitionId == definition.Id
                            && !s.IsDeleted
                            && s.SortOrder > embassy.SortOrder)
                .ToListAsync(ct);

            foreach (var stage in stagesToBump)
                stage.SortOrder += 1;

            caseExecutive = new WorkflowStage
            {
                WorkflowDefinitionId = definition.Id,
                Name = CaseExecutiveStageName,
                Description = "Mirror-only board for Case Executives (visa Ready/Submitted)",
                SortOrder = embassy.SortOrder + 1,
                StageType = StageType.Simple,
                IsInitialStage = false,
                IsFinalStage = false
            };
            context.WorkflowStages.Add(caseExecutive);
            _logger.LogInformation("Added Case Executive stage for tenant {TenantId}", tenantId);
        }

        await context.SaveChangesAsync(ct);

        await EnsureMirrorAsync(
            context,
            embassy.Id,
            lmis.Id,
            """
            {
              "operator": "AND",
              "rules": [
                { "field": "medical", "op": "eq", "value": "Fit" },
                { "field": "tasheer", "op": "eq", "value": "Book Done" }
              ]
            }
            """,
            ct);

        if (caseExecutive is not null)
            await EnsureMirrorAsync(
                context,
                embassy.Id,
                caseExecutive.Id,
                """
            {
              "operator": "OR",
              "rules": [
                { "field": "visa", "op": "eq", "value": "Ready" },
                { "field": "visa", "op": "eq", "value": "Submitted" }
              ]
            }
            """,
                ct);

        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Whether this stage is gone because the agency took it out, rather than because the
    /// tenant predates it.
    /// </summary>
    private static bool WasRemoved(WorkflowDefinition definition, string name) =>
        definition.Stages.Any(s =>
            s.IsDeleted && s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public async Task EnsureUnit4ArtifactsAsync(ITenantDbContext context, Guid tenantId, CancellationToken ct = default)
    {
        var definition = await context.WorkflowDefinitions
            .Include(d => d.Stages)
            .FirstOrDefaultAsync(d => d.IsActive && !d.IsDeleted, ct);

        if (definition is null)
        {
            _logger.LogDebug("No active workflow definition for tenant {TenantId}; skip Unit 4 upgrade", tenantId);
            return;
        }

        var ticket = FindStage(definition, TicketStageName);
        var departure = FindStage(definition, DepartureStageName);
        var arrival = FindStage(definition, ArrivalStageName);
        var commission = FindStage(definition, CommissionStageName);

        if (ticket is null || departure is null || arrival is null || commission is null)
        {
            _logger.LogWarning(
                "Tenant {TenantId} workflow missing Ticket/Departure/Arrival/Commission; cannot apply Unit 4 upgrade",
                tenantId);
            return;
        }

        await EnsureTransitionAsync(
            context,
            definition.Id,
            ticket.Id,
            departure.Id,
            ToDepartureAction,
            removeFromSource: true,
            requiredFields: ["ticket_status", "destination", "flight_date"],
            conditionsJson: null,
            sortOrder: 1,
            ct);

        await EnsureTransitionAsync(
            context,
            definition.Id,
            departure.Id,
            arrival.Id,
            ToArrivalAction,
            removeFromSource: true,
            requiredFields: null,
            conditionsJson: """{"operator":"AND","rules":[{"field":"departure_status","op":"eq","value":"Departed"}]}""",
            sortOrder: 1,
            ct);

        await EnsureTransitionAsync(
            context,
            definition.Id,
            departure.Id,
            ticket.Id,
            BackToTicketAction,
            removeFromSource: true,
            requiredFields: null,
            conditionsJson: """{"operator":"AND","rules":[{"field":"departure_status","op":"eq","value":"Not Departed"}]}""",
            sortOrder: 2,
            ct);

        // Critical: permanent Arrival ledger
        await EnsureTransitionAsync(
            context,
            definition.Id,
            arrival.Id,
            commission.Id,
            AddToCommissionAction,
            removeFromSource: false,
            requiredFields: null,
            conditionsJson: null,
            sortOrder: 1,
            ct);

        // The Arrival ledger used to be emptied by this step, which lost the record of everyone
        // who had already travelled. That was repaired on every tenant years of restarts ago and
        // is correct in the seeder, so it is no longer forced here: an agency that has since set
        // this deliberately gets to keep its answer.

        await context.SaveChangesAsync(ct);
        _logger.LogDebug("Unit 4 workflow artifacts ensured for tenant {TenantId}", tenantId);
    }

    private static WorkflowStage? FindStage(WorkflowDefinition definition, string name) =>
        definition.Stages.FirstOrDefault(s =>
            !s.IsDeleted && s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static async Task EnsureTransitionAsync(
        ITenantDbContext context,
        Guid definitionId,
        Guid sourceStageId,
        Guid targetStageId,
        string actionName,
        bool removeFromSource,
        string[]? requiredFields,
        string? conditionsJson,
        int sortOrder,
        CancellationToken ct)
    {
        var existing = await context.WorkflowTransitionRules
            .FirstOrDefaultAsync(r =>
                r.WorkflowDefinitionId == definitionId
                && r.ButtonLabel == actionName
                && r.SourceStageId == sourceStageId
                && r.TargetStageId == targetStageId, ct);

        // Already there, in one form or another. It is the agency's now — including what it
        // requires and when it is offered, which this used to overwrite on every restart, and
        // including a decision to remove it.
        if (existing is not null)
            return;

        context.WorkflowTransitionRules.Add(new WorkflowTransitionRule
        {
            WorkflowDefinitionId = definitionId,
            SourceStageId = sourceStageId,
            TargetStageId = targetStageId,
            ButtonLabel = actionName,
            SortOrder = sortOrder,
            RemoveFromSource = removeFromSource,
            RequiredFields = requiredFields ?? [],
            Conditions = JsonDocument.Parse(conditionsJson ?? "{}"),
            IsActive = true
        });
    }

    private static async Task EnsureMirrorAsync(
        ITenantDbContext context,
        Guid sourceStageId,
        Guid targetStageId,
        string conditionsJson,
        CancellationToken ct)
    {
        // Including a mirror the agency has removed: the row survives the delete, and putting it
        // back would make the removal last until the next restart.
        var exists = await context.MirrorViewRules.AnyAsync(r =>
            r.WorkflowStageId == sourceStageId
            && r.TargetStageId == targetStageId, ct);

        if (exists)
            return;

        context.MirrorViewRules.Add(new MirrorViewRule
        {
            WorkflowStageId = sourceStageId,
            TargetStageId = targetStageId,
            IsActive = true,
            Conditions = JsonDocument.Parse(conditionsJson)
        });
    }

    private sealed class UpgradeCurrentUser(Guid tenantId) : ICurrentUserService
    {
        public string? UserId => null;
        public string? UserName => "system";
        public string? Email => null;
        public Guid? ActiveLocationId => null;
        public Guid? DepartmentId => null;
        public Guid? TenantId => tenantId;
        public IReadOnlyList<string> Permissions => [];
        public IReadOnlyList<string> Roles => [];
        public bool IsSuperAdmin => true;
        public bool HasPermission(string permission) => true;
        public string? IpAddress => null;
        public string? UserAgent => null;
    }
}
