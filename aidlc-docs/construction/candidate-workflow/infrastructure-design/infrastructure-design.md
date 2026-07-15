# Infrastructure Design — Unit 2: Candidate & Workflow Engine

## Deployment Context

Unit 2 runs within the same Docker Compose stack from Unit 1. No new containers or services are needed. The infrastructure concerns are:
1. Database schema (new tables in tenant schemas)
2. EF Core migration strategy
3. QuestPDF dependency (included in API container)
4. File storage paths (already configured in Unit 1)

---

## 1. Database Schema (Tenant Schema — Per Agency)

### New Tables (created in each tenant schema via migrations)

```sql
-- Core candidate table
CREATE TABLE candidates (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    middle_name VARCHAR(100),
    passport_number VARCHAR(20) NOT NULL,
    labour_id VARCHAR(50),
    nationality VARCHAR(100),
    date_of_birth DATE NOT NULL,
    gender SMALLINT NOT NULL,
    phone_number VARCHAR(20),
    email VARCHAR(200),
    address VARCHAR(500),
    city VARCHAR(100),
    country VARCHAR(100),
    country_of_travel VARCHAR(100),
    office_name VARCHAR(200),
    contract_date DATE,
    office_id UUID NOT NULL,
    photo_path VARCHAR(500),
    status SMALLINT NOT NULL DEFAULT 0,
    current_stage_id UUID,
    current_stage_name VARCHAR(100),
    current_status_values JSONB DEFAULT '{}',
    visible_in_stages UUID[] DEFAULT '{}',
    registered_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    registered_by VARCHAR(200),
    -- BaseEntity fields
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(200),
    updated_at TIMESTAMPTZ,
    updated_by VARCHAR(200),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version xid NOT NULL DEFAULT txid_current(),
    
    CONSTRAINT uq_candidates_passport UNIQUE (passport_number),
    CONSTRAINT uq_candidates_labour_id UNIQUE (labour_id) 
);

-- Candidate documents
CREATE TABLE candidate_documents (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    candidate_id UUID NOT NULL REFERENCES candidates(id),
    file_name VARCHAR(255) NOT NULL,
    original_file_name VARCHAR(255) NOT NULL,
    content_type VARCHAR(100) NOT NULL,
    file_path VARCHAR(500) NOT NULL,
    thumbnail_path VARCHAR(500),
    document_type SMALLINT NOT NULL,
    file_size_bytes BIGINT NOT NULL,
    uploaded_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    uploaded_by VARCHAR(200),
    -- BaseEntity fields
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(200),
    updated_at TIMESTAMPTZ,
    updated_by VARCHAR(200),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version xid NOT NULL DEFAULT txid_current()
);

-- Workflow event store (append-only)
CREATE TABLE workflow_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    candidate_id UUID NOT NULL REFERENCES candidates(id),
    sequence_number BIGINT NOT NULL,
    event_type SMALLINT NOT NULL,
    from_stage_id UUID,
    from_stage_name VARCHAR(100),
    to_stage_id UUID,
    to_stage_name VARCHAR(100),
    data JSONB NOT NULL DEFAULT '{}',
    user_id UUID NOT NULL,
    user_name VARCHAR(200) NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    notes TEXT,
    
    CONSTRAINT uq_workflow_events_candidate_seq 
        UNIQUE (candidate_id, sequence_number)
);

-- Workflow snapshots (periodic state cache)
CREATE TABLE workflow_snapshots (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    candidate_id UUID NOT NULL REFERENCES candidates(id),
    sequence_number BIGINT NOT NULL,
    stage_id UUID NOT NULL,
    stage_name VARCHAR(100) NOT NULL,
    status_values JSONB NOT NULL DEFAULT '{}',
    visible_in_stages UUID[] DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Workflow configuration
CREATE TABLE workflow_definitions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    version INT NOT NULL DEFAULT 1,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    -- BaseEntity fields
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(200),
    updated_at TIMESTAMPTZ,
    updated_by VARCHAR(200),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version xid NOT NULL DEFAULT txid_current()
);

CREATE TABLE workflow_stages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workflow_definition_id UUID NOT NULL REFERENCES workflow_definitions(id),
    name VARCHAR(100) NOT NULL,
    description TEXT,
    sort_order INT NOT NULL,
    stage_type SMALLINT NOT NULL DEFAULT 0,
    is_initial_stage BOOLEAN NOT NULL DEFAULT FALSE,
    is_final_stage BOOLEAN NOT NULL DEFAULT FALSE,
    -- BaseEntity fields
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(200),
    updated_at TIMESTAMPTZ,
    updated_by VARCHAR(200),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version xid NOT NULL DEFAULT txid_current()
);

CREATE TABLE workflow_stage_statuses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workflow_stage_id UUID NOT NULL REFERENCES workflow_stages(id),
    name VARCHAR(100) NOT NULL,
    sort_order INT NOT NULL,
    is_terminal BOOLEAN NOT NULL DEFAULT FALSE,
    track_name VARCHAR(100),
    color VARCHAR(7),
    -- BaseEntity fields
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(200),
    updated_at TIMESTAMPTZ,
    updated_by VARCHAR(200),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version xid NOT NULL DEFAULT txid_current()
);

CREATE TABLE workflow_transition_rules (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workflow_definition_id UUID NOT NULL REFERENCES workflow_definitions(id),
    source_stage_id UUID NOT NULL REFERENCES workflow_stages(id),
    target_stage_id UUID NOT NULL REFERENCES workflow_stages(id),
    button_label VARCHAR(100) NOT NULL,
    button_icon VARCHAR(50),
    sort_order INT NOT NULL DEFAULT 0,
    conditions JSONB DEFAULT '{}',
    required_fields TEXT[] DEFAULT '{}',
    allowed_roles TEXT[] DEFAULT '{}',
    remove_from_source BOOLEAN NOT NULL DEFAULT TRUE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    -- BaseEntity fields
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(200),
    updated_at TIMESTAMPTZ,
    updated_by VARCHAR(200),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version xid NOT NULL DEFAULT txid_current()
);

CREATE TABLE parallel_track_definitions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workflow_stage_id UUID NOT NULL REFERENCES workflow_stages(id),
    track_name VARCHAR(100) NOT NULL,
    completion_status VARCHAR(100) NOT NULL,
    sort_order INT NOT NULL,
    -- BaseEntity fields
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(200),
    updated_at TIMESTAMPTZ,
    updated_by VARCHAR(200),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version xid NOT NULL DEFAULT txid_current()
);

CREATE TABLE mirror_view_rules (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workflow_stage_id UUID NOT NULL REFERENCES workflow_stages(id),
    target_stage_id UUID NOT NULL REFERENCES workflow_stages(id),
    conditions JSONB NOT NULL DEFAULT '{}',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    -- BaseEntity fields
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(200),
    updated_at TIMESTAMPTZ,
    updated_by VARCHAR(200),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version xid NOT NULL DEFAULT txid_current()
);

CREATE TABLE stage_mandatory_fields (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    workflow_stage_id UUID NOT NULL REFERENCES workflow_stages(id),
    field_name VARCHAR(100) NOT NULL,
    transition_rule_id UUID REFERENCES workflow_transition_rules(id),
    -- BaseEntity fields
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(200),
    updated_at TIMESTAMPTZ,
    updated_by VARCHAR(200),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    row_version xid NOT NULL DEFAULT txid_current()
);
```

---

## 2. Performance Indexes

```sql
-- Candidate search (full-text)
CREATE INDEX ix_candidates_search ON candidates 
    USING GIN (to_tsvector('simple', 
        coalesce(first_name,'') || ' ' || coalesce(last_name,'') || ' ' || 
        coalesce(passport_number,'') || ' ' || coalesce(labour_id,'')));

-- Stage view queries
CREATE INDEX ix_candidates_current_stage ON candidates (current_stage_id) 
    WHERE is_deleted = FALSE AND status = 0;

-- Mirror view queries
CREATE INDEX ix_candidates_visible_stages ON candidates 
    USING GIN (visible_in_stages);

-- Office filtering
CREATE INDEX ix_candidates_office ON candidates (office_id) 
    WHERE is_deleted = FALSE;

-- Event replay
CREATE INDEX ix_workflow_events_candidate_seq 
    ON workflow_events (candidate_id, sequence_number);

-- Event timeline (timestamp queries)
CREATE INDEX ix_workflow_events_timestamp 
    ON workflow_events (timestamp DESC);

-- Snapshot lookup
CREATE INDEX ix_workflow_snapshots_candidate 
    ON workflow_snapshots (candidate_id, sequence_number DESC);

-- Document lookup
CREATE INDEX ix_candidate_documents_candidate 
    ON candidate_documents (candidate_id) WHERE is_deleted = FALSE;
```

---

## 3. EF Core Migration Strategy

### Migration Approach
- Migrations are applied to each tenant schema dynamically
- `TenantMigrationService` iterates active tenants and applies pending migrations
- On tenant provisioning: create schema → apply all migrations → seed default workflow

### DbContext Configuration (OnModelCreating)
```csharp
// Candidate configuration
modelBuilder.Entity<Candidate>(entity => {
    entity.HasIndex(c => c.PassportNumber).IsUnique();
    entity.HasIndex(c => c.LabourId).IsUnique()
        .HasFilter("labour_id IS NOT NULL");
    entity.HasIndex(c => c.CurrentStageId);
    entity.HasIndex(c => c.OfficeId);
    entity.Property(c => c.CurrentStatusValues)
        .HasColumnType("jsonb");
    entity.Property(c => c.VisibleInStages)
        .HasColumnType("uuid[]");
    entity.Property(c => c.RowVersion)
        .IsRowVersion();
});

// WorkflowEvent — append only, no RowVersion needed
modelBuilder.Entity<WorkflowEvent>(entity => {
    entity.HasIndex(e => new { e.CandidateId, e.SequenceNumber }).IsUnique();
    entity.HasIndex(e => e.Timestamp);
    entity.Property(e => e.Data).HasColumnType("jsonb");
});

// TransitionRule conditions
modelBuilder.Entity<WorkflowTransitionRule>(entity => {
    entity.Property(r => r.Conditions).HasColumnType("jsonb");
    entity.Property(r => r.RequiredFields).HasColumnType("text[]");
    entity.Property(r => r.AllowedRoles).HasColumnType("text[]");
});
```

---

## 4. File Storage (Inherited from Unit 1)

No changes to infrastructure. Unit 2 uses `IFileStorageService` from Unit 1:
- Upload path: `/data/tenants/{slug}/candidates/{candidateId}/{filename}`
- CV output path: `/data/tenants/{slug}/candidates/{candidateId}/cv_{timestamp}.pdf`
- Thumbnail path: auto-generated by `LocalFileStorageService`

---

## 5. QuestPDF Configuration

### NuGet Package
- Add `QuestPDF` to `SimbaFlow.Infrastructure.csproj`
- License: Community (free for revenue < $1M)

### Integration
- `CvGenerationService : ICvGenerationService` in Infrastructure/Services/
- Called by `GenerateCVCommand` handler
- Returns `byte[]` — handler saves to file storage and returns stream

---

## 6. No Docker Changes

Unit 2 runs entirely within the existing `api` container. No new containers, volumes, or network configuration needed. The only deployment change is the Docker image includes QuestPDF dependency (resolved at build time via NuGet restore).

---

## 7. Default Workflow Seed Data

On tenant provisioning, `WorkflowSeeder` creates:
- 1 WorkflowDefinition (name: "Default Workflow", version: 1)
- 8 WorkflowStages (with sort order 1-8)
- Status values per stage (as specified in functional design BL-10)
- Transition rules between stages
- Parallel track definitions for Embassy stage
- Mirror view rule for Embassy → LMIS

This seed runs inside the new tenant's schema immediately after schema creation.
