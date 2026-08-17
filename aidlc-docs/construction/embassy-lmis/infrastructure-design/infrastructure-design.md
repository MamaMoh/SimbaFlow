# Infrastructure Design — Unit 3: Embassy & LMIS Processing

## Deployment context

Unit 3 runs in the **same Docker Compose stack** (api + frontend + postgres). No new containers, volumes, networks, or external services.

Infrastructure work is:

1. Small tenant-schema schema delta (`stage_entered_at`)
2. Workflow definition upgrader (Case Executive stage + mirror rule)
3. Platform permission seed (`embassy.*`, `lmis.*`)
4. Engine helpers + Carter module registration
5. Frontend named routes (app code only)

---

## 1. Database schema delta (tenant)

### 1a. Candidate — days-in-stage support

```sql
ALTER TABLE candidates
  ADD COLUMN IF NOT EXISTS stage_entered_at TIMESTAMPTZ;

-- Backfill: use registered_at or last stage transition approx
UPDATE candidates
SET stage_entered_at = COALESCE(stage_entered_at, registered_at, created_at)
WHERE stage_entered_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_candidates_stage_entered
  ON candidates (current_stage_id, stage_entered_at DESC)
  WHERE is_deleted = FALSE AND status = 0;
```

Set `StageEnteredAt = UtcNow` on every successful `StageTransitioned` in `WorkflowEngineService`.

### 1b. No new tables

Embassy/LMIS/Case Executive continue to use:

- `candidates` (status JSONB + `visible_in_stages`)
- `workflow_events` / `workflow_snapshots`
- `workflow_stages`, `mirror_view_rules`, `workflow_transition_rules`, …
- `candidate_documents` (`document_type = LMIS`)

### 1c. EF Core migration

```
dotnet ef migrations add AddCandidateStageEnteredAt \
  --project src/SimbaFlow.Infrastructure \
  --context TenantDbContext \
  --output-dir Migrations/Tenant
```

Applied via existing `ITenantSchemaMigrator.MigrateAllActiveTenantsAsync()` / provision path.

---

## 2. Workflow definition upgrader

### Problem

`WorkflowSeeder` is idempotent (`return` if any active definition exists). Existing tenants never get Case Executive artifacts.

### Solution

```
IWorkflowDefinitionUpgrader
  EnsureUnit3ArtifactsAsync(ITenantDbContext, tenantId, ct)
```

| Step | Action |
|------|--------|
| 1 | Load active `WorkflowDefinition` + stages |
| 2 | If stage named `"Case Executive"` missing → insert (SortOrder between Embassy and LMIS, e.g. 35 or reorder) |
| 3 | Ensure Embassy → Case Executive `MirrorViewRule` (visa Ready OR Submitted) |
| 4 | Confirm Embassy → LMIS mirror (Fit ∧ Book Done) still present |
| 5 | Confirm transitions: To Embassy, To LMIS (visa=Issued), To Ticket (milestone=Issued) |
| 6 | SaveChanges; idempotent on re-run |

**New tenants**: either call upgrader after seed, or fold Case Executive into `WorkflowSeeder` directly (preferred) **and** still run upgrader for safety.

**Hook**: after tenant EF migrate in `TenantSchemaMigrator` and after provision seed.

### Case Executive stage properties

| Field | Value |
|-------|-------|
| Name | `Case Executive` |
| StageType | Simple |
| IsInitial / IsFinal | false |
| Statuses | optional none (uses visa track on Embassy) |

---

## 3. Platform permissions seed

Add system permission codes (public schema / platform seed):

```
embassy.view
embassy.update
embassy.case_view
embassy.case_submit
embassy.visa_outcome
lmis.view
lmis.update
lmis.document
```

Optional default tenant role mappings (new tenants only):

| Role | Permissions |
|------|-------------|
| Embassy Officer | embassy.view, embassy.update, embassy.visa_outcome, lmis.view, lmis.update, lmis.document, workflow.execute, candidate.read |
| Case Executive | embassy.case_view, embassy.case_submit, candidate.read |
| Office Manager | all of above + workflow.view |

Existing tenants: permissions available for Admin to assign; no forced role rewrite.

---

## 4. Application layer wiring

### DI (`Infrastructure` / `API` ServiceExtensions)

```
services.AddScoped<IWorkflowDefinitionUpgrader, WorkflowDefinitionUpgrader>();
// Engine already registered; extend implementation with:
//   UpdateStatusChainAsync
//   ClearSourceAndRelatedMirrors on RemoveFromSource transitions
```

### Carter modules

```
Features/Embassy/EmbassyModule.cs
Features/Lmis/LmisModule.cs
```

Registered via existing Carter discovery (`AddCarter` assembly scan) — no Program.cs special-case if convention matches other modules.

### Engine behavioral gaps to close in code gen

1. **`UpdateStatusChainAsync`** — N status updates, one transaction, one denormalize pass, mirror eval once at end  
2. **Mirror cleanup on To LMIS** — remove Case Executive (and other mirrors of source) from `VisibleInStages` when `RemoveFromSource`  
3. **`StageEnteredAt`** update on stage transition  

---

## 5. File storage

No path changes. LMIS documents:

```
/data/tenants/{slug}/candidates/{candidateId}/{filename}
DocumentType = LMIS
```

Reuse `IFileStorageService` + existing candidate document commands (permission-gated with `lmis.document`).

---

## 6. Frontend infrastructure

No new Next.js services. Add routes under existing app shell:

```
app/(main)/workflow/embassy/page.tsx
app/(main)/workflow/case-executive/page.tsx
app/(main)/workflow/lmis/page.tsx
```

Nav: add/adjust sidebar links with permission gates. Proxy path already covers `/api/*` via existing BFF.

SignalR: subscribe on these pages (or shared provider) and `mutate` board keys — app code only.

---

## 7. Observability & ops

| Concern | Approach |
|---------|----------|
| Logging | Existing Serilog; redact rejection free-text at Info |
| Health | Unchanged `/health` + `/health/ready` |
| Backups | Unchanged nightly `pg_dump` (covers new column) |
| Config | No new env vars required for Unit 3 |

---

## 8. Test infrastructure

| Suite | Location |
|-------|----------|
| Example tests | `SimbaFlow.API.Tests/Services/Embassy*`, `Lmis*` |
| PBT | `SimbaFlow.API.Tests/Properties/EmbassyLmisProperties.cs` |
| InMemory / Testcontainers | Follow Unit 2 WorkflowEngine test patterns |

No new CI jobs — existing `dotnet test` pipeline.

---

## 9. Rollback / risk

| Change | Risk | Mitigation |
|--------|------|------------|
| `stage_entered_at` column | Low | Nullable + backfill |
| Case Executive stage insert | Medium if SortOrder clashes | Stable name lookup; sort between Embassy/LMIS |
| Mirror rule insert | Low | Idempotent by target stage id |
| Permission codes | Low | Additive only |

No Docker image contract changes beyond normal API rebuild.

---

## 10. Deliverable checklist (for code generation plan)

- [ ] Tenant migration: `StageEnteredAt`
- [ ] `WorkflowSeeder` + `WorkflowDefinitionUpgrader` Unit 3 artifacts
- [ ] Platform permission seed
- [ ] Engine: status chain, mirror cleanup, StageEnteredAt
- [ ] EmbassyModule + LmisModule + validators
- [ ] Frontend three boards + API clients
- [ ] Tests (example + FsCheck)
- [ ] Code summary under `construction/embassy-lmis/code/`
