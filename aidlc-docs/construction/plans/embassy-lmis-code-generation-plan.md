# Code Generation Plan — Unit 3: Embassy & LMIS Processing

## Unit Context
- **Unit**: Embassy & LMIS Processing (Unit 3)
- **Workspace Root**: `/Users/mama/Dev/simbaflow`
- **Stories**: US-3.01–US-3.11, US-4.01–US-4.05 (16 stories)
- **Dependencies**: Unit 2 (WorkflowEngine, Candidate, stage boards, SignalR)
- **Design decisions (approved)**:
  - Case Executive = mirror-only stage
  - Dedicated `EmbassyModule` + `LmisModule`
  - No new Docker services

## Permission code alignment

Existing `PermissionSeeder` already has `embassy.read`, `embassy.update`, `lmis.read`, `lmis.update`.  
Unit 3 **keeps those** (nav already uses them) and **adds**:

| Code | Purpose |
|------|---------|
| `embassy.case_view` | Case Executive board |
| `embassy.case_submit` | Submit visa documentation |
| `embassy.visa_outcome` | Issued / Rejected / Resubmit |
| `lmis.document` | LMIS document upload (optional gate; can also use candidate.update) |

Functional-design names `embassy.view` / `lmis.view` map to **`embassy.read` / `lmis.read`** in code.

---

## Code Generation Steps

### Phase A: Domain & Engine Foundation

- [x] **Step 1**: Add `StageEnteredAt` on Candidate + TenantDbContext mapping — DONE (2026-07-21)
- [x] **Step 2**: Extend workflow engine APIs — DONE (2026-07-21)
  - `UpdateStatusAsync` metadata + `saveChanges`; `UpdateStatusChainAsync`
  - RemoveFromSource clears source-rooted mirrors; `StageEnteredAt` on transition
- [x] **Step 3**: Workflow seeder + upgrader — DONE (2026-07-21)
  - Case Executive stage + Embassy→Case Executive mirror in seeder
  - `IWorkflowDefinitionUpgrader` / `WorkflowDefinitionUpgrader`

### Phase B: Permissions & Migration

- [x] **Step 4**: Permission + role seed updates — DONE (2026-07-21)
- [x] **Step 5**: EF Tenant migration — DONE (2026-07-21)
  - `Migrations/Tenant/20260721133610_AddCandidateStageEnteredAt.cs`

### Phase C: Embassy API

- [x] **Step 6**: Embassy commands + validators — DONE (2026-07-21)
- [x] **Step 7**: Embassy queries + module — DONE (2026-07-21)

### Phase D: LMIS API

- [x] **Step 8**: LMIS commands + validators — DONE (2026-07-21)
- [x] **Step 9**: LMIS queries + module — DONE (2026-07-21)

### Phase E: Frontend

- [x] **Step 10**: API clients + types — DONE (2026-07-22)
- [x] **Step 11**: Shared UI pieces — DONE (2026-07-22)
- [x] **Step 12**: Named board pages — DONE (2026-07-22)

### Phase F: Tests

- [x] **Step 13**: Example-based unit/integration tests — DONE (2026-07-22)
- [x] **Step 14**: FsCheck properties (`EmbassyLmisProperties.cs`) — DONE (2026-07-22)

### Phase G: Docs

- [x] **Step 15**: Code summary — DONE (2026-07-22)
  - `aidlc-docs/construction/embassy-lmis/code/code-summary.md`

---

## Recommended execution batches

| Batch | Steps | Rationale |
|-------|-------|-----------|
| 1 | 1–5 | Foundation: column, engine, seeder/upgrader, permissions, migration |
| 2 | 6–9 | Backend Embassy + LMIS APIs |
| 3 | 10–12 | Frontend boards |
| 4 | 13–15 | Tests + summary |

---

## Story Traceability

| Story | Steps |
|-------|-------|
| US-3.01 To Embassy | 2 (transition already seeded), 12 (actions) |
| US-3.02 Book Medical | 6, 7, 11, 12 |
| US-3.03 Medical result + mirror | 2, 3, 6, 7 |
| US-3.04 Book Tasheer | 6, 7, 12 |
| US-3.05 Tasheer result + mirror | 2, 3, 6 |
| US-3.06 Mirror LMIS | 2, 3 (seed already), 9, 12 |
| US-3.07 Set Ready → Case Exec | 3, 6, 7, 12 |
| US-3.08 Submit docs | 6, 7, 12 |
| US-3.09 Visa outcome | 6, 7, 12 |
| US-3.10 To LMIS full transfer | 2 (mirror cleanup), 12 |
| US-3.11 Resubmit | 6, 7, 12 |
| US-4.01 LMIS queue | 9, 12 |
| US-4.02 Insurance Paid | 2 (chain), 8, 9, 12 |
| US-4.03 LMIS documents | 9, 12 |
| US-4.04 Milestone sequence | 8, 9, 12 |
| US-4.05 To Ticket | existing transition + 12 actions |

---

## Out of scope (explicit)

- Ticket / Departure / Arrival UX polish (Unit 4) — To Ticket must work via engine
- Government LMIS HTTP client
- Frontend SignalR for all pages beyond the three Unit 3 boards (nice-to-have if cheap)

---

## Estimated artifacts

| Area | Create | Modify |
|------|--------|--------|
| Domain / Infra | ~5 | ~6 |
| API Embassy/Lmis | ~20 | 0–2 |
| Frontend | ~10 | ~4 (nav, workflow slug map) |
| Tests | ~3 | 0 |
| Docs | 1 | aidlc-state/audit |

**Total**: ~35–45 files touched.
