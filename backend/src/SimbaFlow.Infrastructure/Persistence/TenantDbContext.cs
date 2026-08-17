using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Finance;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Entities.Travel;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Infrastructure.Persistence;

/// <summary>
/// Tenant-level DbContext. Schema is dynamically set per-request via TenantConnectionInterceptor.
/// Contains: Candidates, Workflow, TenantRoles, Travel/Exceptions, Finance.
/// This context NEVER contains platform data (users, tenants, permissions).
/// 
/// If no tenant context is set (SuperAdmin), queries will fail — by design.
/// This enforces security: you cannot accidentally write business data to public schema.
/// </summary>
public class TenantDbContext : DbContext, ITenantDbContext
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IDomainEventDispatcher? _domainEventDispatcher;

    public TenantDbContext(
        DbContextOptions<TenantDbContext> options,
        ICurrentUserService currentUserService,
        IDomainEventDispatcher? domainEventDispatcher = null)
        : base(options)
    {
        _currentUserService = currentUserService;
        _domainEventDispatcher = domainEventDispatcher;
    }

    // Candidates
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CandidateDocument> CandidateDocuments => Set<CandidateDocument>();

    // Workflow
    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();
    public DbSet<WorkflowStage> WorkflowStages => Set<WorkflowStage>();
    public DbSet<WorkflowStageStatus> WorkflowStageStatuses => Set<WorkflowStageStatus>();
    public DbSet<WorkflowTransitionRule> WorkflowTransitionRules => Set<WorkflowTransitionRule>();
    public DbSet<WorkflowEvent> WorkflowEvents => Set<WorkflowEvent>();
    public DbSet<WorkflowSnapshot> WorkflowSnapshots => Set<WorkflowSnapshot>();
    public DbSet<ParallelTrackDefinition> ParallelTrackDefinitions => Set<ParallelTrackDefinition>();
    public DbSet<MirrorViewRule> MirrorViewRules => Set<MirrorViewRule>();
    public DbSet<StageMandatoryField> StageMandatoryFields => Set<StageMandatoryField>();

    // Travel / Arrival exceptions
    public DbSet<ExceptionCase> ExceptionCases => Set<ExceptionCase>();
    public DbSet<InvestigationNote> InvestigationNotes => Set<InvestigationNote>();
    public DbSet<LiabilityAssignment> LiabilityAssignments => Set<LiabilityAssignment>();

    // Finance
    public DbSet<Commission> Commissions => Set<Commission>();
    public DbSet<CommissionFee> CommissionFees => Set<CommissionFee>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<FinanceCounter> FinanceCounters => Set<FinanceCounter>();

    // Tenant Roles
    public DbSet<TenantRole> TenantRoles => Set<TenantRole>();
    public DbSet<TenantRolePermission> TenantRolePermissions => Set<TenantRolePermission>();
    public DbSet<TenantUserRole> TenantUserRoles => Set<TenantUserRole>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // NO default schema — schema is set dynamically by interceptor
        // This means EF Core generates SQL without schema prefix,
        // and PostgreSQL resolves tables via search_path

        // JSON converters for InMemory test compatibility
        var jsonDocConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<System.Text.Json.JsonDocument?, string?>(
            v => v == null ? null : v.RootElement.GetRawText(),
            v => string.IsNullOrEmpty(v) ? null : System.Text.Json.JsonDocument.Parse(v, default));

        // Candidate
        modelBuilder.Entity<Candidate>(entity =>
        {
            entity.ToTable("candidates");
            entity.HasIndex(c => c.PassportNumber).IsUnique();
            entity.HasIndex(c => c.LabourId).IsUnique()
                .HasFilter("labour_id IS NOT NULL");
            entity.HasIndex(c => c.CurrentStageId);
            entity.HasIndex(c => c.OfficeId);
            entity.HasIndex(c => new { c.CurrentStageId, c.StageEnteredAt });
            entity.Property(c => c.CurrentStatusValues).HasConversion(jsonDocConverter);
            entity.Property(c => c.VisibleInStages)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? Array.Empty<Guid>() : System.Text.Json.JsonSerializer.Deserialize<Guid[]>(v, (System.Text.Json.JsonSerializerOptions?)null)!);
            entity.Property(c => c.VisaNumber).HasMaxLength(64);
            entity.Property(c => c.VisaType).HasMaxLength(64);
            entity.Property(c => c.SponsorName).HasMaxLength(256);
            entity.Property(c => c.SponsorIdNumber).HasMaxLength(64);
            entity.Property(c => c.SponsorPhone).HasMaxLength(32);
            entity.Property(c => c.SponsorAddress).HasMaxLength(512);
            entity.Property(c => c.SponsorArabicName).HasMaxLength(256);
            entity.Property(c => c.AgentName).HasMaxLength(256);
        });

        modelBuilder.Entity<CandidateDocument>(entity =>
        {
            entity.ToTable("candidate_documents");
            entity.HasIndex(d => d.CandidateId);
        });

        // Workflow
        modelBuilder.Entity<WorkflowEvent>(entity =>
        {
            entity.ToTable("workflow_events");
            entity.HasIndex(e => new { e.CandidateId, e.SequenceNumber }).IsUnique();
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.Data).HasConversion(jsonDocConverter!);
        });

        modelBuilder.Entity<WorkflowSnapshot>(entity =>
        {
            entity.ToTable("workflow_snapshots");
            entity.HasIndex(s => new { s.CandidateId, s.SequenceNumber });
            entity.Property(s => s.StatusValues).HasConversion(jsonDocConverter!);
            entity.Property(s => s.VisibleInStages)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? Array.Empty<Guid>() : System.Text.Json.JsonSerializer.Deserialize<Guid[]>(v, (System.Text.Json.JsonSerializerOptions?)null)!);
        });

        modelBuilder.Entity<WorkflowDefinition>(entity => entity.ToTable("workflow_definitions"));
        modelBuilder.Entity<WorkflowStage>(entity => entity.ToTable("workflow_stages"));
        modelBuilder.Entity<WorkflowStageStatus>(entity => entity.ToTable("workflow_stage_statuses"));

        modelBuilder.Entity<WorkflowTransitionRule>(entity =>
        {
            entity.ToTable("workflow_transition_rules");
            entity.Property(r => r.Conditions).HasConversion(jsonDocConverter!);
            entity.Property(r => r.RequiredFields)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? Array.Empty<string>() : System.Text.Json.JsonSerializer.Deserialize<string[]>(v, (System.Text.Json.JsonSerializerOptions?)null)!);
            entity.Property(r => r.AllowedRoles)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v) ? Array.Empty<string>() : System.Text.Json.JsonSerializer.Deserialize<string[]>(v, (System.Text.Json.JsonSerializerOptions?)null)!);
            entity.HasOne(r => r.SourceStage).WithMany().HasForeignKey(r => r.SourceStageId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.TargetStage).WithMany().HasForeignKey(r => r.TargetStageId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ParallelTrackDefinition>(entity => entity.ToTable("parallel_track_definitions"));
        modelBuilder.Entity<StageMandatoryField>(entity => entity.ToTable("stage_mandatory_fields"));

        modelBuilder.Entity<MirrorViewRule>(entity =>
        {
            entity.ToTable("mirror_view_rules");
            entity.Property(r => r.Conditions).HasConversion(jsonDocConverter!);
            entity.HasOne(m => m.WorkflowStage).WithMany(s => s.MirrorViewRules).HasForeignKey(m => m.WorkflowStageId);
            entity.HasOne(m => m.TargetStage).WithMany().HasForeignKey(m => m.TargetStageId).OnDelete(DeleteBehavior.Restrict);
        });

        // Travel / Arrival exceptions
        modelBuilder.Entity<ExceptionCase>(entity =>
        {
            entity.ToTable("exception_cases");
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CandidateId);
            entity.HasIndex(e => e.CandidateId)
                .IsUnique()
                .HasFilter($"is_deleted = FALSE AND status = {(int)ExceptionStatus.Open}");
            entity.HasMany(e => e.Notes).WithOne(n => n.ExceptionCase).HasForeignKey(n => n.ExceptionCaseId);
            entity.HasMany(e => e.Liabilities).WithOne(l => l.ExceptionCase).HasForeignKey(l => l.ExceptionCaseId);
            entity.Property(e => e.FinancialImpactAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<InvestigationNote>(entity =>
        {
            entity.ToTable("investigation_notes");
            entity.HasIndex(n => n.ExceptionCaseId);
            entity.Property(n => n.AttachmentDocumentIds)
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => string.IsNullOrEmpty(v)
                        ? Array.Empty<Guid>()
                        : System.Text.Json.JsonSerializer.Deserialize<Guid[]>(v, (System.Text.Json.JsonSerializerOptions?)null)!);
        });

        modelBuilder.Entity<LiabilityAssignment>(entity =>
        {
            entity.ToTable("liability_assignments");
            entity.HasIndex(l => l.ExceptionCaseId);
            entity.Property(l => l.Amount).HasPrecision(18, 2);
            entity.Property(l => l.Currency).HasMaxLength(8);
        });

        modelBuilder.Entity<Commission>(entity =>
        {
            entity.ToTable("commissions");
            entity.HasIndex(c => c.CandidateId)
                .IsUnique()
                .HasFilter("is_deleted = FALSE");
            entity.HasIndex(c => c.Status)
                .HasFilter("is_deleted = FALSE");
            entity.HasIndex(c => c.OpenedAt)
                .HasFilter("is_deleted = FALSE");
            entity.Property(c => c.CountryOfTravel).HasMaxLength(128);
            entity.Property(c => c.OfficeName).HasMaxLength(256);
            entity.Property(c => c.TotalFeesAmount).HasPrecision(18, 2);
            entity.Property(c => c.TotalPaidAmount).HasPrecision(18, 2);
            entity.Property(c => c.BalanceAmount).HasPrecision(18, 2);
            entity.HasMany(c => c.Fees).WithOne(f => f.Commission).HasForeignKey(f => f.CommissionId);
            entity.HasMany(c => c.Payments).WithOne(p => p.Commission).HasForeignKey(p => p.CommissionId);
            entity.HasMany(c => c.Disputes).WithOne(d => d.Commission).HasForeignKey(d => d.CommissionId);
        });

        modelBuilder.Entity<CommissionFee>(entity =>
        {
            entity.ToTable("commission_fees");
            entity.HasIndex(f => f.CommissionId)
                .HasFilter("is_deleted = FALSE");
            entity.Property(f => f.Amount).HasPrecision(18, 2);
            entity.Property(f => f.AmountEtb).HasPrecision(18, 2);
            entity.Property(f => f.Currency).HasMaxLength(8);
            entity.Property(f => f.Description).HasMaxLength(512);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasIndex(p => p.CommissionId)
                .HasFilter("is_deleted = FALSE");
            entity.HasIndex(p => p.JournalEntryId);
            entity.Property(p => p.Amount).HasPrecision(18, 2);
            entity.Property(p => p.AmountEtb).HasPrecision(18, 2);
            entity.Property(p => p.ExchangeRateToEtb).HasPrecision(18, 8);
            entity.Property(p => p.Currency).HasMaxLength(8);
            entity.Property(p => p.Reference).HasMaxLength(256);
            entity.HasOne(p => p.JournalEntry)
                .WithMany()
                .HasForeignKey(p => p.JournalEntryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Dispute>(entity =>
        {
            entity.ToTable("disputes");
            entity.HasIndex(d => d.CommissionId)
                .HasFilter("is_deleted = FALSE");
            entity.HasIndex(d => d.CommissionId)
                .IsUnique()
                .HasFilter($"is_deleted = FALSE AND status = {(int)DisputeStatus.Open}");
            entity.Property(d => d.Reason).HasMaxLength(2000);
            entity.Property(d => d.ResolutionNotes).HasMaxLength(2000);
        });

        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasIndex(a => a.Code)
                .IsUnique()
                .HasFilter("is_deleted = FALSE");
            entity.Property(a => a.Code).HasMaxLength(32);
            entity.Property(a => a.Name).HasMaxLength(256);
            entity.Property(a => a.Currency).HasMaxLength(8);
        });

        modelBuilder.Entity<JournalEntry>(entity =>
        {
            entity.ToTable("journal_entries");
            entity.HasIndex(j => j.EntryNumber)
                .IsUnique()
                .HasFilter("is_deleted = FALSE");
            entity.HasIndex(j => new { j.SourceType, j.SourceId });
            entity.Property(j => j.EntryNumber).HasMaxLength(64);
            entity.Property(j => j.Description).HasMaxLength(512);
            entity.Property(j => j.SourceType).HasMaxLength(64);
            entity.HasMany(j => j.Lines).WithOne(l => l.JournalEntry).HasForeignKey(l => l.JournalEntryId);
        });

        modelBuilder.Entity<JournalLine>(entity =>
        {
            entity.ToTable("journal_lines");
            entity.HasIndex(l => l.JournalEntryId);
            entity.HasIndex(l => l.AccountId);
            entity.Property(l => l.Debit).HasPrecision(18, 2);
            entity.Property(l => l.Credit).HasPrecision(18, 2);
            entity.Property(l => l.Memo).HasMaxLength(512);
            entity.HasOne(l => l.Account).WithMany().HasForeignKey(l => l.AccountId);
        });

        modelBuilder.Entity<FinanceCounter>(entity =>
        {
            entity.ToTable("finance_counters");
        });

        // Tenant Roles
        modelBuilder.Entity<TenantRole>(entity => entity.ToTable("tenant_roles"));
        modelBuilder.Entity<TenantRolePermission>(entity =>
        {
            entity.ToTable("tenant_role_permissions");
            entity.HasKey(trp => new { trp.TenantRoleId, trp.PermissionCode });
        });
        modelBuilder.Entity<TenantUserRole>(entity =>
        {
            entity.ToTable("tenant_user_roles");
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
                entry.Entity.ClearDomainEvents();
        }

        return result;
    }
}
