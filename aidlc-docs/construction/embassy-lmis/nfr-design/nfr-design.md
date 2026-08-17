# NFR Design — Unit 3: Embassy & LMIS Processing

Builds on Unit 2 NFR Design (event store, denormalization, condition evaluator, SignalR). This document specifies how intent modules, Case Executive mirrors, and LMIS side-effects meet Unit 3 NFRs.

---

## 1. Intent Module Architecture

```
HTTP  POST /api/embassy/candidates/{id}/medical/book
        │
        ▼
EmbassyModule (Carter) → BookMedicalCommand
        │
        ▼
FluentValidation → BookMedicalValidator
        │
        ▼
BookMedicalHandler
  1. Load candidate (TenantDbContext)
  2. Assert Embassy visibility (CurrentStage or VisibleInStages)
  3. Assert medical track precondition (Pending / re-book rules)
  4. IWorkflowEngineService.UpdateStatusAsync(track, to, data)
  5. Engine: append event + denormalize + re-eval mirrors (atomic)
  6. Return Result; SignalR after commit (existing handlers)
```

### Endpoint map

| Method | Path | Command / Query |
|--------|------|-----------------|
| GET | `/api/embassy/board` | `GetEmbassyBoardQuery` |
| POST | `/api/embassy/candidates/{id}/medical/book` | `BookMedicalCommand` |
| POST | `/api/embassy/candidates/{id}/medical/result` | `RecordMedicalResultCommand` |
| POST | `/api/embassy/candidates/{id}/tasheer/book` | `BookTasheerCommand` |
| POST | `/api/embassy/candidates/{id}/tasheer/result` | `RecordTasheerResultCommand` |
| POST | `/api/embassy/candidates/{id}/visa/ready` | `SetVisaReadyCommand` |
| POST | `/api/embassy/candidates/{id}/visa/submit` | `SubmitVisaDocumentationCommand` |
| POST | `/api/embassy/candidates/{id}/visa/outcome` | `RecordVisaOutcomeCommand` |
| POST | `/api/embassy/candidates/{id}/visa/resubmit` | `ResubmitVisaCommand` |
| GET | `/api/embassy/case-executive/board` | `GetCaseExecutiveBoardQuery` |
| GET | `/api/lmis/board` | `GetLmisBoardQuery` |
| POST | `/api/lmis/candidates/{id}/insurance/paid` | `RecordInsurancePaidCommand` |
| POST | `/api/lmis/candidates/{id}/milestone` | `AdvanceLmisMilestoneCommand` |

Stage transitions (`To Embassy`, `To LMIS`, `To Ticket`) stay on existing WorkflowModule `ExecuteTransition`.

### Performance budget (PERF-33/34)

- Handler work must complete inside the engine’s single transaction target (&lt; 500ms p95)
- Mirror evaluation runs **inside** `UpdateStatusAsync` before commit (PERF-34 &lt; 1s / same request)

---

## 2. Case Executive Mirror Design

### Seed additions (WorkflowSeeder / backfill)

```
Stages: ... Embassy, Case Executive (new), LMIS, ...
MirrorViewRule on Embassy:
  Target = Case Executive
  Conditions: OR(
    { field: visa, op: eq, value: Ready },
    { field: visa, op: eq, value: Submitted }
  )
```

Existing LMIS mirror (Fit ∧ Book Done) unchanged.

### Board query (PERF-31)

```csharp
// Case Executive board
query.Where(c => !c.IsDeleted
    && c.VisibleInStages.Contains(caseExecutiveStageId)
    && (officeFilter == null || c.OfficeId == officeFilter));
```

### Full transfer cleanup (TEST-36 / BR-E06)

On `To LMIS` with `RemoveFromSource=true`, engine must:

1. Set `CurrentStageId = LMIS`
2. Remove Embassy from visibility
3. Remove Case Executive from `VisibleInStages`
4. Ensure LMIS present as primary stage

If current engine only clears source stage, Unit 3 adds an explicit post-transition hook or expands `RemoveFromSource` semantics to clear **all mirrors of the source stage**.

---

## 3. Side-Effect Patterns

### 3a. Insurance Paid → Available (RES-31)

```csharp
await using var tx = await _db.Database.BeginTransactionAsync(ct);
await _engine.UpdateStatusAsync(..., track: "insurance", to: "Insurance Paid", data);
await _engine.UpdateStatusAsync(..., track: "insurance", to: "Available", data: null);
await tx.CommitAsync(ct);
// Prefer: engine method UpdateStatusWithFollowUpsAsync OR single handler wrapping both
// if engine opens its own transaction — nest via ambient transaction / ExecutionStrategy
```

**Design choice**: Handler calls a new engine helper `UpdateStatusChainAsync(IReadOnlyList<StatusChange>)` that appends N events and denormalizes once at the end inside one transaction.

### 3b. Milestone sequence (TEST-33)

```csharp
static readonly Dictionary<string?, string> Next = new()
{
    [null] = "Uploaded",
    [""] = "Uploaded",
    ["Uploaded"] = "Check Verified",
    ["Check Verified"] = "Issued"
};
// Reject if requested != Next[current] OR insurance != Available
```

Enforced in `AdvanceLmisMilestoneHandler` before calling engine (SEC-35).

### 3c. Rejection reason (TEST-34)

FluentValidation: `When(x => x.Outcome == Rejected, () => RuleFor(x => x.RejectionReason).NotEmpty())`.

---

## 4. Board Query & Indexing

Reuses Unit 2 indexes:

- `ix_candidates_stage` on `current_stage_id`
- GIN on `visible_in_stages`

### Embassy board projection (PERF-30)

Select denormalized JSON keys only (no event replay):

`medical`, `tasheer`, `visa`, `medical_appointment_date`, `medical_facility`, `tasheer_appointment_date`, `visa_rejection_reason`, `visa_resubmission_count`

### Days-in-stage (USAB-35)

```csharp
// Prefer denormalized Candidate.StageEnteredAt if added;
// else: max Timestamp of last StageTransitioned into current stage (one subquery / join)
```

**Design choice for Unit 3**: Add optional `StageEnteredAt` (timestamptz) on Candidate, set on every StageTransitioned — avoids N+1 timeline queries on boards. Small migration via tenant migrator.

---

## 5. Authorization Design (SEC-30–36)

| Endpoint group | Permission |
|----------------|------------|
| Embassy board + medical/tasheer/ready | `embassy.view` + `embassy.update` |
| Case Executive board | `embassy.case_view` |
| Visa submit | `embassy.case_submit` |
| Visa outcome / resubmit | `embassy.visa_outcome` |
| LMIS board | `lmis.view` |
| Insurance / milestone | `lmis.update` |
| LMIS document upload | `lmis.document` (+ existing candidate document APIs) |
| To Embassy / To LMIS / To Ticket | `workflow.execute` |

Office scope: apply `OfficeId` filter unless user has `candidate.read_all_offices` (or equivalent existing cross-office permission).

Register permission codes in system permission seed; map default tenant roles in Unit 3 seeder.

---

## 6. Existing-Tenant Backfill

New installs: extend `WorkflowSeeder` before first definition save.

Existing tenants (definition already exists):

```
IWorkflowDefinitionUpgrader.EnsureUnit3ArtifactsAsync(tenant)
  - Upsert Case Executive stage if missing
  - Upsert Case Executive mirror rule
  - Ensure embassy.* / lmis.* permissions exist
  - Idempotent; safe on every app start (Development) or one-shot migration job
```

Called from `ITenantSchemaMigrator` after EF migrate (same path as Unit 2).

---

## 7. SignalR / Frontend Cache (USAB-36)

After status/stage events, invalidate:

```
mutate('/api/embassy/board')
mutate('/api/embassy/case-executive/board')
mutate('/api/lmis/board')
mutate(key => key includes candidateId)
mutate(key => key includes '/workflow/')
```

Wire client subscribe if still deferred from Unit 2 follow-ups — **in scope for Unit 3** for the three named boards (minimum).

---

## 8. PBT Architecture (Unit 3)

### Generators

```csharp
GenTrackStatus() // medical/tasheer/visa/insurance/milestone legal values
GenEmbassyCommand() // BookMedical | RecordResult | SetReady | Submit | Outcome | Resubmit
GenLmisCommand() // PayInsurance | AdvanceMilestone | ToTicket (when legal)
```

### Properties (map to TEST-30–38)

| Property | Assert |
|----------|--------|
| TrackIndependence | After medical cmd, tasheer unchanged |
| LmisMirrorSymmetry | VisibleInStages has LMIS ↔ Fit∧BookDone (while in Embassy) |
| CaseExecMirrorSymmetry | Case Exec visible ↔ visa in {Ready,Submitted} |
| MilestoneNoSkip | Illegal advance → state identical |
| RejectionReasonRequired | Outcome=Rejected without reason → fail |
| ResubmitPreservesHistory | Events contain prior rejection payload |
| ToLmisClearsMirrors | After To LMIS: no Embassy/Case Exec visibility |
| InsurancePaidImpliesAvailable | Final insurance == Available |
| StatefulEmbassyLmis | Model ↔ system after random legal command seq |

Example-based tests cover happy paths per US-3.xx / US-4.xx.

---

## 9. Error & Observability

- Intent handlers return ProblemDetails via existing Result pipeline
- Log: `CandidateId`, `Track`, `From`, `To`, `UserId` — **not** passport; redact rejection free-text at Information level (SEC-33)
- Metrics (optional log-based): count of mirror activations, milestone advances, resubmits

---

## 10. Out of scope in NFR Design

- Government LMIS HTTP client / Polly pipeline (Decision 6) — stub interface only if needed later
- Ticket board UX (Unit 4) — transition API must succeed; page may be generic workflow view
