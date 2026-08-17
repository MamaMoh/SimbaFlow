# Tech Stack Decisions — Unit 4: Travel, Departure & Arrival

## Confirmed stack (inherited)

No new frameworks. Unit 4 adds **intent modules** + **tenant tables** for exceptions and Commission shell on top of the Unit 2 engine.

| Component | Technology | Unit 4 notes |
|-----------|------------|--------------|
| API | Carter + MediatR | `TravelModule`, `ArrivalModule`, `ExceptionModule` |
| Validation | FluentValidation | Book ticket, Not Departed, liability, etc. |
| Persistence | TenantDbContext | Candidate/WorkflowEvent + new aggregates |
| Real-time | SignalR | Existing stage/status handlers |
| Frontend | Next.js + SWR + sonner | Named boards + exception workspace |
| PBT | FsCheck | Travel/Arrival/Exception property suite |

## Unit 4–specific decisions

### Decision 1: Three intent modules
- **Choice**: TravelModule (Ticket + Departure) + ArrivalModule + ExceptionModule
- **Rationale**: Matches FD and Unit of Work; keeps exception investigation separate from stage boards
- **Trade-off**: More modules than Embassy/Lmis pair; clearer permission boundaries

### Decision 2: Remaining Days in query projection
- **Choice**: Compute `flight_date − UtcNow.Date` in board DTO mapping (or SQL expression)
- **Rationale**: No stored “remaining days” column (stale overnight); sort by `flight_date` ascending for countdown
- **Trade-off**: Sort key is flight_date, not precomputed remaining — equivalent for ordering

### Decision 3: Canceled as status flag, not terminal stage
- **Choice**: `canceled=true` on Departure status values; stay `CurrentStage=Departure`
- **Rationale**: History stays on Departure board (optional filter); avoids new WorkflowStage; countdown excludes by default
- **Trade-off**: “Show canceled” UI needed for ops history

### Decision 4: Not Departed as single command with outcome
- **Choice**: `RecordNotDepartedCommand(reason, outcome)` — not separate “set Not Departed” then later cancel
- **Rationale**: Forces reason + fork in one atomic decision; matches UX sheet
- **Trade-off**: Cannot leave a row in limbo as “Not Departed” without outcome

### Decision 5: Full exception aggregates (not JSON-only)
- **Choice**: EF entities ExceptionCase, InvestigationNote, LiabilityAssignment + tenant migration
- **Rationale**: Investigation workspace (US-6.04–6.05); queryable liabilities for Unit 5 later
- **Trade-off**: Schema change vs Unit 3’s JSONB-only embassy tracks

### Decision 6: Commission shell in Unit 4
- **Choice**: Minimal `Commission` row (Open + snapshots) on Add to Commission
- **Rationale**: Approved FD Q3=A; Unit 5 extends fees/payments without inventing the link later
- **Trade-off**: Thin finance entity exists before full finance UI

### Decision 7: Arrival permanence via RemoveFromSource=false
- **Choice**: Seeded “Add to Commission” keeps Arrival visibility; no soft-copy rows
- **Rationale**: Single candidate model; permanent ledger (US-6.01)
- **Trade-off**: Arrival boards grow — pagination + filters required (SCALE-42)

### Decision 8: Bot notify out of scope
- **Choice**: Mark Notified is status + timestamp only; stub no-op for messaging
- **Rationale**: Unit 7 owns Telegram/WhatsApp; avoids flaky external I/O in travel path
- **Future**: Queue/outbox when bot unit lands

### Decision 9: Permissions additive
- **Choice**: `travel.ticket`, `travel.departure`, `travel.arrival`, `travel.exception`, `travel.exception.view`
- **Rationale**: Least privilege across ticketing staff, departure ops, arrival officers, investigators
- **Trade-off**: Role seed updates for new + existing tenants

### Decision 10: Named frontend routes
- **Choice**: `/workflow/ticket`, `/workflow/departure`, `/workflow/arrival`, `/workflow/exceptions` (+ commission stub)
- **Rationale**: Same pattern as Unit 3 named boards; bind to seeded stage names
- **Mitigation**: Well-known stage name constants; warn on admin rename
