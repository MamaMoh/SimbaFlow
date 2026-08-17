# Code Summary — Unit 2: Candidate & Workflow Engine

**Completed**: 2026-07-21  
**Stories**: US-1.01–US-1.10, US-2.01–US-2.10  
**Status**: Code Generation complete including Step 18 + nav UI completeness pass

---

## Architecture delivered

- Single candidate aggregate + append-only `workflow_events` (not AppSheet-style stage tables)
- Denormalized `CurrentStageId` / `CurrentStatusValues` / `VisibleInStages` for stage-board queries
- `WorkflowEngineService` — state replay, transitions, status updates, mirror views, available actions, snapshots every 20 events
- Platform vs Tenant DbContexts; tenant schemas migrated via `ITenantSchemaMigrator` + `search_path`

---

## Backend — Domain

| Area | Files |
|------|--------|
| Candidates | `Entities/Candidates/Candidate.cs`, `CandidateDocument.cs` |
| Workflow config | `WorkflowDefinition`, `WorkflowStage`, `WorkflowStageStatus`, `WorkflowTransitionRule`, `ParallelTrackDefinition`, `MirrorViewRule`, `StageMandatoryField` |
| Event store | `WorkflowEvent`, `WorkflowSnapshot` |
| Enums | `CandidateStatus`, `DocumentType`, `WorkflowEventType`, `StageType` |
| Domain events | `CandidateRegisteredEvent`, `CandidateStageChangedEvent`, `CandidateStatusChangedEvent` |

## Backend — Infrastructure

| Area | Files |
|------|--------|
| Engine | `Workflow/IWorkflowEngineService.cs`, `WorkflowEngineService.cs`, `ConditionEvaluator.cs` |
| CV | `Services/CvGenerationService.cs` + `ICvGenerationService` (QuestPDF) |
| Seed | `Seeds/WorkflowSeeder.cs` (8-stage default template) |
| SignalR | `DomainEvents/CandidateStageChangedHandler.cs`, `CandidateStatusChangedHandler.cs` |
| Persistence | `TenantDbContext` (+ indexes), `TenantDbContextFactory`, `TenantSchemaMigrator` |
| Migration | `Migrations/Tenant/20260721075312_InitialTenant.cs` |

## Backend — API

| Module | Endpoints / handlers |
|--------|----------------------|
| Candidates | CRUD, documents, timeline, Generate CV |
| Workflow runtime | actions, transition, status, state, events, stage views |
| Workflow config | get definition, create/update stage, transitions, parallel tracks |
| Tenants | Provision uses EF tenant migrate + workflow seed |

Validators: `RegisterCandidateValidator`, `UpdateCandidateValidator`.

## Frontend

| Area | Files |
|------|--------|
| API hooks | `lib/api/candidates.ts`, `lib/api/workflow.ts` |
| Candidates | list, `/candidates/[id]` detail, document uploader/list, timeline (+ PageAlert/toasts) |
| Workflow boards | `/workflow/[stageId]` with slug→stage resolve, table, status badge, action buttons |
| Workflow admin | `/admin/workflow` — stages/transitions editor, condition builder |
| Offices | `/offices` CRUD via Departments API; `/departments` redirects |
| Shell pages | Partners, Accounting, Reports, Settings, Overview — standard layout + alerts/toasts |
| Shared | `components/ui/page-alert.tsx` (AccessDenied, LoadError, PageAlert) |
| Types | `types/candidate.ts`, `types/workflow.ts` |

## Tests

- `ConditionEvaluatorTests`, `WorkflowEngineServiceTests`, `CandidateSignalRHandlerTests`
- `WorkflowEngineProperties` (FsCheck)
- **25/25 passed** (last run 2026-07-21)

---

## Deferred / follow-ups

| Item | Notes |
|------|--------|
| Partners / Accounting / Reports backends | UI shells exist; APIs ship in later units — Create/Run show clear toast errors |
| Step 6 — Extract Fluent configs | Indexes added inline; separate `Configurations/*` optional |
| Frontend SignalR → SWR invalidate | Backend broadcasts; client subscribe/mutate not wired yet |
| Native `uuid[]` / `jsonb` columns | Currently text converters for InMemory compatibility; production may prefer native types |

---

## How to apply tenant migrations

```bash
# Generate (already done for InitialTenant)
dotnet ef migrations add <Name> --project src/SimbaFlow.Infrastructure \
  --context TenantDbContext --output-dir Migrations/Tenant

# Runtime: Program.cs (Development) calls ITenantSchemaMigrator.MigrateAllActiveTenantsAsync()
# Provision: EnsureSchemaAndMigrateAsync(schema) then WorkflowSeeder
```

---

## Story coverage (plan)

All US-1.* and US-2.* mapped in `candidate-workflow-code-generation-plan.md` are implemented at API/engine/admin UI level. Live SignalR cache invalidation on the frontend remains a follow-up.
