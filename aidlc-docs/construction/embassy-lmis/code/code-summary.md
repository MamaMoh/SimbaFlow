# Code Summary — Unit 3: Embassy & LMIS Processing

**Completed**: 2026-07-22  
**Stories**: US-3.01–US-3.11, US-4.01–US-4.05  
**Status**: Code Generation complete (Batches 1–4)

---

## Architecture delivered

- No new aggregates — Embassy/LMIS state via `WorkflowEvent` + `CurrentStatusValues` + `VisibleInStages`
- **Case Executive** = mirror-only stage (never primary); activates when `visa` ∈ {Ready, Submitted}
- **EmbassyModule** / **LmisModule** — intent commands wrapping `IWorkflowEngineService`
- Engine extensions: status metadata, `UpdateStatusChainAsync`, RemoveFromSource clears source-rooted mirrors, `StageEnteredAt`

---

## Backend

| Area | Files |
|------|--------|
| Engine | `WorkflowEngineService` (mirror cleanup, status chain, StageEnteredAt) |
| Seed / upgrade | `WorkflowSeeder`, `WorkflowDefinitionUpgrader` (Case Executive + mirrors) |
| Permissions | `embassy.case_view`, `embassy.case_submit`, `embassy.visa_outcome`, `lmis.document` (+ existing read/update) |
| Migration | `Migrations/Tenant/20260721133610_AddCandidateStageEnteredAt.cs` |
| Embassy API | `Features/Embassy/*` — board, case-executive board, medical/tasheer/visa intents |
| LMIS API | `Features/Lmis/*` — board, insurance Paid→Available, milestone sequence |
| Shared | `EmbassyLmisHelpers.cs` |

### Key behaviors

| Behavior | Implementation |
|----------|----------------|
| Medical ∥ Tasheer | Independent tracks; Fit ∧ Book Done → LMIS mirror |
| Visa Ready | Case Executive mirror |
| Visa Rejected | Reason required (validator + handler) |
| Resubmit | Rejected → Ready; prior rejection kept in events |
| To LMIS | `RemoveFromSource` clears Embassy + Case Exec (+ LMIS preview mirrors of Embassy) |
| Insurance Paid | Two-event chain → Available |
| Milestone | Uploaded → Check Verified → Issued only |

---

## Frontend

| Route | Permission |
|-------|------------|
| `/workflow/embassy` | `embassy.read` |
| `/workflow/case-executive` | `embassy.case_view` |
| `/workflow/lmis` | `lmis.read` |

| Area | Files |
|------|--------|
| API | `lib/api/embassy.ts`, `lib/api/lmis.ts` (+ SignalR board revalidate) |
| UI | `status-update-sheet.tsx`, `embassy-row-actions.tsx`, `lmis-row-actions.tsx` |
| Nav | Case Executive between Embassy and LMIS |

---

## Tests

| File | Coverage |
|------|----------|
| `EmbassyLmisServiceTests.cs` | TEST-30–37 example-based |
| `EmbassyLmisProperties.cs` | FsCheck TEST-30–38 |

**Last run**: EmbassyLmis filter **16/16**; full suite **58/58** passed (2026-07-22).

---

## Deferred / follow-ups

| Item | Notes |
|------|--------|
| Ticket / Departure / Arrival boards | Unit 4 (To Ticket transition works) |
| Partner catalog + tenant links | Unit 6 — see `partner-agency-and-tenant-licensing.md` |
| Tenant license level on provision | Unit 1/6 enrichment |
| Government LMIS HTTP client | Future; stub not required |

---

## Story checklist

- [x] US-3.01–3.11 Embassy / Case Executive / visa
- [x] US-4.01–4.05 LMIS queue, insurance, docs, milestone, To Ticket
