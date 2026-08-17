# NFR Design — Unit 4: Travel, Departure & Arrival

Builds on Unit 2–3 NFR Design (workflow engine, denormalization, SignalR, named boards). Specifies how Travel / Arrival / Exception modules, Not Departed fork, Arrival permanence, and Commission shell meet Unit 4 NFRs.

---

## 1. Intent Module Architecture

```
HTTP  POST /api/travel/candidates/{id}/not-departed
        │
        ▼
TravelModule (Carter) → RecordNotDepartedCommand
        │
        ▼
FluentValidation → reason + outcome required
        │
        ▼
RecordNotDepartedHandler
  1. Load candidate; assert Departure visibility; not canceled
  2. Begin transaction
  3. UpdateStatus(departure_status=Not Departed, reason, outcome, canceled?)
  4. IF BackToTicket → ExecuteTransition("Back to Ticket"); reset ticket_status
     IF CancelDeparture → canceled=true only (no transition)
  5. Commit; SignalR after commit
```

### Endpoint map

| Method | Path | Command / Query |
|--------|------|-----------------|
| GET | `/api/travel/ticket/board` | `GetTicketBoardQuery` |
| POST | `/api/travel/candidates/{id}/ticket/book` | `BookTicketCommand` |
| GET | `/api/travel/departure/board` | `GetDepartureBoardQuery` |
| POST | `/api/travel/candidates/{id}/notify` | `MarkNotifiedCommand` |
| POST | `/api/travel/candidates/{id}/departed` | `ConfirmDepartedCommand` |
| POST | `/api/travel/candidates/{id}/not-departed` | `RecordNotDepartedCommand` |
| GET | `/api/arrival/board` | `GetArrivalBoardQuery` |
| POST | `/api/arrival/candidates/{id}/arrived` | `ConfirmArrivedCommand` |
| POST | `/api/arrival/candidates/{id}/flag-exception` | `FlagExceptionCommand` |
| POST | `/api/arrival/candidates/{id}/add-to-commission` | `AddToCommissionCommand` |
| GET | `/api/exceptions` | `GetExceptionCasesQuery` |
| GET | `/api/exceptions/{id}` | `GetExceptionCaseByIdQuery` |
| POST | `/api/exceptions/{id}/notes` | `AddInvestigationNoteCommand` |
| PATCH | `/api/exceptions/{id}/status` | `UpdateExceptionStatusCommand` |
| POST | `/api/exceptions/{id}/liabilities` | `AssignLiabilityCommand` |
| POST | `/api/exceptions/{id}/close` | `CloseExceptionCommand` |

Stage transitions (`To Departure`, `To Arrival`, `Back to Ticket`, `Add to Commission`) may be invoked from handlers **or** WorkflowModule; Unit 4 intents prefer wrapping them so side-effects stay atomic.

### Performance budget (PERF-45–47)

- Simple intents &lt; 500ms p95 inside one DB transaction
- Not Departed + transition and Add to Commission + shell &lt; 600ms p95
- SignalR after commit (RES-45)

---

## 2. Departure Countdown Design (PERF-41, TEST-42)

### Board query

```csharp
query.Where(c => !c.IsDeleted
    && (c.CurrentStageId == departureId || c.VisibleInStages.Contains(departureId))
    && (!includeCanceled && GetJsonBool(c.CurrentStatusValues, "canceled") != true)
    && (officeFilter == null || c.OfficeId == officeFilter))
  .OrderBy(c => GetJsonDate(c.CurrentStatusValues, "flight_date")); // ascending urgency
```

### Remaining Days (PERF-48)

```csharp
RemainingDays = flightDate.Date - DateOnly.FromDateTime(DateTime.UtcNow);
// Project in DTO; do not store RemainingDays column
```

### Optional `includeCanceled=true`

History view for ops; default false (USAB-42).

### Indexing

Reuse stage + VisibleInStages indexes. If JSON `canceled` / `flight_date` filters become hot:

```
-- optional expression indexes on CurrentStatusValues->>'canceled'
-- and (CurrentStatusValues->>'flight_date')::date
```

Defer until measured; start with in-memory filter after stage-scoped query (SCALE-41: 2k active rows).

---

## 3. Atomic Side-Effect Patterns

### 3a. Confirm Departed → To Arrival (RES-40)

```csharp
await using var tx = await _db.Database.BeginTransactionAsync(ct);
await _engine.UpdateStatusAsync(..., departure_status: Departed, departed_at);
await _engine.ExecuteTransitionAsync(..., "To Arrival"); // RemoveFromSource=true
// Initialize Arrival status Pending via transition side-effect or follow-up UpdateStatus
await tx.CommitAsync(ct);
```

### 3b. Not Departed fork (RES-41/42, TEST-43–45)

```csharp
await using var tx = ...
await _engine.UpdateStatusAsync(..., Not Departed, reason, outcome, canceled?);
if (outcome == BackToTicket) {
  await _engine.ExecuteTransitionAsync(..., "Back to Ticket");
  await _engine.UpdateStatusAsync(..., ticket_status: Pending); // rebook
} // Cancel: canceled=true already set; no transition
await tx.CommitAsync(ct);
```

FluentValidation: `Reason` required; `Outcome` required; if Reason=Other then `ReasonOther` not empty.

### 3c. Flag exception (RES-44, TEST-50–51)

```csharp
await using var tx = ...
await _engine.UpdateStatusAsync(..., Returned|Runaway);
if (!await _db.ExceptionCases.AnyAsync(e => e.CandidateId == id && e.Status == Open))
  _db.ExceptionCases.Add(new ExceptionCase { ... Status = Open });
else throw Conflict; // or no-op per BR — prefer Conflict for clarity
await tx.CommitAsync(ct);
```

### 3d. Add to Commission (RES-43, TEST-48–49, TEST-52)

```csharp
// Precondition: Arrival status Arrived; no Open ExceptionCase
await using var tx = ...
await _engine.ExecuteTransitionAsync(..., "Add to Commission"); // RemoveFromSource=false
var shell = await _db.Commissions.FirstOrDefaultAsync(c => c.CandidateId == id);
if (shell == null)
  _db.Commissions.Add(new Commission { Status = Open, snapshots... });
// else idempotent success
await tx.CommitAsync(ct);
```

Unique index: `UX_Commissions_CandidateId` (one shell per candidate for v1).

---

## 4. New Persistence (Exception + Commission)

### Tenant migration (Unit 4)

Tables: `ExceptionCases`, `InvestigationNotes`, `LiabilityAssignments`, `Commissions`

Indexes:

| Index | Purpose |
|-------|---------|
| `IX_ExceptionCases_Status` | List filter (SCALE-43) |
| `IX_ExceptionCases_CandidateId` | One-open check |
| `IX_InvestigationNotes_ExceptionCaseId` | Detail load |
| `UX_Commissions_CandidateId` | Shell idempotency |

All tenant-schema scoped via existing `TenantDbContext` + migrator path (same as Unit 3 StageEnteredAt pattern).

### Status tracks remain JSONB

Ticket / Departure / Arrival track fields stay on Candidate `CurrentStatusValues` + WorkflowEvent (no wide column migration for flight_date etc.).

---

## 5. Arrival Ledger Query (PERF-42, SCALE-42)

```csharp
query.Where(c => CurrentStageId == arrivalId || VisibleInStages.Contains(arrivalId))
  .OrderByDescending(c => c.StageEnteredAt ?? c.UpdatedAt)
  .Skip/Take // mandatory pagination
```

Optional filters: status, year, office, `hasCommission` (join or JSON flag `commission_linked`).

Denormalize `commission_linked=true` on Add to Commission for cheap board badge (USAB-43).

---

## 6. Authorization Design (SEC-40–47)

| Endpoint group | Permission |
|----------------|------------|
| Ticket board + Book Ticket | `travel.ticket` |
| Departure board + Notify / Departed / Not Departed | `travel.departure` |
| Arrival board + Arrived / Flag / Add to Commission | `travel.arrival` |
| Exception list/detail (read) | `travel.exception.view` |
| Exception notes / liability / close / status | `travel.exception` |
| To Departure / To Arrival / Back to Ticket (generic) | `workflow.execute` (+ intent perms when wrapped) |

Office scope: same as Unit 3 (`OfficeId` unless cross-office permission).

No uncancel endpoint in Unit 4 (SEC-43).

---

## 7. Notify Stub (TEST-47, RES-46)

```csharp
// MarkNotifiedHandler
await _engine.UpdateStatusAsync(..., notification_status: Notified, notified_at);
// ICandidateNotifier.TryNotifyAsync — Unit 4 registration: NoOpCandidateNotifier
```

Never await external HTTP in the request path.

---

## 8. Existing-Tenant Backfill

```
IWorkflowDefinitionUpgrader.EnsureUnit4ArtifactsAsync(tenant)
  - Ensure Ticket / Departure / Arrival / Commission stages exist (already seeded)
  - Ensure transitions: To Departure, To Arrival, Back to Ticket, Add to Commission
  - Ensure Add to Commission RemoveFromSource=false
  - Ensure travel.* permissions
  - Idempotent
```

EF tenant migration for new tables runs via `ITenantSchemaMigrator` before upgrader.

---

## 9. SignalR / Frontend Cache

After travel/arrival/exception mutations, invalidate:

```
mutate('/api/travel/ticket/board')
mutate('/api/travel/departure/board')
mutate('/api/arrival/board')
mutate('/api/exceptions')
mutate(key => key includes candidateId)
mutate(key => key includes '/workflow/')
```

Named routes: `/workflow/ticket`, `/workflow/departure`, `/workflow/arrival`, `/workflow/exceptions`.

---

## 10. PBT Architecture (Unit 4)

### Generators

```csharp
GenNonDepartureReason()
GenNotDepartedOutcome() // BackToTicket | CancelDeparture
GenTravelCommand() // BookTicket | Notify | Departed | NotDeparted | ...
GenArrivalCommand() // Arrived | Flag | AddToCommission
GenExceptionCommand() // Note | Liability | Close
```

### Properties (TEST-40–53)

| Property | Assert |
|----------|--------|
| TicketCompleteness | Book without destination/date fails |
| ToDepartureGate | Transition only when Booking Complete |
| CanceledExcluded | After Cancel, default board omits candidate |
| NotDepartedRequiresReasonOutcome | Validation fails if missing |
| BackToTicketPrimary | CurrentStage = Ticket |
| CancelBlocksArrival | To Arrival unavailable / fails |
| DepartedImpliesArrival | CurrentStage = Arrival |
| NotifyNoBot | NoOp notifier call count / no HTTP |
| ArrivalPermanence | After AddToCommission still on Arrival |
| CommissionShellIdempotent | One Commission row |
| ExceptionCreatesCase | Flag → Open case |
| OneOpenException | Second flag fails |
| CommissionBlockedIfOpenException | AddToCommission fails |
| StatefulTravelArrival | Model ↔ system after random legal seq |

---

## 11. Error & Observability

- ProblemDetails via existing Result pipeline
- Log: `CandidateId`, `Action`, `Reason` (enum), `Outcome`, `UserId` — not passport
- Metrics (optional): not-departed by reason, cancel rate, open exceptions count

---

## 12. Out of scope in NFR Design

- Telegram/WhatsApp delivery (Unit 7)
- Full commission fees/payments UI (Unit 5) — shell + visibility only
- Soft-copy / duplicate candidate rows
- Self-service uncancel
