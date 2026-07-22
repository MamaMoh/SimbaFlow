using Microsoft.EntityFrameworkCore;
using SimbaFlow.Domain.Entities.Agency;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Locations;
using SimbaFlow.Domain.Entities.Staff;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Entities.Workflow;

namespace SimbaFlow.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // Identity
    DbSet<ApplicationUser> ApplicationUsers { get; }
    DbSet<ApplicationRole> ApplicationRoles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<Department> Departments { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<PasswordHistory> PasswordHistories { get; }
    DbSet<TenantInfo> Tenants { get; }

    // Tenancy (public schema)
    DbSet<SystemConfiguration> SystemConfigurations { get; }
    DbSet<ExchangeRate> ExchangeRates { get; }

    // Locations
    DbSet<Location> Locations { get; }

    // Agency
    DbSet<Office> Offices { get; }
    DbSet<Partner> Partners { get; }

    // Staff
    DbSet<StaffProfile> StaffProfiles { get; }
    DbSet<StaffLocationMapping> StaffLocationMappings { get; }
    DbSet<StaffIdentifier> StaffIdentifiers { get; }
    DbSet<StaffDepartmentAffiliation> StaffDepartmentAffiliations { get; }

    // Candidates
    DbSet<Candidate> Candidates { get; }
    DbSet<CandidateDocument> CandidateDocuments { get; }
    DbSet<CandidatePlacement> CandidatePlacements { get; }
    DbSet<CandidateRelative> CandidateRelatives { get; }
    DbSet<CandidateSkills> CandidateSkills { get; }
    DbSet<CandidateStageStay> CandidateStageStays { get; }
    DbSet<CandidateStepStay> CandidateStepStays { get; }
    DbSet<CandidateReturned> CandidateReturnedRecords { get; }
    DbSet<CandidateComplaint> CandidateComplaints { get; }
    DbSet<CandidateCommission> CandidateCommissions { get; }

    // Workflow
    DbSet<WorkflowDefinition> WorkflowDefinitions { get; }
    DbSet<WorkflowStage> WorkflowStages { get; }
    DbSet<WorkflowEvent> WorkflowEvents { get; }
    DbSet<WorkflowSnapshot> WorkflowSnapshots { get; }
    DbSet<WorkflowTransitionRule> WorkflowTransitionRules { get; }
    DbSet<WorkflowStageStatus> WorkflowStageStatuses { get; }
    DbSet<ParallelTrackDefinition> ParallelTrackDefinitions { get; }
    DbSet<MirrorViewRule> MirrorViewRules { get; }
    DbSet<StageMandatoryField> StageMandatoryFields { get; }
    DbSet<TaskAssignment> TaskAssignments { get; }
    DbSet<StatusTransitionPermission> StatusTransitionPermissions { get; }

    // Tenant Roles
    DbSet<TenantRole> TenantRoles { get; }
    DbSet<TenantRolePermission> TenantRolePermissions { get; }
    DbSet<TenantUserRole> TenantUserRoles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
