# NFR Requirements — Unit 4: Travel, Departure & Arrival

Inherits all Unit 1–3 NFR baselines. Adds targets for Ticket / Departure countdown / Arrival ledger, Not Departed fork, exception workspace, and Commission shell.

## NFR-PERF: Performance Requirements

| ID | Requirement | Target | Context |
|----|-------------|--------|---------|
| PERF-40 | Ticket board query (paginated) | < 300ms p95 | Stage filter + office scope |
| PERF-41 | Departure board + Remaining Days | < 300ms p95 | Exclude canceled; sort by flight_date |
| PERF-42 | Arrival board (permanent ledger) | < 400ms p95 | Includes Commission-visible rows; may grow large |
| PERF-43 | Exception case list query | < 300ms p95 | Filter by status/type/office |
| PERF-44 | Exception case detail (+ notes/liabilities) | < 400ms p95 | Single case graph |
| PERF-45 | Intent update (Book Ticket, Notify, Departed, …) | < 500ms p95 | Validate + UpdateStatus ± transition + SignalR |
| PERF-46 | Not Departed + outcome (incl. Back to Ticket) | < 600ms p95 | Status + transition in one transaction |
| PERF-47 | Add to Commission (transition + shell upsert) | < 600ms p95 | Engine + Commission insert |
| PERF-48 | Remaining Days compute | O(1) per row | `flight_date − today` in query projection, not N+1 |

## NFR-SCALE: Scalability Requirements

| ID | Requirement | Target | Strategy |
|----|-------------|--------|----------|
| SCALE-40 | Concurrent Departure board users | 30+ per tenant | Indexed stage + canceled filter |
| SCALE-41 | Active (non-canceled) Departure rows | 2,000+ | Exclude canceled in query; index status JSON keys if needed |
| SCALE-42 | Arrival ledger rows (never leave) | 50,000+ over time | Pagination mandatory; archive/filter by year later |
| SCALE-43 | Open exception cases | 500+ concurrent | Indexed ExceptionCase.Status |
| SCALE-44 | Investigation notes per case | 200+ | Append-only; page notes if needed |
| SCALE-45 | Commission shells | 1:1 with Add-to-Commission | Unique CandidateId for Open shell |

## NFR-SEC: Security Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| SEC-40 | Ticket vs Departure vs Arrival separation | `travel.ticket` / `travel.departure` / `travel.arrival` |
| SEC-41 | Exception workspace least privilege | `travel.exception` write; `travel.exception.view` read |
| SEC-42 | Not Departed reason audited | Persist reason enum (+ Other text) on WorkflowEvent; never drop |
| SEC-43 | Cancel departure irreversible without admin | No self-service “uncancel” in Unit 4 |
| SEC-44 | Commission shell create only via Add to Commission | No direct Commission CRUD from UI in Unit 4 |
| SEC-45 | Liability amounts tenant-scoped | ExceptionCase owned by tenant schema |
| SEC-46 | Office-scoped boards by default | Same pattern as Unit 3 |
| SEC-47 | Intent APIs re-validate preconditions server-side | Never trust client outcome/reason |

## NFR-RES: Resiliency Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| RES-40 | Departed + To Arrival atomic | Status + transition one transaction |
| RES-41 | Not Departed + Back to Ticket atomic | Status + transition one transaction |
| RES-42 | Cancel departure: status-only, no partial cancel flag | Single UpdateStatus with canceled=true |
| RES-43 | Add to Commission: transition + shell atomic | Rollback both on failure; idempotent shell upsert |
| RES-44 | Flag Returned/Runaway + ExceptionCase atomic | Status + case create one transaction |
| RES-45 | SignalR failure after commit does not roll back | Fire-and-forget |
| RES-46 | Bot notify stub never fails Mark Notified | No external I/O in Unit 4 notify path |
| RES-47 | Seed Ticket/Departure/Arrival/Commission transitions idempotent | Existing WorkflowSeeder pattern |

## NFR-TEST: PBT Requirements (Unit-Specific)

| ID | Requirement | PBT Rule | Implementation |
|----|-------------|----------|----------------|
| TEST-40 | Booking Complete requires destination + flight_date | PBT-04 | Invalid payloads always fail |
| TEST-41 | To Departure only when Booking Complete | PBT-03 | Illegal transition rejected |
| TEST-42 | Countdown excludes canceled=true | PBT-03 | Query invariant after CancelDeparture |
| TEST-43 | Not Departed requires reason + outcome | PBT-04 | Missing either fails validation |
| TEST-44 | Back to Ticket clears Departure primary | PBT-03 | CurrentStage=Ticket after outcome |
| TEST-45 | Cancel stays on Departure + blocked To Arrival | PBT-03 | No To Arrival when canceled |
| TEST-46 | Departed implies Arrival primary | PBT-03 | After ConfirmDeparted |
| TEST-47 | Notify does not call bot | PBT-03 | No external side-effect; status only |
| TEST-48 | Arrival permanence after Add to Commission | PBT-03 | Still VisibleInStages/CurrentStage Arrival |
| TEST-49 | Commission shell idempotent | PBT-03 | Second Add does not duplicate Open shell |
| TEST-50 | Exception flag creates Open case | PBT-03 | Returned/Runaway → one Open case |
| TEST-51 | One Open ExceptionCase per candidate | PBT-03 | Second flag rejected or no-op |
| TEST-52 | Add to Commission blocked if Open exception | PBT-03 | Command fails |
| TEST-53 | Stateful travel/arrival command sequences | PBT-06 | Random Book/Notify/Depart/NotDepart/Arrive/Flag/Commission |

## NFR-USAB: Usability Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| USAB-40 | Remaining Days visible + urgency coloring | Badge: overdue / ≤3 / ok |
| USAB-41 | Not Departed is two-step (reason then outcome) | `NotDepartedSheet` |
| USAB-42 | Canceled hidden from countdown by default | Optional “Show canceled” |
| USAB-43 | Arrival shows Commission-linked indicator | Badge or column |
| USAB-44 | Exception link from Returned/Runaway rows | Navigate to case detail |
| USAB-45 | Disabled actions explain why | `disabledReason` tooltips |
| USAB-46 | Success/error toasts on every intent action | sonner + PageAlert |
| USAB-47 | Days-in-stage / StageEnteredAt where available | Reuse Unit 3 pattern |

## Tech Stack Additions (Unit 4-Specific)

| Package | Purpose |
|---------|---------|
| (None required) | Reuses MediatR, FluentValidation, SignalR, SWR, EF Core |
| EF migrations (Tenant) | New tables: ExceptionCase, InvestigationNote, LiabilityAssignment, Commission |

## Testable Properties Summary

Must have FsCheck (and complementary example tests) for:

1. Ticket completeness gates To Departure
2. Canceled exclusion from countdown
3. Not Departed reason+outcome fork (Back to Ticket vs Cancel)
4. Departed → Arrival transition
5. Notify = status-only (no bot)
6. Arrival permanence + Commission shell idempotency
7. Exception case open invariant + Commission block while open
8. Stateful random travel/arrival command model
