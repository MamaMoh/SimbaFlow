using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Staff;
using SimbaFlow.Domain.Entities.Tenancy;
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


    // Staff
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<StaffIdentifier> StaffIdentifiers => Set<StaffIdentifier>();
    public DbSet<StaffDepartmentAffiliation> StaffDepartmentAffiliations => Set<StaffDepartmentAffiliation>();

    // Candidates
    public DbSet<Domain.Entities.Candidates.Candidate> Candidates => Set<Domain.Entities.Candidates.Candidate>();
    public DbSet<Domain.Entities.Candidates.CandidateDocument> CandidateDocuments => Set<Domain.Entities.Candidates.CandidateDocument>();

    // Workflow
    public DbSet<Domain.Entities.Workflow.WorkflowDefinition> WorkflowDefinitions => Set<Domain.Entities.Workflow.WorkflowDefinition>();
    public DbSet<Domain.Entities.Workflow.WorkflowStage> WorkflowStages => Set<Domain.Entities.Workflow.WorkflowStage>();
    public DbSet<Domain.Entities.Workflow.WorkflowEvent> WorkflowEvents => Set<Domain.Entities.Workflow.WorkflowEvent>();
    public DbSet<Domain.Entities.Workflow.WorkflowSnapshot> WorkflowSnapshots => Set<Domain.Entities.Workflow.WorkflowSnapshot>();

    // Tenant Roles (per-agency custom roles)
    public DbSet<Domain.Entities.Tenancy.TenantRole> TenantRoles => Set<Domain.Entities.Tenancy.TenantRole>();
    public DbSet<Domain.Entities.Tenancy.TenantRolePermission> TenantRolePermissions => Set<Domain.Entities.Tenancy.TenantRolePermission>();
    public DbSet<Domain.Entities.Tenancy.TenantUserRole> TenantUserRoles => Set<Domain.Entities.Tenancy.TenantUserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // JsonDocument value converter — enables InMemory provider for tests
        // and provides serialization for PostgreSQL JSONB columns
        var jsonDocConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<System.Text.Json.JsonDocument?, string?>(
            v => v == null ? null : v.RootElement.GetRawText(),
            v => string.IsNullOrEmpty(v) ? null : System.Text.Json.JsonDocument.Parse(v, default));

        // Candidate
        modelBuilder.Entity<Domain.Entities.Candidates.Candidate>(entity =>
        {
            entity.Property(c => c.CurrentStatusValues).HasConversion(jsonDocConverter);
            entity.Property(c => c.VisibleInStages)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? Array.Empty<Guid>() : System.Text.Json.JsonSerializer.Deserialize<Guid[]>(v, (System.Text.Json.JsonSerializerOptions?)null)!);
        });

        // WorkflowEvent
        modelBuilder.Entity<Domain.Entities.Workflow.WorkflowEvent>(entity =>
        {
            entity.Property(e => e.Data).HasConversion(jsonDocConverter!);
        });

        // WorkflowSnapshot
        modelBuilder.Entity<Domain.Entities.Workflow.WorkflowSnapshot>(entity =>
        {
            entity.Property(s => s.StatusValues).HasConversion(jsonDocConverter!);
            entity.Property(s => s.VisibleInStages)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? Array.Empty<Guid>() : System.Text.Json.JsonSerializer.Deserialize<Guid[]>(v, (System.Text.Json.JsonSerializerOptions?)null)!);
        });

        // WorkflowTransitionRule
        modelBuilder.Entity<Domain.Entities.Workflow.WorkflowTransitionRule>(entity =>
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
                .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);

            entity.HasOne(r => r.TargetStage)
                .WithMany()
                .HasForeignKey(r => r.TargetStageId)
                .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        });

        // MirrorViewRule
        modelBuilder.Entity<Domain.Entities.Workflow.MirrorViewRule>(entity =>
        {
            entity.Property(r => r.Conditions).HasConversion(jsonDocConverter!);

            entity.HasOne(m => m.WorkflowStage)
                .WithMany(s => s.MirrorViewRules)
                .HasForeignKey(m => m.WorkflowStageId);

            entity.HasOne(m => m.TargetStage)
                .WithMany()
                .HasForeignKey(m => m.TargetStageId)
                .OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        });

        // TenantInfo — TenantSettings + licensed countries as JSON
        modelBuilder.Entity<Domain.Entities.Identity.TenantInfo>(entity =>
        {
            entity.Property(t => t.Settings)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? new Domain.Entities.Tenancy.TenantSettings() : System.Text.Json.JsonSerializer.Deserialize<Domain.Entities.Tenancy.TenantSettings>(v, (System.Text.Json.JsonSerializerOptions?)null)!);

            entity.Property(t => t.LicensedCountries)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v ?? new List<string>(), (System.Text.Json.JsonSerializerOptions?)null),
                    v => ParseStringList(v));
        });

        // ApplicationUser — relationships and conversions
        modelBuilder.Entity<Domain.Entities.Identity.ApplicationUser>(entity =>
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
                .HasForeignKey<Domain.Entities.Staff.StaffProfile>("UserId")
                .IsRequired(false);

            entity.Ignore(u => u.RefreshTokens);
            entity.Ignore(u => u.UserSessions);
            entity.Ignore(u => u.PasswordHistories);
        });

        // Department
        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasOne(d => d.HeadUser)
                .WithMany()
                .HasForeignKey(d => d.HeadUserId)
                .IsRequired(false);
        });

        // UserSession — navigations
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

        // RolePermission — composite key
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });
        });

        // TenantRolePermission — composite key
        modelBuilder.Entity<Domain.Entities.Tenancy.TenantRolePermission>(entity =>
        {
            entity.HasKey(trp => new { trp.TenantRoleId, trp.PermissionCode });
        });

        // TenantUserRole — composite key
        modelBuilder.Entity<Domain.Entities.Tenancy.TenantUserRole>(entity =>
        {
            entity.HasKey(tur => new { tur.UserId, tur.TenantRoleId });
        });
    }

    private static List<string> ParseStringList(string? v)
    {
        if (string.IsNullOrWhiteSpace(v) || v == "{}" || v == "null")
            return new List<string>();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null)
                   ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Set audit fields
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

        // Collect domain events before saving
        var domainEvents = ChangeTracker.Entries<BaseEntity>()
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        // Dispatch domain events after successful save
        if (_domainEventDispatcher is not null && domainEvents.Count > 0)
        {
            await _domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);

            // Clear events from entities
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                entry.Entity.ClearDomainEvents();
            }
        }

        return result;
    }
}
