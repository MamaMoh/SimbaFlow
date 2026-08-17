# Unit 2 Code Generation — Progress Reassessment

**Date**: 2026-07-21  
**Trigger**: Session continuity option C  
**Plan**: `aidlc-docs/construction/plans/candidate-workflow-code-generation-plan.md`

## Summary (updated 2026-07-21 after engine batch)

| Status | Steps | Count |
|--------|-------|-------|
| Done | 1–5, 7, 9–10, 13–14 | 10 |
| Partial | 6, 11, 16, 17, 19 | 5 |
| Not started | 8, 12, 15, 18, 20–23 | 8 |

**Overall**: ~55% complete. Workflow engine + APIs are real; frontend stage boards, CV, tests, and tenant EF migration remain.

## Engine batch delivered

- `WorkflowEngineService` — event replay, transitions, status updates, mirror views, available actions, snapshots every 20 events
- `WorkflowSeeder` — 8-stage default template; called from tenant provisioning
- Provision DDL expanded for all workflow tables
- Workflow runtime + config API handlers no longer stubs
- Register candidate appends `Registered` workflow event
- Build: **succeeded** (0 errors)

## Architecture drift (plan vs current code)

The plan assumed a single `ApplicationDbContext`. The codebase now uses:
- `PlatformDbContext` — identity, tenants, staff
- `TenantDbContext` / `ITenantDbContext` — candidates, workflow, tenant roles

Reassessment treats TenantDbContext work as fulfilling Steps 5–6 intent. Separate Fluent `*Configuration.cs` files were **not** created; configuration is inline in `TenantDbContext.OnModelCreating`.

**Gap**: `ITenantDbContext` is missing DbSets that exist on `TenantDbContext` (`WorkflowStageStatuses`, `WorkflowTransitionRules`, `ParallelTrackDefinitions`, `MirrorViewRules`, `StageMandatoryFields`). Handlers cannot query those via the interface until it is extended.

## Step-by-step findings

### Phase A — Domain (DONE)
| Step | Status | Evidence |
|------|--------|----------|
| 1 Candidate entities | Done | `Candidate.cs`, `CandidateDocument.cs`, enums |
| 2 Event sourcing entities | Done | `WorkflowEvent`, `WorkflowSnapshot`, `WorkflowEventType` |
| 3 Config entities | Done | Definition, Stage, Status, TransitionRule, ParallelTrack, MirrorView, MandatoryField, `StageType` |
| 4 Domain events | Done | Registered / StageChanged / StatusChanged |

### Phase B — Infrastructure (MOSTLY OPEN)
| Step | Status | Evidence |
|------|--------|----------|
| 5 DbSets | Partial | On `TenantDbContext` + legacy `ApplicationDbContext`; interface incomplete |
| 6 EF configurations | Partial | Inline in TenantDbContext; no `Configurations/*Candidate*` files |
| 7 Workflow engine | Partial | `IWorkflowEngineService`, `WorkflowState`, `ConditionEvaluator` exist; **`WorkflowEngineService` implementation missing** |
| 8 CV generation | Not started | Handler returns 501 |
| 9 Workflow seeder | Not started | No `WorkflowSeeder.cs` |
| 10 DI registration | Not started | No `IWorkflowEngineService` / CV registration in DI |

### Phase C — Candidate API (PARTIAL)
| Step | Status | Evidence |
|------|--------|----------|
| 11 Candidate module | Partial | Routes wired; Register/Update/Delete/List/GetById/Documents/Timeline **implemented**; GenerateCV **stub** |
| 12 Validators | Not started | No FluentValidation validators |

### Phase D — Workflow API (STUBS)
| Step | Status | Evidence |
|------|--------|----------|
| 13 Workflow runtime API | Partial | Module + commands/queries exist; all handlers TODO / empty success |
| 14 Workflow config API | Partial | CreateStage / UpdateStage / CreateTransitionRule / ConfigureParallelTracks are stubs |
| 15 SignalR domain handlers | Not started | No CandidateStage/Status Changed handlers |

### Phase E — Frontend (PARTIAL)
| Step | Status | Evidence |
|------|--------|----------|
| 16 Candidate UI | Partial | List + register sheet/page; missing `[id]` detail, document uploader/list, timeline |
| 17 Stage boards | Partial | `/workflow/[stageId]` placeholder only |
| 18 Admin workflow config | Not started | No admin page / editors |
| 19 Types & hooks | Partial | `types/candidate.ts`, `types/workflow.ts` exist; no `lib/api/candidates.ts` / `workflow.ts` |

### Phase F–G — Tests / Migration / Docs
| Step | Status | Evidence |
|------|--------|----------|
| 20 Unit tests | Not started | No workflow test files |
| 21 PBT (FsCheck) | Not started | — |
| 22 EF migration | Not started | Only Platform `InitialPlatform` migration exists; no tenant workflow migration |
| 23 Code summary | Not started | No `candidate-workflow/code/code-summary.md` |

## Recommended resume order

1. **Close interface gaps** — extend `ITenantDbContext` with missing workflow DbSets  
2. **Step 7** — implement `WorkflowEngineService` (core product value)  
3. **Steps 9–10** — seeder + DI  
4. **Steps 13–14** — wire real workflow handlers  
5. **Step 8 + CV handler** — QuestPDF  
6. **Steps 16–17, 19** — detail page + stage boards + API hooks  
7. **Steps 12, 15, 18, 20–23** — validators, SignalR, admin UI, tests, migration, docs

## Blocking for agency MVP

Until Step 7 + 13 ship, stage transitions, mirror views, and action buttons cannot work end-to-end (matches `docs/AGENCY_SAAS_DESIGN_GUIDE.md` “designed, not shipped”).
