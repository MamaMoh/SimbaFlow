# NFR Design — Unit 5: Finance & Commission

Builds on Unit 4 (Commission shell) and Unit 1–3 baselines. Specifies how Commission/Accounting modules, payment+journal atomicity, FX, and status recalc meet Unit 5 NFRs.

---

## 1. Module architecture

```
HTTP  POST /api/commissions/{id}/payments
        │
        ▼
CommissionModule → RecordPaymentCommand
        │
        ▼
FluentValidation → amount > 0, fees exist, FX if needed
        │
        ▼
RecordPaymentHandler
  1. Load Commission + fees
  2. Resolve ExchangeRate → AmountEtb
  3. BeginTransaction
  4. Insert Payment
  5. IJournalPostingService.PostCommissionPaymentAsync (Cash Dr / Revenue Cr)
  6. Link JournalEntryId; RecalcTotalsAndStatus
  7. Commit
```

### Endpoint map

| Method | Path | Command / Query |
|--------|------|-----------------|
| GET | `/api/commissions/board` | `GetCommissionBoardQuery` |
| GET | `/api/commissions/{id}` | `GetCommissionByIdQuery` |
| PUT | `/api/commissions/{id}/fees` | `UpsertCommissionFeesCommand` |
| POST | `/api/commissions/{id}/payments` | `RecordPaymentCommand` |
| POST | `/api/commissions/{id}/disputes` | `OpenDisputeCommand` |
| POST | `/api/commissions/disputes/{id}/resolve` | `ResolveDisputeCommand` |
| GET | `/api/commissions/reports/by-office` | `GetCommissionReportQuery` |
| GET | `/api/accounting/accounts` | `GetAccountsQuery` |
| GET | `/api/accounting/journals/{id}` | `GetJournalEntryByIdQuery` |
| GET | `/api/accounting/rates` | `GetExchangeRatesQuery` |
| POST | `/api/accounting/rates` | `UpsertExchangeRateCommand` |

### Performance budget (PERF-50–53)

- Board &lt; 300ms; detail &lt; 400ms; fees &lt; 500ms; payment+journal &lt; 700ms p95
- Journal posting inside same transaction as Payment (RES-50)

---

## 2. Status & totals recalc

```csharp
void Recalc(Commission c) {
  c.TotalFeesAmount = c.Fees.Where(!deleted).Sum(f => f.AmountEtb);
  c.TotalPaidAmount = c.Payments.Where(!deleted).Sum(p => p.AmountEtb);
  c.BalanceAmount = c.TotalFeesAmount - c.TotalPaidAmount;
  if (c.Disputes.Any(d => d.Status == Open))
    c.Status = Disputed;
  else if (c.TotalFeesAmount > 0 && c.TotalPaidAmount >= c.TotalFeesAmount)
    c.Status = Settled;
  else if (c.TotalPaidAmount > 0)
    c.Status = Partial;
  else
    c.Status = Open;
}
```

Property TEST-51/52 assert this mapping for random fee/payment/dispute sets.

---

## 3. Journal posting design (Cash / Revenue)

### Seeded accounts

| Code | Role in payment |
|------|-----------------|
| `1100` | Cash/Bank — **Debit** AmountEtb |
| `4100` | Commission Revenue — **Credit** AmountEtb |

```csharp
// IJournalPostingService
PostCommissionPaymentAsync(Payment payment, Commission commission, userId, ct)
  Ensure accounts 1100 & 4100 exist
  entry = new JournalEntry {
    EntryNumber = next sequential,
    SourceType = "CommissionPayment",
    SourceId = payment.Id,
    Description = $"Commission payment {commission.Id}"
  };
  lines: Dr 1100, Cr 4100 (equal AmountEtb, 2 dp)
  Assert Sum(Debit) == Sum(Credit) else throw
```

**No AR path in Unit 5** (FD Q2=A).

---

## 4. FX design (PERF-54, TEST-54/55)

```csharp
decimal ResolveRate(string currency, DateOnly asOf) {
  if (currency equals "ETB", ignore case) return 1m;
  var rate = rates
    .Where(r => r.From == currency && r.To == "ETB" && r.EffectiveDate <= asOf)
    .OrderByDescending(r => r.EffectiveDate)
    .FirstOrDefault();
  if (rate is null) throw MissingExchangeRate;
  return rate.Rate;
}
AmountEtb = Round(Amount * rate, 2);
```

Index: unique `(from_currency, to_currency, effective_date)`.

---

## 5. Persistence & indexing

| Table | Indexes |
|-------|---------|
| `commissions` | UX CandidateId; IX Status, OpenedAt; columns TotalFees/Paid/Balance |
| `commission_fees` | IX CommissionId |
| `payments` | IX CommissionId; IX JournalEntryId |
| `disputes` | IX CommissionId; partial unique Open per commission |
| `accounts` | UX Code |
| `journal_entries` | UX EntryNumber; IX (SourceType, SourceId) |
| `journal_lines` | IX JournalEntryId, AccountId |
| `exchange_rates` | UX (From, To, EffectiveDate) |

Extend Unit 4 `Commission` via migration (nullable totals → backfill 0).

---

## 6. Board query (PERF-50)

```csharp
from c in Commissions.AsNoTracking()
join cand in Candidates on c.CandidateId equals cand.Id
where !c.IsDeleted && !cand.IsDeleted
  && (status == null || c.Status == status)
  && (officeFilter == null || cand.OfficeId == officeFilter)
orderby priority(c.Status), c.OpenedAt descending
```

Projection includes denormalized totals (no Sum in page query).

---

## 7. Authorization (SEC-50–53)

| Endpoint group | Permission |
|----------------|------------|
| Board / detail / report | `commission.read` |
| Fees / disputes | `commission.update` |
| Record payment | `accounting.post` (+ `commission.update` optional dual-check) |
| Accounts / journal read / rates read | `accounting.read` |
| Upsert rates | `accounting.post` |

Office scope: same as Unit 3/4 unless cross-office claim.

---

## 8. Tenant seed / backfill

```
EnsureDefaultChartOfAccountsAsync(tenant)
  Upsert 1100, 1200, 2100, 4100 (IsSystem=true)

EnsureUnit5FinanceAsync
  - CoA seed
  - Backfill Commission totals = 0 where null
```

Hook: after tenant EF migrate (Program + provision), parallel to workflow upgrader.

---

## 9. SignalR / cache

After fee/payment/dispute mutations:

```
mutate('/api/commissions/board')
mutate(`/api/commissions/${id}`)
mutate(key => key includes '/commissions')
```

---

## 10. PBT architecture

| Property | Assert |
|----------|--------|
| JournalAlwaysBalances | Debit == Credit for any payment amount |
| StatusFromBalances | Recalc model matches Open/Partial/Settled |
| DisputedOverrides | Open dispute → Disputed |
| PaymentRequiresFees | Empty fees → fail |
| EtbRateIsOne | ETB → AmountEtb == Amount |
| NonEtbNeedsRate | Missing rate → fail |
| OneCommissionPerCandidate | Unique model |
| NoShellOnArrived | Arrived-only does not insert Commission |
| StatefulFinance | Replay consistency |

---

## 11. Error & observability

- ProblemDetails; log `CommissionId`, `PaymentId`, `AmountEtb`, `UserId` — not passport
- Metrics (optional): payments posted/day, unbalanced reject count (should be 0)

---

## 12. Out of scope

- CoA admin UI, bank recon, P&L/BS/TB, tax
- AR-on-fees posting model
- Void/reverse payment journals (delete blocked)
- Auto Commission create on Arrived
