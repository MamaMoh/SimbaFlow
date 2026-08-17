# Frontend Components — Unit 4: Travel, Departure & Arrival

## Pages

| Route | Purpose |
|-------|---------|
| `/workflow/ticket` | Ticket board — book ticket, To Departure |
| `/workflow/departure` | Departure countdown (excludes canceled) |
| `/workflow/arrival` | Permanent arrival ledger |
| `/workflow/exceptions` | Exception case list + detail workspace |
| `/workflow/commission` | Commission visibility (shell rows; finance Unit 5) — may be stub table |

Reuse Unit 2/3 patterns: `WorkflowViewTable`, `ActionButtonBar`, stage layout, SignalR refresh.

---

## Ticket board

- Columns: Name, Passport, Destination, Flight date, Ticket status, Office, Partner
- Row actions: **Book Ticket** (sheet: destination, flight date, optional ref) → Booking Complete
- Engine actions: **To Departure** when Booking Complete
- Client: `lib/api/travel.ts` → TravelModule

---

## Departure board

- Sort by Remaining Days ascending
- Highlight overdue (RemainingDays < 0) and ≤3 days
- Columns: Name, Passport, Destination, Flight date, Remaining days, Notification, Departure status, Office
- Actions:
  - **Mark Notified**
  - **Confirm Departed** (→ Arrival)
  - **Not Departed** → sheet: reason (required) + outcome radio:
    - Back to Ticket (rebook)
    - Cancel departure (hide from countdown)
- Optional filter: “Show canceled” for history (off by default)

---

## Arrival board

- Permanent list including candidates already on Commission
- Columns: Name, Passport, Destination, Arrival status, Arrived at, Exception, Commission linked?
- Actions:
  - **Confirm Arrived**
  - **Flag Returned** / **Flag Runaway**
  - **Add to Commission** (creates shell; stays on board)

---

## Exception workspace

- List: type, status, candidate, opened at, financial impact
- Detail: candidate summary, status changer, note thread, liability table, close form
- Link from Arrival row when Returned/Runaway

---

## Shared components

| Component | Role |
|-----------|------|
| `NotDepartedSheet` | Reason + outcome (Back to Ticket \| Cancel) |
| `BookTicketSheet` | Destination + flight date |
| `RemainingDaysBadge` | Urgency coloring |
| `ExceptionStatusBadge` | Open / Investigating / Closed |
| `LiabilityForm` | Party + amount |
| Reuse `StatusUpdateSheet` / `ActionButtonBar` where generic |

---

## API clients

```
lib/api/travel.ts     // ticket + departure intents
lib/api/arrival.ts    // arrived, add to commission
lib/api/exceptions.ts // cases, notes, liability
```

SignalR: same candidate hubs as Unit 2/3 — refresh Ticket / Departure / Arrival / Exception boards on stage/status events.

---

## UX notes

- Not Departed is a **two-step** sheet (reason then outcome) — never silent cancel
- Canceled rows are history; do not clutter countdown
- Arrival never “empties” after Commission — ledger mental model
- Commission page can show shell Open status; payment UI is Unit 5
