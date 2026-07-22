using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SimbaFlow.Domain.Entities.Agency;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Workflow;

namespace SimbaFlow.Infrastructure.Persistence.Configurations;

public class OfficeConfiguration : IEntityTypeConfiguration<Office>
{
    public void Configure(EntityTypeBuilder<Office> builder)
    {
        builder.ToTable("offices");
        builder.HasIndex(o => o.Code).IsUnique();
        builder.Property(o => o.Name).HasMaxLength(200).IsRequired();
        builder.Property(o => o.Code).HasMaxLength(50).IsRequired();
    }
}

public class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        builder.ToTable("partners");
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
    }
}

public class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.ToTable("candidates");
        builder.HasIndex(c => c.PassportNumber).IsUnique();
        builder.HasIndex(c => c.ApplicationNo).IsUnique();
        builder.HasIndex(c => c.LabourId).IsUnique().HasFilter("\"LabourId\" IS NOT NULL");
        builder.HasIndex(c => c.CurrentStageId);
        builder.HasIndex(c => c.OfficeId);
        builder.HasIndex(c => c.CurrentStageEnteredAt);

        builder.Property(c => c.ApplicationNo).HasMaxLength(50).IsRequired();
        builder.Property(c => c.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.LastName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.PassportNumber).HasMaxLength(20).IsRequired();
        builder.Property(c => c.LastActionLabel).HasMaxLength(200);

        builder.HasOne(c => c.Office)
            .WithMany()
            .HasForeignKey(c => c.OfficeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Placement)
            .WithOne(p => p.Candidate)
            .HasForeignKey<CandidatePlacement>(p => p.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Skills)
            .WithOne(s => s.Candidate)
            .HasForeignKey<CandidateSkills>(s => s.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Commission)
            .WithOne(c => c.Candidate)
            .HasForeignKey<CandidateCommission>(c => c.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Relatives)
            .WithOne(r => r.Candidate)
            .HasForeignKey(r => r.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Documents)
            .WithOne(d => d.Candidate)
            .HasForeignKey(d => d.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.StageStays)
            .WithOne(s => s.Candidate)
            .HasForeignKey(s => s.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.StepStays)
            .WithOne(s => s.Candidate)
            .HasForeignKey(s => s.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.ReturnedRecords)
            .WithOne(r => r.Candidate)
            .HasForeignKey(r => r.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Complaints)
            .WithOne(c => c.Candidate)
            .HasForeignKey(c => c.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CandidatePlacementConfiguration : IEntityTypeConfiguration<CandidatePlacement>
{
    public void Configure(EntityTypeBuilder<CandidatePlacement> builder)
    {
        builder.ToTable("candidate_placements");
        builder.HasIndex(p => p.CandidateId).IsUnique();
        builder.Property(p => p.Salary).HasPrecision(18, 2);
    }
}

public class CandidateRelativeConfiguration : IEntityTypeConfiguration<CandidateRelative>
{
    public void Configure(EntityTypeBuilder<CandidateRelative> builder)
    {
        builder.ToTable("candidate_relatives");
        builder.Property(r => r.RelativeName).HasMaxLength(200).IsRequired();
    }
}

public class CandidateSkillsConfiguration : IEntityTypeConfiguration<CandidateSkills>
{
    public void Configure(EntityTypeBuilder<CandidateSkills> builder)
    {
        builder.ToTable("candidate_skills");
        builder.HasIndex(s => s.CandidateId).IsUnique();
        builder.Property(s => s.Height).HasPrecision(8, 2);
        builder.Property(s => s.Weight).HasPrecision(8, 2);
    }
}

public class CandidateStageStayConfiguration : IEntityTypeConfiguration<CandidateStageStay>
{
    public void Configure(EntityTypeBuilder<CandidateStageStay> builder)
    {
        builder.ToTable("candidate_stage_stays");
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.CandidateId, s.IsCurrent });
        builder.HasIndex(s => s.StageId);
        builder.Property(s => s.StageName).HasMaxLength(100).IsRequired();
    }
}

public class CandidateStepStayConfiguration : IEntityTypeConfiguration<CandidateStepStay>
{
    public void Configure(EntityTypeBuilder<CandidateStepStay> builder)
    {
        builder.ToTable("candidate_step_stays");
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.CandidateId, s.TrackKey, s.FinishedAt });
        builder.Property(s => s.TrackKey).HasMaxLength(50).IsRequired();
        builder.Property(s => s.StatusValue).HasMaxLength(50).IsRequired();

        builder.HasOne(s => s.StageStay)
            .WithMany()
            .HasForeignKey(s => s.StageStayId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class CandidateReturnedConfiguration : IEntityTypeConfiguration<CandidateReturned>
{
    public void Configure(EntityTypeBuilder<CandidateReturned> builder)
    {
        builder.ToTable("candidate_returned");
    }
}

public class CandidateComplaintConfiguration : IEntityTypeConfiguration<CandidateComplaint>
{
    public void Configure(EntityTypeBuilder<CandidateComplaint> builder)
    {
        builder.ToTable("candidate_complaints");
        builder.Property(c => c.ComplaintText).IsRequired();
        builder.Property(c => c.Status).HasMaxLength(50);
    }
}

public class CandidateCommissionConfiguration : IEntityTypeConfiguration<CandidateCommission>
{
    public void Configure(EntityTypeBuilder<CandidateCommission> builder)
    {
        builder.ToTable("candidate_commissions");
        builder.HasIndex(c => c.CandidateId).IsUnique();
        builder.Property(c => c.Amount).HasPrecision(18, 2);
        builder.Property(c => c.Currency).HasMaxLength(10);
    }
}

public class CandidateDocumentConfiguration : IEntityTypeConfiguration<CandidateDocument>
{
    public void Configure(EntityTypeBuilder<CandidateDocument> builder)
    {
        builder.ToTable("candidate_documents");
        builder.Property(d => d.FileName).HasMaxLength(255).IsRequired();
        builder.Property(d => d.OriginalFileName).HasMaxLength(255).IsRequired();
    }
}

public class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("workflow_definitions");
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.HasMany(d => d.Stages)
            .WithOne(s => s.WorkflowDefinition)
            .HasForeignKey(s => s.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(d => d.TransitionRules)
            .WithOne()
            .HasForeignKey(r => r.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkflowStageConfiguration : IEntityTypeConfiguration<WorkflowStage>
{
    public void Configure(EntityTypeBuilder<WorkflowStage> builder)
    {
        builder.ToTable("workflow_stages");
        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => new { s.WorkflowDefinitionId, s.SortOrder });
    }
}

public class WorkflowEventConfiguration : IEntityTypeConfiguration<WorkflowEvent>
{
    public void Configure(EntityTypeBuilder<WorkflowEvent> builder)
    {
        builder.ToTable("workflow_events");
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.CandidateId, e.SequenceNumber }).IsUnique();
        builder.HasIndex(e => e.Timestamp);
    }
}

public class WorkflowSnapshotConfiguration : IEntityTypeConfiguration<WorkflowSnapshot>
{
    public void Configure(EntityTypeBuilder<WorkflowSnapshot> builder)
    {
        builder.ToTable("workflow_snapshots");
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => new { s.CandidateId, s.SequenceNumber });
    }
}

public class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> builder)
    {
        builder.ToTable("task_assignments");
        builder.HasIndex(t => new { t.StaffUserId, t.TrackKey }).IsUnique();
        builder.Property(t => t.TrackKey).HasMaxLength(50).IsRequired();
    }
}

public class StatusTransitionPermissionConfiguration : IEntityTypeConfiguration<StatusTransitionPermission>
{
    public void Configure(EntityTypeBuilder<StatusTransitionPermission> builder)
    {
        builder.ToTable("status_transition_permissions");
        builder.HasIndex(p => new { p.TrackKey, p.ToStatus, p.AllowedRoleCode }).IsUnique();
        builder.Property(p => p.TrackKey).HasMaxLength(50).IsRequired();
        builder.Property(p => p.ToStatus).HasMaxLength(50).IsRequired();
        builder.Property(p => p.AllowedRoleCode).HasMaxLength(100).IsRequired();
    }
}
