using Microsoft.EntityFrameworkCore;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Finance;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Entities.Travel;
using SimbaFlow.Domain.Entities.Workflow;

namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Interface for the Tenant DbContext (dynamic schema per agency).
/// Contains: Candidates, Workflow, TenantRoles, Travel/Exceptions, Finance.
/// Schema is set dynamically via TenantConnectionInterceptor.
/// </summary>
public interface ITenantDbContext
{
    // Candidates
    DbSet<Candidate> Candidates { get; }
    DbSet<CandidateDocument> CandidateDocuments { get; }

    // Workflow
    DbSet<WorkflowDefinition> WorkflowDefinitions { get; }
    DbSet<WorkflowStage> WorkflowStages { get; }
    DbSet<WorkflowStageStatus> WorkflowStageStatuses { get; }
    DbSet<WorkflowTransitionRule> WorkflowTransitionRules { get; }
    DbSet<WorkflowEvent> WorkflowEvents { get; }
    DbSet<WorkflowSnapshot> WorkflowSnapshots { get; }
    DbSet<ParallelTrackDefinition> ParallelTrackDefinitions { get; }
    DbSet<MirrorViewRule> MirrorViewRules { get; }
    DbSet<StageMandatoryField> StageMandatoryFields { get; }

    // Travel / Arrival exceptions
    DbSet<ExceptionCase> ExceptionCases { get; }
    DbSet<InvestigationNote> InvestigationNotes { get; }
    DbSet<LiabilityAssignment> LiabilityAssignments { get; }

    // Finance
    DbSet<Commission> Commissions { get; }
    DbSet<CommissionFee> CommissionFees { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Dispute> Disputes { get; }
    DbSet<Account> Accounts { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalLine> JournalLines { get; }
    DbSet<FinanceCounter> FinanceCounters { get; }

    // Tenant Roles
    DbSet<TenantRole> TenantRoles { get; }
    DbSet<TenantRolePermission> TenantRolePermissions { get; }
    DbSet<TenantUserRole> TenantUserRoles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
