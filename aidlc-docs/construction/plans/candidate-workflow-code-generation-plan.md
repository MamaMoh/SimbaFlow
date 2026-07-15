# Code Generation Plan — Unit 2: Candidate & Workflow Engine

## Unit Context
- **Unit**: Candidate & Workflow Engine (Unit 2)
- **Workspace Root**: /Users/mama/Dev/simbaflow
- **Stories**: US-1.01–US-1.10, US-2.01–US-2.10 (20 stories)
- **Dependencies**: Unit 1 (schema-per-tenant, SignalR, file storage)

---

## Code Generation Steps

### Phase A: Domain Layer

- [ ] **Step 1**: Create candidate domain entities
  - Create: `backend/src/SimbaFlow.Domain/Entities/Candidates/Candidate.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Candidates/CandidateDocument.cs`
  - Create: `backend/src/SimbaFlow.Domain/Enums/CandidateStatus.cs`
  - Create: `backend/src/SimbaFlow.Domain/Enums/Gender.cs` (if not reusing existing)
  - Create: `backend/src/SimbaFlow.Domain/Enums/DocumentType.cs`

- [ ] **Step 2**: Create workflow event sourcing entities
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/WorkflowEvent.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/WorkflowSnapshot.cs`
  - Create: `backend/src/SimbaFlow.Domain/Enums/WorkflowEventType.cs`

- [ ] **Step 3**: Create workflow configuration entities
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/WorkflowDefinition.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/WorkflowStage.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/WorkflowStageStatus.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/WorkflowTransitionRule.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/ParallelTrackDefinition.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/MirrorViewRule.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Workflow/StageMandatoryField.cs`
  - Create: `backend/src/SimbaFlow.Domain/Enums/StageType.cs`

- [ ] **Step 4**: Create domain events
  - Create: `backend/src/SimbaFlow.Domain/Events/CandidateRegisteredEvent.cs`
  - Create: `backend/src/SimbaFlow.Domain/Events/CandidateStageChangedEvent.cs`
  - Create: `backend/src/SimbaFlow.Domain/Events/CandidateStatusChangedEvent.cs`

### Phase B: Infrastructure Layer

- [ ] **Step 5**: Update ApplicationDbContext — add new DbSets
  - Modify: `ApplicationDbContext.cs` — add Candidate, Document, Workflow DbSets
  - Modify: `IApplicationDbContext.cs` — add interface members

- [ ] **Step 6**: Create EF Core entity configurations
  - Create: `backend/src/SimbaFlow.Infrastructure/Persistence/Configurations/CandidateConfiguration.cs`
  - Create: `backend/src/SimbaFlow.Infrastructure/Persistence/Configurations/WorkflowEventConfiguration.cs`
  - Create: `backend/src/SimbaFlow.Infrastructure/Persistence/Configurations/WorkflowDefinitionConfiguration.cs`

- [ ] **Step 7**: Create workflow engine service
  - Create: `backend/src/SimbaFlow.Infrastructure/Workflow/WorkflowEngineService.cs`
  - Create: `backend/src/SimbaFlow.Infrastructure/Workflow/IWorkflowEngineService.cs`
  - Create: `backend/src/SimbaFlow.Infrastructure/Workflow/ConditionEvaluator.cs`
  - Create: `backend/src/SimbaFlow.Infrastructure/Workflow/WorkflowState.cs`

- [ ] **Step 8**: Create CV generation service
  - Create: `backend/src/SimbaFlow.Infrastructure/Services/CvGenerationService.cs`
  - Create: `backend/src/SimbaFlow.Application/Common/Interfaces/ICvGenerationService.cs`

- [ ] **Step 9**: Create default workflow seeder
  - Create: `backend/src/SimbaFlow.Infrastructure/Persistence/Seeds/WorkflowSeeder.cs`

- [ ] **Step 10**: Register new services in DI
  - Modify: `DependencyInjection.cs` — register IWorkflowEngineService, ICvGenerationService

### Phase C: API Layer — Candidate Module

- [ ] **Step 11**: Create Candidate API module
  - Create: `backend/src/SimbaFlow.API/Features/Candidates/CandidateModule.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Candidates/Commands/RegisterCandidateCommand.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Candidates/Commands/UpdateCandidateCommand.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Candidates/Commands/DeleteCandidateCommand.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Candidates/Commands/UploadDocumentCommand.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Candidates/Commands/GenerateCVCommand.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Candidates/Queries/GetCandidatesQuery.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Candidates/Queries/GetCandidateByIdQuery.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Candidates/Queries/GetCandidateDocumentsQuery.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Candidates/Queries/GetCandidateTimelineQuery.cs`

- [ ] **Step 12**: Create Candidate validators
  - Create: `backend/src/SimbaFlow.API/Features/Candidates/Validators/RegisterCandidateValidator.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Candidates/Validators/UpdateCandidateValidator.cs`

### Phase D: API Layer — Workflow Module

- [ ] **Step 13**: Create Workflow API module
  - Create: `backend/src/SimbaFlow.API/Features/Workflow/WorkflowModule.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Workflow/Commands/ExecuteTransitionCommand.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Workflow/Commands/UpdateStatusCommand.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Workflow/Queries/GetAvailableActionsQuery.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Workflow/Queries/GetViewCandidatesQuery.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Workflow/Queries/GetWorkflowStateQuery.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Workflow/Queries/GetWorkflowEventsQuery.cs`

- [ ] **Step 14**: Create Workflow Configuration API
  - Create: `backend/src/SimbaFlow.API/Features/Workflow/Commands/CreateStageCommand.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Workflow/Commands/UpdateStageCommand.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Workflow/Commands/CreateTransitionRuleCommand.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Workflow/Commands/ConfigureParallelTracksCommand.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Workflow/Queries/GetWorkflowDefinitionQuery.cs`

- [ ] **Step 15**: Create domain event handlers (SignalR broadcast)
  - Create: `backend/src/SimbaFlow.Infrastructure/DomainEvents/CandidateStageChangedHandler.cs`
  - Create: `backend/src/SimbaFlow.Infrastructure/DomainEvents/CandidateStatusChangedHandler.cs`

### Phase E: Frontend

- [ ] **Step 16**: Create candidate pages and components
  - Create: `frontend/app/(main)/candidates/page.tsx` — candidate list
  - Create: `frontend/app/(main)/candidates/[id]/page.tsx` — candidate detail
  - Create: `frontend/app/(main)/candidates/register/page.tsx` — registration form
  - Create: `frontend/components/candidates/candidate-table.tsx`
  - Create: `frontend/components/candidates/candidate-detail.tsx`
  - Create: `frontend/components/candidates/register-candidate-form.tsx`
  - Create: `frontend/components/candidates/document-uploader.tsx`
  - Create: `frontend/components/candidates/document-list.tsx`
  - Create: `frontend/components/candidates/candidate-timeline.tsx`

- [ ] **Step 17**: Create workflow view pages and components
  - Create: `frontend/app/(main)/workflow/[stageId]/page.tsx` — generic workflow view
  - Create: `frontend/components/workflow/action-button-bar.tsx`
  - Create: `frontend/components/workflow/candidate-status-badge.tsx`
  - Create: `frontend/components/workflow/workflow-view-table.tsx`

- [ ] **Step 18**: Create workflow configuration admin page
  - Create: `frontend/app/(main)/(admin)/workflow/page.tsx`
  - Create: `frontend/components/workflow/workflow-config-editor.tsx`
  - Create: `frontend/components/workflow/condition-builder.tsx`
  - Create: `frontend/components/workflow/stage-editor.tsx`

- [ ] **Step 19**: Create TypeScript types and API hooks
  - Create: `frontend/types/candidate.ts`
  - Create: `frontend/types/workflow.ts`
  - Create: `frontend/lib/api/candidates.ts` — SWR hooks
  - Create: `frontend/lib/api/workflow.ts` — SWR hooks

### Phase F: Tests

- [ ] **Step 20**: Backend unit tests
  - Create: `backend/tests/SimbaFlow.API.Tests/Services/WorkflowEngineServiceTests.cs`
  - Create: `backend/tests/SimbaFlow.API.Tests/Services/ConditionEvaluatorTests.cs`

- [ ] **Step 21**: Property-based tests (FsCheck)
  - Create: `backend/tests/SimbaFlow.API.Tests/Properties/WorkflowEngineProperties.cs`
  - Properties: event replay idempotence, snapshot round-trip, transition atomic rejection, condition determinism, stateful model test

### Phase G: Migration & Documentation

- [ ] **Step 22**: Create EF Core migration
  - Generate migration for all new entities

- [ ] **Step 23**: Generate code summary documentation
  - Create: `aidlc-docs/construction/candidate-workflow/code/code-summary.md`

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
