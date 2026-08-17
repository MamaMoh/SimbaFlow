# Code Generation Plan — Unit 2: Candidate & Workflow Engine

## Unit Context
- **Unit**: Candidate & Workflow Engine (Unit 2)
- **Workspace Root**: /Users/mama/Dev/simbaflow
- **Stories**: US-1.01–US-1.10, US-2.01–US-2.10 (20 stories)
- **Dependencies**: Unit 1 (schema-per-tenant, SignalR, file storage)

---

## Progress (reassessed 2026-07-21; engine batch completed same day)

See `aidlc-docs/construction/candidate-workflow/code-generation-progress.md` for full evidence.
**Done: 23 | Partial: 0 | Deferred: 0** (100% — UI completeness pass after Answer D)

## Code Generation Steps

### Phase A: Domain Layer

- [x] **Step 1**: Create candidate domain entities — DONE
  - Create: `backend/src/SimbaFlow.Domain/Entities/Candidates/Candidate.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Candidates/CandidateDocument.cs`
  - Create: `backend/src/SimbaFlow.Domain/Enums/CandidateStatus.cs`
  - Create: `backend/src/SimbaFlow.Domain/Enums/Gender.cs` (if not reusing existing)
  - Create: `backend/src/SimbaFlow.Domain/Enums/DocumentType.cs`

- [x] **Step 2**: Create workflow event sourcing entities — DONE
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/WorkflowEvent.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/WorkflowSnapshot.cs`
  - Create: `backend/src/SimbaFlow.Domain/Enums/WorkflowEventType.cs`

- [x] **Step 3**: Create workflow configuration entities — DONE
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/WorkflowDefinition.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/WorkflowStage.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/WorkflowStageStatus.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/WorkflowTransitionRule.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/ParallelTrackDefinition.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/MirrorViewRule.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/StageMandatoryField.cs`
  - Create: `backend/src/SimbaFlow.Domain/Enums/StageType.cs`

- [x] **Step 4**: Create domain events — DONE
  - Create: `backend/src/SimbaFlow.Domain/Events/CandidateRegisteredEvent.cs`
  - Create: `backend/src/SimbaFlow.Domain/Events/CandidateStageChangedEvent.cs`
  - Create: `backend/src/SimbaFlow.Domain/Events/CandidateStatusChangedEvent.cs`

### Phase B: Infrastructure Layer

- [x] **Step 5**: Update DbContext — add new DbSets — DONE (2026-07-21)
  - `ITenantDbContext` extended with all workflow DbSets; `TenantDbContext` already had them

- [x] **Step 6**: Create EF Core entity configurations — DONE (indexes inline, 2026-07-21)
  - Indexes added in `TenantDbContext.OnModelCreating` (passport unique, labour_id, stage, office, event seq)

- [x] **Step 7**: Create workflow engine service — DONE (2026-07-21)
  - Done: `IWorkflowEngineService.cs`, `ConditionEvaluator.cs`, `WorkflowState`, **`WorkflowEngineService.cs`**

- [x] **Step 8**: Create CV generation service — DONE (2026-07-21)
  - `ICvGenerationService` + `CvGenerationService` (QuestPDF Community)

- [x] **Step 9**: Create default workflow seeder — DONE (2026-07-21)
  - Create: `backend/src/SimbaFlow.Infrastructure/Persistence/Seeds/WorkflowSeeder.cs`
  - Hooked into `ProvisionTenantCommand` (DDL + seed)

- [x] **Step 10**: Register new services in DI — DONE (2026-07-21)
  - `IWorkflowEngineService` → `WorkflowEngineService`
  - `ICvGenerationService` → `CvGenerationService`

### Phase C: API Layer — Candidate Module

- [x] **Step 11**: Create Candidate API module — DONE (2026-07-21)
  - Routes + Register/Update/Delete/Upload + queries + **GenerateCV** (PDF + store as CandidateDocument)

- [x] **Step 12**: Create Candidate validators — DONE (2026-07-21)
  - `Validators/RegisterCandidateValidator.cs`
  - `Validators/UpdateCandidateValidator.cs`

### Phase D: API Layer — Workflow Module

- [x] **Step 13**: Create Workflow API module — DONE (2026-07-21)
  - ExecuteTransition, UpdateStatus, GetAvailableActions, GetViewCandidates, GetWorkflowState, GetWorkflowEvents wired to engine/EF

- [x] **Step 14**: Create Workflow Configuration API — DONE (2026-07-21)
  - CreateStage, UpdateStage, CreateTransitionRule, ConfigureParallelTracks, GetWorkflowDefinition implemented
  - Route: `PUT /api/workflow/config/stages/{stageId}/tracks`

- [x] **Step 15**: Create domain event handlers (SignalR broadcast) — DONE (2026-07-21)
  - `CandidateStageChangedHandler.cs`, `CandidateStatusChangedHandler.cs`

### Phase E: Frontend

- [x] **Step 16**: Create candidate pages and components — DONE (2026-07-21)
  - List + register (prior); **detail page**, document uploader/list, timeline

- [x] **Step 17**: Create workflow view pages and components — DONE (2026-07-21)
  - Stage board page resolves nav slugs → stage IDs; action-button-bar, status-badge, workflow-view-table

- [x] **Step 18**: Create workflow configuration admin page — DONE (2026-07-21)
  - `frontend/app/(main)/admin/workflow/page.tsx`
  - `workflow-config-editor.tsx`, `condition-builder.tsx`, `stage-editor.tsx`, `create-transition-sheet.tsx`
  - Also: Offices CRUD (`/offices`), Partners/Accounting/Reports/Settings/Overview page standards + PageAlert/toasts

- [x] **Step 19**: Create TypeScript types and API hooks — DONE (2026-07-21)
  - Types (prior); `lib/api/candidates.ts`, `lib/api/workflow.ts`

### Phase F: Tests

- [x] **Step 20**: Backend unit tests — DONE (2026-07-21)
  - ConditionEvaluatorTests, WorkflowEngineServiceTests, CandidateSignalRHandlerTests

- [x] **Step 21**: Property-based tests (FsCheck) — DONE (2026-07-21)
  - `WorkflowEngineProperties.cs` — replay idempotence, snapshot round-trip, condition determinism, contradictory AND, stateful compose, mirror activate/deactivate
  - 25 tests passed

### Phase G: Migration & Documentation

- [x] **Step 22**: Create EF Core migration — DONE (2026-07-21)
  - `Migrations/Tenant/20260721075312_InitialTenant.cs`
  - `ITenantSchemaMigrator` / `TenantSchemaMigrator` applies per-schema via search_path
  - Wired in Program.cs (dev) + ProvisionTenantCommand

- [x] **Step 23**: Generate code summary documentation — DONE (2026-07-21)
  - `aidlc-docs/construction/candidate-workflow/code/code-summary.md`

---

## Story Traceability

| Step(s) | Story | Coverage |
|---------|-------|----------|
| 1, 5, 11 | US-1.01 (Register) | Full |
| 1, 11 | US-1.02 (Upload Documents) | Full |
| 11 | US-1.03 (Search) | Full |
| 11 | US-1.04 (Filter) | Full |
| 2, 7, 13 | US-1.05 (Timeline) | Full |
| 8, 11 | US-1.06 (Generate CV) | Full |
| 11 | US-1.07 (Edit) | Full |
| 11 | US-1.08 (Soft Delete) | Full |
| 11 | US-1.09 (View Documents) | Full |
| 11 | US-1.10 (Labour ID) | Full |
| 3, 6, 14 | US-2.01 (View Config) | Full |
| 14 | US-2.02 (Add Stage) | Full |
| 14 | US-2.03 (Define Transitions) | Full |
| 14 | US-2.04 (Action Buttons) | Full |
| 14 | US-2.05 (Stage Statuses) | Full |
| 3, 14 | US-2.06 (Parallel Tracks) | Full |
| 3, 14 | US-2.07 (Mirror Views) | Full |
| 14 | US-2.08 (Reorder Stages) | Full |
| 3, 14 | US-2.09 (Mandatory Fields) | Full |
| 9 | US-2.10 (Default Template) | Full |

## Estimated Scope
- **Total Steps**: 23
- **Files Created**: ~55
- **Files Modified**: ~5
- **Tests**: Unit tests + 5 PBT properties (FsCheck)
