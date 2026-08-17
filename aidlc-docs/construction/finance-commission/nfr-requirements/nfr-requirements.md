# NFR Requirements — Unit 5: Finance & Commission

Inherits Unit 1–4 NFR baselines. Adds targets for commission queue/detail, fee/payment/dispute flows, journal posting, and FX rates. **Out of scope for this unit’s UI NFRs:** CoA admin, bank recon, full statements, tax.

## NFR-PERF: Performance Requirements

| ID | Requirement | Target | Context |
|----|-------------|--------|---------|
| PERF-50 | Commission queue (paginated) | < 300ms p95 | Status/office filters + candidate join |
| PERF-51 | Commission detail (fees+payments+disputes) | < 400ms p95 | Single aggregate load |
| PERF-52 | Upsert fees | < 500ms p95 | Replace lines + recalc totals |
| PERF-53 | Record payment + journal | < 700ms p95 | Payment + balanced JournalEntry one transaction |
| PERF-54 | Exchange rate lookup | < 50ms p95 | Indexed From/To/EffectiveDate |
| PERF-55 | Office commission report | < 500ms p95 | Date range aggregate |
| PERF-56 | Journal entry by id | < 200ms p95 | Entry + lines |

## NFR-SCALE: Scalability Requirements

| ID | Requirement | Target | Strategy |
|----|-------------|--------|----------|
| SCALE-50 | Commission rows per tenant | 50,000+ | Pagination; indexes on Status, OpenedAt |
| SCALE-51 | Fees per commission | 50+ | Child table; replace-all OK |
| SCALE-52 | Payments per commission | 200+ | Append-only |
| SCALE-53 | Journal entries | 100,000+ | SourceType/SourceId index |
| SCALE-54 | Concurrent finance users | 20+ per tenant | SWR + optimistic disable on submit |
| SCALE-55 | Exchange rates | 10 currencies × daily | Composite unique (From, To, EffectiveDate) |

## NFR-SEC: Security Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| SEC-50 | Commission read vs update split | `commission.read` / `commission.update` |
| SEC-51 | Payment posts require accounting permission | `accounting.post` (or commission.update + accounting.post) |
| SEC-52 | Journal/accounts read | `accounting.read` |
| SEC-53 | FX rate write | `accounting.post` or dedicated rate permission |
| SEC-54 | No Commission create from Unit 5 UI | Shell only from Unit 4 Add to Commission |
| SEC-55 | Office-scoped queue by default | Filter unless cross-office permission |
| SEC-56 | Amounts never logged with full card/bank secrets | Reference only; no PAN |
| SEC-57 | Settled fee edits blocked server-side | BR-C08 |

## NFR-RES: Resiliency Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| RES-50 | Payment + journal atomic | Single DB transaction; rollback both |
| RES-51 | Unbalanced journal rejected | Σ Debit ≠ Σ Credit → fail before commit |
| RES-52 | Missing CoA seed blocks payment with clear error | EnsureDefaultChartOfAccounts on provision/migrate |
| RES-53 | Missing FX rate fails payment (non-ETB) | No silent rate=1 for foreign currency |
| RES-54 | Fee recalc after payment always consistent | Recalc in same transaction |
| RES-55 | SignalR/mutate failure does not roll back payment | After commit |

## NFR-TEST: PBT Requirements (Unit-Specific)

| ID | Requirement | PBT Rule | Implementation |
|----|-------------|----------|----------------|
| TEST-50 | Journal always balances | PBT-03 | Generated payment journals Debit==Credit |
| TEST-51 | Status from balances | PBT-03 | Open/Partial/Settled invariant vs fees/paid |
| TEST-52 | Disputed overrides status | PBT-03 | Open dispute → Disputed |
| TEST-53 | Payment requires fees | PBT-04 | Empty fees → validation/handler fail |
| TEST-54 | ETB rate is 1 | PBT-03 | Currency ETB → AmountEtb == Amount |
| TEST-55 | Non-ETB needs rate | PBT-04 | Missing rate → fail |
| TEST-56 | One commission per candidate | PBT-03 | Unique constraint / upsert model |
| TEST-57 | No Unit 5 shell create on Arrived | PBT-03 | Arrived alone does not insert Commission |
| TEST-58 | Stateful fee/payment/dispute sequences | PBT-06 | Random legal finance commands |

## NFR-USAB: Usability Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| USAB-50 | Queue shows Fees / Paid / Balance / Status | Columns + badges |
| USAB-51 | Show ETB equivalent for foreign payments | Detail + toast |
| USAB-52 | Block payment CTA when no fees | Disabled + reason |
| USAB-53 | Disputed banner on detail | Alert |
| USAB-54 | Deferred features labeled | Accounting stub pages |
| USAB-55 | Success/error toasts on fee/payment/dispute | sonner |

## Tech Stack Additions (Unit 5-Specific)

| Package | Purpose |
|---------|---------|
| (None required) | Reuses MediatR, FluentValidation, EF Core, SWR |
| EF Tenant migration | Extend Commission; add Fee, Payment, Dispute, Account, Journal*, ExchangeRate |

## Testable Properties Summary

1. Balanced journals on every payment  
2. Commission status ↔ fees/paid/dispute  
3. FX conversion rules  
4. Shell uniqueness / no Arrived auto-create  
5. Stateful finance command model  
