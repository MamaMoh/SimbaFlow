using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Partners;
using SimbaFlow.Domain.Entities.Staff;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Infrastructure.Audit;

namespace SimbaFlow.Infrastructure.Persistence;

/// <summary>
/// Platform-level DbContext. Hardcoded to "public" schema.
/// Contains: Identity, Tenants, Permissions, Audit, Staff, Partner catalog.
/// This context NEVER contains tenant business data (candidates, workflow, etc.)
/// </summary>
public class PlatformDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid,
        IdentityUserClaim<Guid>, IdentityUserRole<Guid>, IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>, IPlatformDbContext
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDomainEventDispatcher? _domainEventDispatcher;

    public PlatformDbContext(
        DbContextOptions<PlatformDbContext> options,
        ICurrentUserService currentUserService,
        IDomainEventDispatcher? domainEventDispatcher = null)
        : base(options)
    {
        _currentUserService = currentUserService;
        _domainEventDispatcher = domainEventDispatcher;
    }

    // Identity
    public DbSet<ApplicationUser> ApplicationUsers => Users;
    public DbSet<ApplicationRole> ApplicationRoles => Roles;
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<PasswordHistory> PasswordHistories => Set<PasswordHistory>();

    // Tenancy (platform-level)
    public DbSet<TenantInfo> Tenants => Set<TenantInfo>();
    public DbSet<SystemConfiguration> SystemConfigurations => Set<SystemConfiguration>();
    public DbSet<BotRegistrationChallenge> BotRegistrationChallenges => Set<BotRegistrationChallenge>();
    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    // Partner catalog (platform)
    public DbSet<PartnerAgency> PartnerAgencies => Set<PartnerAgency>();
    public DbSet<PartnerLink> PartnerLinks => Set<PartnerLink>();
    public DbSet<PartnerAgreementDocument> PartnerAgreementDocuments => Set<PartnerAgreementDocument>();

    // Audit
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ReadAuditLog> ReadAuditLogs => Set<ReadAuditLog>();

    // Staff
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<StaffIdentifier> StaffIdentifiers => Set<StaffIdentifier>();
    public DbSet<StaffDepartmentAffiliation> StaffDepartmentAffiliations => Set<StaffDepartmentAffiliation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("public");

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });
        });

        modelBuilder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.AllowedIpRanges)
                .HasConversion(
                    v => v == null ? null : System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? null : System.Text.Json.JsonSerializer.Deserialize<string[]>(v, (System.Text.Json.JsonSerializerOptions?)null));

            entity.HasIndex(u => u.TelegramChatId);

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

        modelBuilder.Entity<TenantInfo>(entity =>
        {
            entity.Property(t => t.Settings)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? new TenantSettings() : System.Text.Json.JsonSerializer.Deserialize<TenantSettings>(v, (System.Text.Json.JsonSerializerOptions?)null)!);

            entity.Property(t => t.LicensedCountries)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v ?? new List<string>(), (System.Text.Json.JsonSerializerOptions?)null),
                    v => ParseStringList(v));

            entity.Property(t => t.AgencyLevel).HasDefaultValue(5);
            entity.HasIndex(t => t.AgencyLevel);
        });

        modelBuilder.Entity<BotRegistrationChallenge>(entity =>
        {
            entity.ToTable("BotRegistrationChallenges");
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.ExpiresAt });
            entity.Property(x => x.Code).HasMaxLength(12).IsRequired();
        });

        modelBuilder.Entity<NotificationDelivery>(entity =>
        {
            entity.ToTable("NotificationDeliveries");
            entity.HasIndex(x => new { x.TenantId, x.SentAt });
            entity.Property(x => x.EventType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PayloadSummary).HasMaxLength(512).IsRequired();
            entity.Property(x => x.ExternalMessageId).HasMaxLength(128);
            entity.Property(x => x.Error).HasMaxLength(1024);
        });

        modelBuilder.Entity<PartnerAgency>(entity =>
        {
            entity.ToTable("PartnerAgencies");
            entity.HasIndex(p => p.CountryCode);
            entity.HasIndex(p => p.Name);
            entity.Property(p => p.Name).HasMaxLength(256).IsRequired();
            entity.Property(p => p.CountryCode).HasMaxLength(8).IsRequired();
            entity.Property(p => p.CountryName).HasMaxLength(128).IsRequired();
            entity.Property(p => p.ForeignLicenseId).HasMaxLength(128);
            entity.Property(p => p.ContactEmail).HasMaxLength(256);
            entity.Property(p => p.ContactPhone).HasMaxLength(64);
        });

        modelBuilder.Entity<PartnerLink>(entity =>
        {
            entity.ToTable("PartnerLinks");
            entity.HasIndex(l => new { l.TenantId, l.PartnerAgencyId }).IsUnique();
            entity.HasIndex(l => l.PartnerAgencyId);
            entity.HasOne(l => l.PartnerAgency)
                .WithMany()
                .HasForeignKey(l => l.PartnerAgencyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PartnerAgreementDocument>(entity =>
        {
            entity.ToTable("PartnerAgreementDocuments");
            // Reads are always (link + tenant), never link alone — the tenant column is the boundary.
            entity.HasIndex(d => new { d.PartnerLinkId, d.TenantId });
            entity.Property(d => d.FileName).HasMaxLength(256);
            entity.Property(d => d.OriginalFileName).HasMaxLength(256);
            entity.Property(d => d.ContentType).HasMaxLength(128);
            entity.Property(d => d.FilePath).HasMaxLength(1024);
            entity.Property(d => d.Title).HasMaxLength(256);
            entity.HasOne(d => d.PartnerLink)
                .WithMany()
                .HasForeignKey(d => d.PartnerLinkId)
                .OnDelete(DeleteBehavior.Cascade);
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
                entry.Entity.ClearDomainEvents();
        }

        return result;
    }
}
