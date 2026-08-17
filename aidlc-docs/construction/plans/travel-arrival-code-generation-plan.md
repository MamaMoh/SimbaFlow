# Code Generation Plan — Unit 4: Travel, Departure & Arrival

## Unit Context
- **Unit**: Travel, Departure & Arrival (Unit 4)
- **Workspace Root**: `/Users/mama/Dev/simbaflow`
- **Stories**: US-5.01–US-5.08, US-6.01–US-6.07 (15 stories)
- **Dependencies**: Unit 2 engine, Unit 3 (To Ticket)
- **Design decisions (approved)**:
  - TravelModule + ArrivalModule + ExceptionModule
  - Full ExceptionCase / InvestigationNote / LiabilityAssignment
  - Not Departed: reason + Back to Ticket | Cancel departure
  - Notify = status only (NoOp notifier)
  - Add to Commission: RemoveFromSource=false + Commission shell
  - Arrival permanent ledger
  - No new Docker services

## Permission code alignment

Existing `PermissionSeeder` already has travel / arrival / commission codes.  
Unit 4 **keeps those** (roles already reference them) and maps FD names as follows:

| FD / NFR name | Code in use |
|---------------|-------------|
| `travel.ticket` (board) | `travel.read` |
| `travel.ticket` (book) | `travel.update` |
| `travel.departure` | `travel.read` + `travel.update` |
| `travel.arrival` | `arrival.read` + `arrival.update` |
| `travel.exception.view` | `arrival.read` |
| `travel.exception` | `arrival.exception` |
| Commission shell create | `commission.create` |
| Commission board view | `commission.read` |

**Optional additive** (only if role split Ticket vs Departure is needed in Batch 1):

| Code | Purpose |
|------|---------|
| `travel.ticket` | Book ticket only |
| `travel.departure` | Notify / Departed / Not Departed |

Default: **reuse existing codes** unless product asks for split mid-batch.

---

## Code Generation Steps

### Phase A: Domain & Persistence

- [x] **Step 1**: Domain entities + enums — DONE (2026-07-22)
  - `ExceptionCase`, `InvestigationNote`, `LiabilityAssignment`, `Commission`
  - Enums: ExceptionType, ExceptionStatus, LiabilityParty, CommissionStatus, NonDepartureReason, NotDepartedOutcome
- [x] **Step 2**: TenantDbContext mapping + Fluent configs + indexes — DONE (2026-07-22)
- [x] **Step 3**: EF Tenant migration `AddTravelArrivalExceptionCommission` — DONE (2026-07-22)
  - `Migrations/Tenant/20260722054539_AddTravelArrivalExceptionCommission.cs`

### Phase B: Workflow & Permissions

- [x] **Step 4**: Workflow seeder polish + `EnsureUnit4ArtifactsAsync` — DONE (2026-07-22)
  - Transitions ensured; Add to Commission forced `RemoveFromSource=false`
  - Hooked in Program.cs + ProvisionTenantCommand
- [x] **Step 5**: Permission / role seed touch-up — DONE (2026-07-22)
  - No new codes; reuse existing `travel.*` / `arrival.*` / `commission.*`
- [x] **Step 6**: `ICandidateNotifier` + `NoOpCandidateNotifier` DI — DONE (2026-07-22)

### Phase C: Travel API

- [x] **Step 7**: Travel commands + validators — DONE (2026-07-22)
  - BookTicket, MarkNotified, ConfirmDeparted, RecordNotDeparted
- [x] **Step 8**: Travel queries + TravelModule — DONE (2026-07-22)
  - GetTicketBoard, GetDepartureBoard (exclude canceled by default; RemainingDays)

### Phase D: Arrival & Exception API

- [x] **Step 9**: Arrival commands + validators — DONE (2026-07-22)
  - ConfirmArrived, FlagException, AddToCommission (+ shell upsert)
- [x] **Step 10**: Arrival queries + ArrivalModule — DONE (2026-07-22)
  - GetArrivalBoard (pagination; commission_linked)
- [x] **Step 11**: Exception commands/queries + ExceptionModule — DONE (2026-07-22)
  - List/detail, notes, liability, status, close

### Phase E: Frontend

- [x] **Step 12**: API clients + types — DONE (2026-07-22)
  - `travel.ts`, `arrival.ts`, `exceptions.ts`
- [x] **Step 13**: Shared UI — DONE (2026-07-22)
  - Travel/Arrival row actions, RemainingDaysBadge, ExceptionStatusBadge
- [x] **Step 14**: Named pages + nav — DONE (2026-07-22)
  - `/workflow/tickets`, `/departures`, `/arrivals`, `/exceptions`, `/exceptions/[id]`, commissions stub

### Phase F: Tests

- [x] **Step 15**: Example-based tests — DONE (2026-07-22)
  - `TravelArrivalServiceTests.cs`
- [x] **Step 16**: FsCheck `TravelArrivalProperties.cs` — DONE (2026-07-22)

### Phase G: Docs

- [x] **Step 17**: Code summary — DONE (2026-07-22)
  - `aidlc-docs/construction/travel-arrival/code/code-summary.md`

---

## Recommended execution batches

| Batch | Steps | Rationale |
|-------|-------|-----------|
| 1 | 1–6 | Domain, migration, upgrader, notifier, permissions |
| 2 | 7–11 | Backend Travel + Arrival + Exception APIs |
| 3 | 12–14 | Frontend boards + exception workspace |
| 4 | 15–17 | Tests + summary |

---

## Story Traceability

| Story | Steps |
|-------|-------|
| US-5.01 Book Ticket | 7, 8, 13, 14 |
| US-5.02 To Departure | 4 (seed), 14 actions |
| US-5.03 Departure countdown | 8, 13, 14 |
| US-5.04 Remaining Days | 8, 13 |
| US-5.05 Mark Notified | 6, 7, 14 |
| US-5.06 Confirm Departed → Arrival | 7, 9, 14 |
| US-5.07 Not Departed + reason | 7, 13, 14 |
| US-5.08 Back to Ticket / Cancel | 4, 7, 8, 14 |
| US-6.01 Arrival ledger | 10, 14 |
| US-6.02 Confirm Arrived | 9, 14 |
| US-6.03 Flag Returned/Runaway | 1, 9, 11, 14 |
| US-6.04 Exception workspace | 11, 13, 14 |
| US-6.05 Investigation notes / liability | 1, 11, 14 |
| US-6.06 Add to Commission + shell | 1, 4, 9, 14 |
| US-6.07 Permanent Arrival | 4, 9, 10, 16 |

---

## Out of scope (explicit)

- Bot Telegram/WhatsApp send (Unit 7) — NoOp only
- Full commission fees / payments / disputes UI (Unit 5)
- Soft-copy duplicate rows
- Self-service uncancel
- Partner agency / tenant licensing (Unit 6)

---

## Estimated artifacts

| Area | Create | Modify |
|------|--------|--------|
| Domain / Infra / Migration | ~12 | ~4 |
| API Travel/Arrival/Exception | ~25 | 0–2 |
| Frontend | ~15 | ~4 (nav) |
| Tests | ~4 | 0 |
| Docs | 1 | aidlc-state/audit |

**Total**: ~50–60 files touched.
