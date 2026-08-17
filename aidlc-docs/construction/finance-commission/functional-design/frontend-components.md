# Frontend Components — Unit 5: Finance & Commission

## Pages

| Route | Purpose |
|-------|---------|
| `/workflow/commissions` | Commission queue (replace Unit 4 stub) |
| `/workflow/commissions/[id]` | Detail: fees, payments, disputes, journal link |
| `/finance/rates` | Exchange rate list / upsert (simple) |
| `/finance/journals/[id]` | Journal entry read-only detail (optional deep link) |

**Deferred UI:** CoA admin, bank recon, P&L / BS / TB, tax.

Reuse existing finance nav (`/finance/accounting` can link to rates + “coming soon” for statements).

---

## Commission queue

- Columns: Candidate, Passport, Country, Office, Status, Fees (ETB), Paid, Balance, Opened
- Filters: status, office, search
- Row → detail page
- Client: `lib/api/commissions.ts`

---

## Commission detail

| Section | Actions |
|---------|---------|
| Header | Status badge, candidate link, snapshots |
| Fees | Editable table + save (UpsertFees) |
| Payments | List + **Record payment** sheet (amount, currency, method, date, ref) |
| Disputes | Open / resolve |
| Journal | Link to entry for each payment |

---

## Shared components

| Component | Role |
|-----------|------|
| `CommissionStatusBadge` | Open / Partial / Settled / Disputed |
| `RecordPaymentSheet` | Payment + currency |
| `FeeBreakdownEditor` | Fee lines |
| `DisputePanel` | Open/resolve forms |

---

## API clients

```
lib/api/commissions.ts   // board, detail, fees, payments, disputes
lib/api/accounting.ts    // journals (read), exchange rates
```

SignalR: invalidate commission keys on payment/fee updates (extend existing candidate/finance events if available; else mutate on success).

---

## UX notes

- Show ETB equivalents when payment currency ≠ ETB
- Block payment when no fees
- Disputed banner on detail header
- Accounting “statements / recon / tax” clearly marked deferred
