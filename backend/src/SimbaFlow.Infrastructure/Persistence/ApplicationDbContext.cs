using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Entities.Agency;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Locations;
using SimbaFlow.Domain.Entities.Staff;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Infrastructure.Audit;

namespace SimbaFlow.Infrastructure.Persistence;

/// <summary>
/// Application database context built on ASP.NET Core Identity.
/// Handles audit fields and domain event collection on SaveChanges.
/// Supports schema-per-tenant isolation via TenantConnectionInterceptor.
/// </summary>
public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid,
        IdentityUserClaim<Guid>, IdentityUserRole<Guid>, IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>,
      IApplicationDbContext
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDomainEventDispatcher? _domainEventDispatcher;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUserService,
        IDomainEventDispatcher? domainEventDispatcher = null)
        : base(options)
    {
        _currentUserService = currentUserService;
        _domainEventDispatcher = domainEventDispatcher;
    }

    // Identity (custom)
    public DbSet<ApplicationUser> ApplicationUsers => Users;
    public DbSet<ApplicationRole> ApplicationRoles => Roles;
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();
    public DbSet<TenantInfo> Tenants => Set<TenantInfo>();

    // Tenancy (public schema)
    public DbSet<SystemConfiguration> SystemConfigurations => Set<SystemConfiguration>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    // Audit
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ReadAuditLog> ReadAuditLogs => Set<ReadAuditLog>();

    // Locations
    public DbSet<Location> Locations => Set<Location>();

    // Agency
    public DbSet<Office> Offices => Set<Office>();
    public DbSet<Partner> Partners => Set<Partner>();

    // Staff
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<StaffLocationMapping> StaffLocationMappings => Set<StaffLocationMapping>();
    public DbSet<StaffIdentifier> StaffIdentifiers => Set<StaffIdentifier>();
    public DbSet<StaffDepartmentAffiliation> StaffDepartmentAffiliations => Set<StaffDepartmentAffiliation>();

    // Candidates
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CandidateDocument> CandidateDocuments => Set<CandidateDocument>();
    public DbSet<CandidatePlacement> CandidatePlacements => Set<CandidatePlacement>();
    public DbSet<CandidateRelative> CandidateRelatives => Set<CandidateRelative>();
    public DbSet<CandidateSkills> CandidateSkills => Set<CandidateSkills>();
    public DbSet<CandidateStageStay> CandidateStageStays => Set<CandidateStageStay>();
    public DbSet<CandidateStepStay> CandidateStepStays => Set<CandidateStepStay>();
    public DbSet<CandidateReturned> CandidateReturnedRecords => Set<CandidateReturned>();
    public DbSet<CandidateComplaint> CandidateComplaints => Set<CandidateComplaint>();
    public DbSet<CandidateCommission> CandidateCommissions => Set<CandidateCommission>();

    // Workflow
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowStage> WorkflowStages => Set<WorkflowStage>();
    public DbSet<WorkflowEvent> WorkflowEvents => Set<WorkflowEvent>();
    public DbSet<WorkflowSnapshot> WorkflowSnapshots => Set<WorkflowSnapshot>();
    public DbSet<WorkflowTransitionRule> WorkflowTransitionRules => Set<WorkflowTransitionRule>();
    public DbSet<WorkflowStageStatus> WorkflowStageStatuses => Set<WorkflowStageStatus>();
    public DbSet<ParallelTrackDefinition> ParallelTrackDefinitions => Set<ParallelTrackDefinition>();
    public DbSet<MirrorViewRule> MirrorViewRules => Set<MirrorViewRule>();
    public DbSet<StageMandatoryField> StageMandatoryFields => Set<StageMandatoryField>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<StatusTransitionPermission> StatusTransitionPermissions => Set<StatusTransitionPermission>();

    // Tenant Roles (per-agency custom roles)
    public DbSet<TenantRole> TenantRoles => Set<TenantRole>();
    public DbSet<TenantRolePermission> TenantRolePermissions => Set<TenantRolePermission>();
    public DbSet<TenantUserRole> TenantUserRoles => Set<TenantUserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // JsonDocument value converter — enables InMemory provider for tests
        // and provides serialization for PostgreSQL JSONB columns
        var jsonDocConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<System.Text.Json.JsonDocument?, string?>(
            v => v == null ? null : v.RootElement.GetRawText(),
            v => string.IsNullOrEmpty(v) ? null : System.Text.Json.JsonDocument.Parse(v, default));

        modelBuilder.Entity<Candidate>(entity =>
        {
            entity.Property(c => c.CurrentStatusValues).HasConversion(jsonDocConverter);
            entity.Property(c => c.VisibleInStages)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? Array.Empty<Guid>() : System.Text.Json.JsonSerializer.Deserialize<Guid[]>(v, (System.Text.Json.JsonSerializerOptions?)null)!);
        });

        modelBuilder.Entity<WorkflowEvent>(entity =>
        {
            entity.Property(e => e.Data).HasConversion(jsonDocConverter!);
        });

        modelBuilder.Entity<WorkflowSnapshot>(entity =>
        {
            entity.Property(s => s.StatusValues).HasConversion(jsonDocConverter!);
            entity.Property(s => s.VisibleInStages)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? Array.Empty<Guid>() : System.Text.Json.JsonSerializer.Deserialize<Guid[]>(v, (System.Text.Json.JsonSerializerOptions?)null)!);
        });

        modelBuilder.Entity<WorkflowTransitionRule>(entity =>
        {
            entity.Property(r => r.Conditions).HasConversion(jsonDocConverter!);
            entity.Property(r => r.RequiredFields)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? Array.Empty<string>() : System.Text.Json.JsonSerializer.Deserialize<string[]>(v, (System.Text.Json.JsonSerializerOptions?)null)!);
            entity.Property(r => r.AllowedRoles)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? Array.Empty<string>() : System.Text.Json.JsonSerializer.Deserialize<string[]>(v, (System.Text.Json.JsonSerializerOptions?)null)!);

            entity.HasOne(r => r.SourceStage)
                .WithMany()
                .HasForeignKey(r => r.SourceStageId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.TargetStage)
                .WithMany()
                .HasForeignKey(r => r.TargetStageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MirrorViewRule>(entity =>
        {
            entity.Property(r => r.Conditions).HasConversion(jsonDocConverter!);

            entity.HasOne(m => m.WorkflowStage)
                .WithMany(s => s.MirrorViewRules)
                .HasForeignKey(m => m.WorkflowStageId);

            entity.HasOne(m => m.TargetStage)
                .WithMany()
                .HasForeignKey(m => m.TargetStageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TenantInfo>(entity =>
        {
            entity.Property(t => t.Settings)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? new TenantSettings() : System.Text.Json.JsonSerializer.Deserialize<TenantSettings>(v, (System.Text.Json.JsonSerializerOptions?)null)!);
        });

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.AllowedIpRanges)
                .HasConversion(
                    v => v == null ? null : System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? null : System.Text.Json.JsonSerializer.Deserialize<string[]>(v, (System.Text.Json.JsonSerializerOptions?)null));

            entity.HasOne(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId)
                .IsRequired(false);

            entity.HasOne(u => u.StaffProfile)
                .WithOne()
                .HasForeignKey<StaffProfile>("UserId")
                .IsRequired(false);

            entity.Ignore(u => u.RefreshTokens);
            entity.Ignore(u => u.UserSessions);
            entity.Ignore(u => u.PasswordHistories);
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasOne(d => d.HeadUser)
                .WithMany()
                .HasForeignKey(d => d.HeadUserId)
                .IsRequired(false);
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .IsRequired();

            entity.HasOne(s => s.ImpersonatedByUser)
                .WithMany()
                .HasForeignKey(s => s.ImpersonatedByUserId)
                .IsRequired(false);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });
        });

        modelBuilder.Entity<TenantRolePermission>(entity =>
        {
            entity.HasKey(trp => new { trp.TenantRoleId, trp.PermissionCode });
        });

        modelBuilder.Entity<TenantUserRole>(entity =>
        {
            entity.HasKey(tur => new { tur.UserId, tur.TenantRoleId });
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.CreatedBy = _currentUserService.UserId?.ToString();
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedBy = _currentUserService.UserId?.ToString();
                    break;
            }
        }

        var domainEvents = ChangeTracker.Entries<BaseEntity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        if (_domainEventDispatcher is not null && domainEvents.Count > 0)
        {
            await _domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);

            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                entry.Entity.ClearDomainEvents();
            }
        }

        return result;
    }
}
