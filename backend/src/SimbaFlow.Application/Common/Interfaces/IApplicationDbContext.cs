using Microsoft.EntityFrameworkCore;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Staff;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Entities.Workflow;

namespace SimbaFlow.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // Identity (ASP.NET Core Identity-based entities)
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

    // Staff
    DbSet<StaffProfile> StaffProfiles { get; }
    DbSet<StaffIdentifier> StaffIdentifiers { get; }
    DbSet<StaffDepartmentAffiliation> StaffDepartmentAffiliations { get; }

    // Candidates
    DbSet<Candidate> Candidates { get; }
    DbSet<CandidateDocument> CandidateDocuments { get; }

    // Workflow
    DbSet<WorkflowDefinition> WorkflowDefinitions { get; }
    DbSet<WorkflowStage> WorkflowStages { get; }
    DbSet<WorkflowEvent> WorkflowEvents { get; }
    DbSet<WorkflowSnapshot> WorkflowSnapshots { get; }

    // Tenant Roles (per-agency)
    DbSet<Domain.Entities.Tenancy.TenantRole> TenantRoles { get; }
    DbSet<Domain.Entities.Tenancy.TenantRolePermission> TenantRolePermissions { get; }
    DbSet<Domain.Entities.Tenancy.TenantUserRole> TenantUserRoles { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
