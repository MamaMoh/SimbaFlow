# Code Summary — Unit 4: Travel, Departure & Arrival

**Completed**: 2026-07-22  
**Stories**: US-5.01–US-5.08, US-6.01–US-6.07  
**Status**: Code Generation complete (Batches 1–4)

---

## Architecture delivered

- **TravelModule** / **ArrivalModule** / **ExceptionModule** — intent APIs wrapping `IWorkflowEngineService`
- New tenant aggregates: `ExceptionCase`, `InvestigationNote`, `LiabilityAssignment`, `Commission` (shell)
- Ticket / Departure / Arrival tracks remain JSONB on Candidate + WorkflowEvent
- Not Departed: reason + **Back to Ticket** | **Cancel** (`canceled=true`, excluded from countdown)
- Notify: status-only via `NoOpCandidateNotifier` (bot = Unit 7)
- Add to Commission: `RemoveFromSource=false` — Arrival stays in `VisibleInStages`; Commission shell upserted

---

## Backend

| Area | Files |
|------|--------|
| Domain | `Entities/Travel/*`, `Entities/Finance/Commission.cs`, enums |
| Migration | `Migrations/Tenant/20260722054539_AddTravelArrivalExceptionCommission.cs` |
| Upgrade | `EnsureUnit4ArtifactsAsync` (transitions + RemoveFromSource=false) |
| Notifier | `ICandidateNotifier` / `NoOpCandidateNotifier` |
| Travel API | `Features/Travel/*` — ticket/departure boards, BookTicket, Notify, Departed, NotDeparted |
| Arrival API | `Features/Arrival/*` — board, Arrived, FlagException, AddToCommission |
| Exception API | `Features/Exceptions/*` — list/detail, notes, liability, close |
| Engine | Preserve visibility on RemoveFromSource=false; merge VisibleInStages on reload |

### Key behaviors

| Behavior | Implementation |
|----------|----------------|
| Book Ticket | destination + flight_date → Booking Complete |
| Departure countdown | Sort by flight_date; exclude `canceled=true` |
| Confirm Departed | Notified gate → Departed + To Arrival (atomic) |
| Not Departed | Reason + BackToTicket \| CancelDeparture |
| Arrival permanence | VisibleInStages keeps Arrival after Add to Commission |
| Commission shell | One row per candidate; idempotent |
| Flag exception | Returned/Runaway + Open ExceptionCase |
| Open exception | Blocks Add to Commission |

---

## Frontend

| Route | Permission |
|-------|------------|
| `/workflow/tickets` | `travel.read` |
| `/workflow/departures` | `travel.read` |
| `/workflow/arrivals` | `arrival.read` |
| `/workflow/exceptions` | `arrival.read` / `arrival.exception` |
| `/workflow/commissions` | `commission.read` (stub → Unit 5) |

| Area | Files |
|------|--------|
| API | `lib/api/travel.ts`, `arrival.ts`, `exceptions.ts` |
| UI | `travel-row-actions.tsx`, `arrival-row-actions.tsx`, `remaining-days-badge.tsx` |
| Nav | Exceptions between Arrivals and Commissions |

---

## Tests

| File | Coverage |
|------|----------|
| `TravelArrivalServiceTests.cs` | TEST-40–52 example-based |
| `TravelArrivalProperties.cs` | FsCheck TEST-40–53 |

**Last run**: full suite **80/80** passed (2026-07-22).

---

## Deferred / follow-ups

| Item | Notes |
|------|--------|
| Bot Telegram/WhatsApp | Unit 7 — NoOp only |
| Commission fees/payments UI | Unit 5 |
| Partner catalog + tenant licensing | Unit 6 |
| Self-service uncancel | Out of scope |

---

## Permission map (existing codes)

`travel.read` / `travel.update` · `arrival.read` / `arrival.update` / `arrival.exception` · `commission.create` / `commission.read`
