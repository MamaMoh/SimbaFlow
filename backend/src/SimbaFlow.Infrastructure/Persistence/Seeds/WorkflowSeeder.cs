using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.Infrastructure.Persistence.Seeds;

/// <summary>
/// Seeds the default 8-stage labour-export workflow into a tenant schema.
/// Idempotent: skips if an active workflow definition already exists.
/// </summary>
public static class WorkflowSeeder
{
    public static async Task SeedDefaultWorkflowAsync(
        ITenantDbContext context,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var exists = await context.WorkflowDefinitions
            .AnyAsync(w => w.IsActive && !w.IsDeleted, ct);

        if (exists)
            return;

        var definition = new WorkflowDefinition
        {
            TenantId = tenantId,
            Name = "Default Workflow",
            Description = "Standard labour-export pipeline: Intake → Commission",
            Version = 1,
            IsActive = true
        };

        // ──── Stages ────
        var intake = Stage(definition.Id, "Intake", 1, StageType.Simple, isInitial: true);
        var newContracts = Stage(definition.Id, "New Contracts", 2, StageType.Simple);
        var embassy = Stage(definition.Id, "Embassy", 3, StageType.ParallelTrack);
        var caseExecutive = Stage(definition.Id, "Case Executive", 4, StageType.Simple);
        var lmis = Stage(definition.Id, "LMIS", 5, StageType.MilestoneSequence);
        var ticket = Stage(definition.Id, "Ticket", 6, StageType.Simple);
        var departure = Stage(definition.Id, "Departure", 7, StageType.Simple);
        var arrival = Stage(definition.Id, "Arrival", 8, StageType.Simple);
        var commission = Stage(definition.Id, "Commission", 9, StageType.Simple, isFinal: true);

        // Fix WorkflowDefinitionId after stages created with shared parent
        foreach (var s in new[] { intake, newContracts, embassy, caseExecutive, lmis, ticket, departure, arrival, commission })
            s.WorkflowDefinitionId = definition.Id;

        definition.Stages =
        [
            intake, newContracts, embassy, caseExecutive, lmis, ticket, departure, arrival, commission
        ];

        // ──── Statuses ────
        intake.Statuses = [Status(intake.Id, "Registered", 1)];

        newContracts.Statuses =
        [
            Status(newContracts.Id, "Pending Review", 1),
            Status(newContracts.Id, "Ready", 2)
        ];

        embassy.Statuses =
        [
            Status(embassy.Id, "Pending", 1, "medical"),
            Status(embassy.Id, "Booked", 2, "medical"),
            Status(embassy.Id, "Fit", 3, "medical", isTerminal: true),
            Status(embassy.Id, "Unfit", 4, "medical", isTerminal: true),
            Status(embassy.Id, "Pending", 1, "tasheer"),
            Status(embassy.Id, "Booked", 2, "tasheer"),
            Status(embassy.Id, "Book Done", 3, "tasheer", isTerminal: true),
            Status(embassy.Id, "Expired", 4, "tasheer", isTerminal: true),
            Status(embassy.Id, "Ready", 1, "visa"),
            Status(embassy.Id, "Submitted", 2, "visa"),
            Status(embassy.Id, "Issued", 3, "visa", isTerminal: true),
            Status(embassy.Id, "Rejected", 4, "visa", isTerminal: true)
        ];

        embassy.ParallelTracks =
        [
            new ParallelTrackDefinition
            {
                WorkflowStageId = embassy.Id,
                TrackName = "medical",
                CompletionStatus = "Fit",
                SortOrder = 1
            },
            new ParallelTrackDefinition
            {
                WorkflowStageId = embassy.Id,
                TrackName = "tasheer",
                CompletionStatus = "Book Done",
                SortOrder = 2
            }
        ];

        lmis.Statuses =
        [
            Status(lmis.Id, "Insurance Unpaid", 1, "insurance"),
            Status(lmis.Id, "Insurance Paid", 2, "insurance"),
            Status(lmis.Id, "Available", 3, "insurance"),
            Status(lmis.Id, "Uploaded", 1, "milestone"),
            Status(lmis.Id, "Check Verified", 2, "milestone"),
            Status(lmis.Id, "Issued", 3, "milestone", isTerminal: true)
        ];

        ticket.Statuses =
        [
            Status(ticket.Id, "Pending", 1, "ticket_status"),
            Status(ticket.Id, "Booking Complete", 2, "ticket_status", isTerminal: true)
        ];

        departure.Statuses =
        [
            Status(departure.Id, "Awaiting", 1, "notification_status"),
            Status(departure.Id, "Notified", 2, "notification_status"),
            Status(departure.Id, "Departed", 3, "departure_status", isTerminal: true),
            Status(departure.Id, "Not Departed", 4, "departure_status")
        ];

        arrival.Statuses =
        [
            Status(arrival.Id, "Pending", 1),
            Status(arrival.Id, "Arrived", 2, isTerminal: true),
            Status(arrival.Id, "Returned", 3, isTerminal: true),
            Status(arrival.Id, "Runaway", 4, isTerminal: true)
        ];

        commission.Statuses =
        [
            Status(commission.Id, "Pending", 1),
            Status(commission.Id, "Partial", 2),
            Status(commission.Id, "Settled", 3, isTerminal: true),
            Status(commission.Id, "Disputed", 4)
        ];

        // ──── Mirror: Embassy → LMIS when medical=Fit AND tasheer=Book Done ────
        // ──── Mirror: Embassy → Case Executive when visa Ready OR Submitted ────
        embassy.MirrorViewRules =
        [
            new MirrorViewRule
            {
                WorkflowStageId = embassy.Id,
                TargetStageId = lmis.Id,
                IsActive = true,
                Conditions = JsonDocument.Parse("""
                    {
                      "operator": "AND",
                      "rules": [
                        { "field": "medical", "op": "eq", "value": "Fit" },
                        { "field": "tasheer", "op": "eq", "value": "Book Done" }
                      ]
                    }
                    """)
            },
            new MirrorViewRule
            {
                WorkflowStageId = embassy.Id,
                TargetStageId = caseExecutive.Id,
                IsActive = true,
                Conditions = JsonDocument.Parse("""
                    {
                      "operator": "OR",
                      "rules": [
                        { "field": "visa", "op": "eq", "value": "Ready" },
                        { "field": "visa", "op": "eq", "value": "Submitted" }
                      ]
                    }
                    """)
            }
        ];

        // ──── Transitions ────
        definition.TransitionRules =
        [
            // Intake → New Contracts (manual; typically immediate after review)
            Transition(definition.Id, intake.Id, newContracts.Id, "To New Contracts", 1,
                removeFromSource: true),

            // New Contracts → Embassy when Ready
            Transition(definition.Id, newContracts.Id, embassy.Id, "To Embassy", 1,
                conditions: """{"operator":"AND","rules":[{"field":"status","op":"eq","value":"Ready"}]}""",
                removeFromSource: true),

            // Embassy → LMIS when visa Issued
            Transition(definition.Id, embassy.Id, lmis.Id, "To LMIS", 1,
                conditions: """{"operator":"AND","rules":[{"field":"visa","op":"eq","value":"Issued"}]}""",
                removeFromSource: true),

            // LMIS → Ticket when milestone Issued
            Transition(definition.Id, lmis.Id, ticket.Id, "To Ticket", 1,
                conditions: """{"operator":"AND","rules":[{"field":"milestone","op":"eq","value":"Issued"}]}""",
                removeFromSource: true),

            // Ticket → Departure when booking fields present
            Transition(definition.Id, ticket.Id, departure.Id, "To Departure", 1,
                requiredFields: ["ticket_status", "destination", "flight_date"],
                removeFromSource: true),

            // Departure → Arrival when Departed
            Transition(definition.Id, departure.Id, arrival.Id, "To Arrival", 1,
                conditions: """{"operator":"AND","rules":[{"field":"departure_status","op":"eq","value":"Departed"}]}""",
                removeFromSource: true),

            // Departure → Ticket (back) when Not Departed
            Transition(definition.Id, departure.Id, ticket.Id, "Back to Ticket", 2,
                conditions: """{"operator":"AND","rules":[{"field":"departure_status","op":"eq","value":"Not Departed"}]}""",
                removeFromSource: true),

            // Arrival stays + commission child is Unit 3; for now transition into Commission view
            Transition(definition.Id, arrival.Id, commission.Id, "Add to Commission", 1,
                removeFromSource: false)
        ];

        context.WorkflowDefinitions.Add(definition);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Seeds into an explicit tenant schema using a dedicated DbContext (e.g. during provisioning).
    /// </summary>
    public static async Task SeedDefaultWorkflowIntoSchemaAsync(
        string connectionString,
        string schemaName,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var csb = new Npgsql.NpgsqlConnectionStringBuilder(connectionString)
        {
            SearchPath = schemaName
        };

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(csb.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var ctx = new TenantDbContext(options, new ProvisioningCurrentUser(tenantId));
        await SeedDefaultWorkflowAsync(ctx, tenantId, ct);
    }

    private static WorkflowStage Stage(
        Guid definitionId, string name, int sortOrder, StageType type,
        bool isInitial = false, bool isFinal = false) => new()
    {
        WorkflowDefinitionId = definitionId,
        Name = name,
        SortOrder = sortOrder,
        StageType = type,
        IsInitialStage = isInitial,
        IsFinalStage = isFinal
    };

    private static WorkflowStageStatus Status(
        Guid stageId, string name, int sortOrder,
        string? trackName = null, bool isTerminal = false) => new()
    {
        WorkflowStageId = stageId,
        Name = name,
        SortOrder = sortOrder,
        TrackName = trackName,
        IsTerminal = isTerminal
    };

    private static WorkflowTransitionRule Transition(
        Guid definitionId, Guid sourceId, Guid targetId, string label, int sortOrder,
        string? conditions = null,
        string[]? requiredFields = null,
        string[]? allowedRoles = null,
        bool removeFromSource = true) => new()
    {
        WorkflowDefinitionId = definitionId,
        SourceStageId = sourceId,
        TargetStageId = targetId,
        ButtonLabel = label,
        SortOrder = sortOrder,
        Conditions = JsonDocument.Parse(conditions ?? "{}"),
        RequiredFields = requiredFields ?? [],
        AllowedRoles = allowedRoles ?? [],
        RemoveFromSource = removeFromSource,
        IsActive = true
    };

    private sealed class ProvisioningCurrentUser(Guid tenantId) : ICurrentUserService
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
