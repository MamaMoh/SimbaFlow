using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimbaFlow.Domain.Entities.Agency;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Infrastructure.Persistence.Seeds;

public static class OfficeSeeder
{
    public static async Task SeedOfficesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        if (await db.Offices.AnyAsync())
            return;

        db.Offices.AddRange(
            new Office
            {
                Name = "Head Office — Addis Ababa",
                Code = "HO-ADD",
                City = "Addis Ababa",
                Phone = "+251911000100",
                Email = "headoffice@simbaflow.local",
                SortOrder = 1
            },
            new Office
            {
                Name = "Bole Branch",
                Code = "BR-BOLE",
                City = "Addis Ababa",
                Phone = "+251911000200",
                Email = "bole@simbaflow.local",
                SortOrder = 2
            });

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded default offices");
    }
}

public static class WorkflowSeeder
{
    public static async Task SeedDefaultWorkflowAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        if (await db.WorkflowDefinitions.AnyAsync(d => d.IsActive))
            return;

        var defId = Guid.NewGuid();
        var newContractsId = Guid.NewGuid();
        var embassyId = Guid.NewGuid();
        var lmisId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var departId = Guid.NewGuid();
        var arrivalId = Guid.NewGuid();
        var commissionId = Guid.NewGuid();

        var definition = new WorkflowDefinition
        {
            Id = defId,
            TenantId = Guid.Empty,
            Name = "Default Labour Export Pipeline",
            Description = "ERP default: New Contracts → Embassy → LMIS → Ticket → Depart → Arrival → Commission",
            Version = 1,
            IsActive = true
        };

        var stages = new List<WorkflowStage>
        {
            new()
            {
                Id = newContractsId, WorkflowDefinitionId = defId, Name = "New Contracts",
                SortOrder = 1, StageType = StageType.Simple, IsInitialStage = true,
                ExpectedDurationHours = 72, WarningDurationHours = 48, CriticalDurationHours = 72
            },
            new()
            {
                Id = embassyId, WorkflowDefinitionId = defId, Name = "Embassy",
                SortOrder = 2, StageType = StageType.ParallelTrack,
                ExpectedDurationHours = 168, WarningDurationHours = 120, CriticalDurationHours = 168
            },
            new()
            {
                Id = lmisId, WorkflowDefinitionId = defId, Name = "LMIS",
                SortOrder = 3, StageType = StageType.MilestoneSequence,
                ExpectedDurationHours = 120, WarningDurationHours = 96, CriticalDurationHours = 120
            },
            new()
            {
                Id = ticketId, WorkflowDefinitionId = defId, Name = "Ticket",
                SortOrder = 4, StageType = StageType.Simple,
                ExpectedDurationHours = 72, WarningDurationHours = 48, CriticalDurationHours = 72
            },
            new()
            {
                Id = departId, WorkflowDefinitionId = defId, Name = "Depart",
                SortOrder = 5, StageType = StageType.Simple,
                ExpectedDurationHours = 48, WarningDurationHours = 24, CriticalDurationHours = 48
            },
            new()
            {
                Id = arrivalId, WorkflowDefinitionId = defId, Name = "Arrival",
                SortOrder = 6, StageType = StageType.Simple, IsFinalStage = false,
                ExpectedDurationHours = null
            },
            new()
            {
                Id = commissionId, WorkflowDefinitionId = defId, Name = "Commission",
                SortOrder = 7, StageType = StageType.Simple, IsFinalStage = true,
                ExpectedDurationHours = null
            }
        };

        var statuses = new List<WorkflowStageStatus>
        {
            // Embassy tracks
            Stat(embassyId, "Booked", "medical", 1, "#1f6fb2"),
            Stat(embassyId, "OnProgress", "medical", 2, "#b8860b"),
            Stat(embassyId, "Fit", "medical", 3, "#1d8a4e", terminal: true),
            Stat(embassyId, "Unfit", "medical", 4, "#c0392b", terminal: true),
            Stat(embassyId, "Booked", "tasheer", 1, "#1f6fb2"),
            Stat(embassyId, "Done", "tasheer", 2, "#1d8a4e", terminal: true),
            Stat(embassyId, "Expired", "tasheer", 3, "#c0392b", terminal: true),
            Stat(embassyId, "Ready", "embassy", 1, "#b8860b"),
            Stat(embassyId, "Submitted", "embassy", 2, "#1f6fb2"),
            Stat(embassyId, "Issued", "embassy", 3, "#1d8a4e", terminal: true),
            Stat(embassyId, "Rejected", "embassy", 4, "#c0392b", terminal: true),
            // LMIS
            Stat(lmisId, "Paid", "insurance", 1, "#1d8a4e"),
            Stat(lmisId, "Unpaid", "insurance", 2, "#c0392b"),
            Stat(lmisId, "Submitted", "lmis", 1, "#1f6fb2"),
            Stat(lmisId, "PaymentVerified", "lmis", 2, "#b8860b"),
            Stat(lmisId, "Checked", "lmis", 3, "#b8860b"),
            Stat(lmisId, "Verified", "lmis", 4, "#b8860b"),
            Stat(lmisId, "Issued", "lmis", 5, "#1d8a4e", terminal: true),
            // Ticket
            Stat(ticketId, "NotBooked", "ticket", 1, "#c0392b"),
            Stat(ticketId, "Booked", "ticket", 2, "#1d8a4e", terminal: true),
            // Depart
            Stat(departId, "Notified", "depart", 1, "#1f6fb2"),
            Stat(departId, "Depart", "depart", 2, "#1d8a4e", terminal: true),
            Stat(departId, "NotDepart", "depart", 3, "#b8860b"),
            // Arrival
            Stat(arrivalId, "OnDuty", "arrival", 1, "#1d8a4e"),
            Stat(arrivalId, "Returned", "arrival", 2, "#c0392b"),
            Stat(arrivalId, "Runaway", "arrival", 3, "#c0392b"),
            // Commission
            Stat(commissionId, "Requested", "commission", 1, "#b8860b"),
            Stat(commissionId, "Paid", "commission", 2, "#1d8a4e", terminal: true),
            Stat(commissionId, "Unpaid", "commission", 3, "#c0392b")
        };

        var tracks = new List<ParallelTrackDefinition>
        {
            new() { WorkflowStageId = embassyId, TrackName = "medical", CompletionStatus = "Fit", SortOrder = 1 },
            new() { WorkflowStageId = embassyId, TrackName = "tasheer", CompletionStatus = "Done", SortOrder = 2 }
        };

        JsonDocument Cond(string json) => JsonDocument.Parse(json);

        var transitions = new List<WorkflowTransitionRule>
        {
            new()
            {
                WorkflowDefinitionId = defId,
                SourceStageId = newContractsId,
                TargetStageId = embassyId,
                ButtonLabel = "To Embassy",
                SortOrder = 1,
                Conditions = Cond("{}"),
                AllowedRoles = ["ITPersonnel", "AgencyOwner", "Admin"]
            },
            new()
            {
                WorkflowDefinitionId = defId,
                SourceStageId = embassyId,
                TargetStageId = lmisId,
                ButtonLabel = "To LMIS",
                SortOrder = 1,
                Conditions = Cond("""{"operator":"AND","rules":[{"field":"embassy","op":"eq","value":"Issued"}]}"""),
                AllowedRoles = ["ITPersonnel", "AgencyOwner", "Admin"]
            },
            new()
            {
                WorkflowDefinitionId = defId,
                SourceStageId = lmisId,
                TargetStageId = ticketId,
                ButtonLabel = "To Ticket",
                SortOrder = 1,
                Conditions = Cond("""{"operator":"AND","rules":[{"field":"lmis","op":"eq","value":"Issued"}]}"""),
                AllowedRoles = ["ITPersonnel", "AgencyOwner", "Admin"]
            },
            new()
            {
                WorkflowDefinitionId = defId,
                SourceStageId = ticketId,
                TargetStageId = departId,
                ButtonLabel = "To Depart",
                SortOrder = 1,
                Conditions = Cond("""{"operator":"AND","rules":[{"field":"ticket","op":"eq","value":"Booked"}]}"""),
                RequiredFields = ["FlightDate"],
                AllowedRoles = ["ITPersonnel", "AgencyOwner", "Admin"]
            },
            new()
            {
                WorkflowDefinitionId = defId,
                SourceStageId = departId,
                TargetStageId = arrivalId,
                ButtonLabel = "To Arrival",
                SortOrder = 1,
                Conditions = Cond("""{"operator":"AND","rules":[{"field":"depart","op":"eq","value":"Depart"}]}"""),
                AllowedRoles = ["ITPersonnel", "Manager", "AgencyOwner", "Admin"]
            },
            new()
            {
                WorkflowDefinitionId = defId,
                SourceStageId = arrivalId,
                TargetStageId = commissionId,
                ButtonLabel = "Send to Commission",
                SortOrder = 1,
                Conditions = Cond("{}"),
                RemoveFromSource = false,
                AllowedRoles = ["Manager", "DeputyManager", "AgencyOwner", "Admin"]
            }
        };

        var mirrors = new List<MirrorViewRule>
        {
            new()
            {
                WorkflowStageId = embassyId,
                TargetStageId = lmisId,
                IsActive = true,
                Conditions = Cond("""{"operator":"AND","rules":[{"field":"medical","op":"eq","value":"Fit"},{"field":"tasheer","op":"eq","value":"Done"}]}""")
            }
        };

        var permissions = new List<StatusTransitionPermission>
        {
            new()
            {
                TrackKey = "embassy",
                ToStatus = "Submitted",
                AllowedRoleCode = "CaseExecutive",
                AllowedPermissionCode = "embassy.submit"
            }
        };

        db.WorkflowDefinitions.Add(definition);
        db.WorkflowStages.AddRange(stages);
        db.WorkflowStageStatuses.AddRange(statuses);
        db.ParallelTrackDefinitions.AddRange(tracks);
        db.WorkflowTransitionRules.AddRange(transitions);
        db.MirrorViewRules.AddRange(mirrors);
        db.StatusTransitionPermissions.AddRange(permissions);

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded default labour-export workflow pipeline");
    }

    private static WorkflowStageStatus Stat(
        Guid stageId, string name, string track, int order, string color, bool terminal = false) =>
        new()
        {
            WorkflowStageId = stageId,
            Name = name,
            TrackName = track,
            SortOrder = order,
            Color = color,
            IsTerminal = terminal
        };
}
