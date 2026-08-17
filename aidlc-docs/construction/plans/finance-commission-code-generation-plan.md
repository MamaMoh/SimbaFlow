# Code Generation Plan — Unit 5: Finance & Commission

## Unit Context
- **Unit**: Finance & Commission (Unit 5)
- **Workspace Root**: `/Users/mama/Dev/simbaflow`
- **Stories**: US-7.01–US-7.04, US-7.07–US-7.08 (phased); US-7.05/7.06/7.09 deferred UI
- **Dependencies**: Unit 4 Commission shell + Arrival permanence
- **Design decisions (approved)**:
  - Commission-first (fees, payments, disputes, queue, office report)
  - Extend Unit 4 `Commission` entity
  - Every payment posts Cash Dr / Revenue Cr journal (seeded CoA)
  - ETB + ExchangeRate table
  - CommissionModule + AccountingModule
  - No auto-create shell on Arrived
  - No new Docker services

## Permission code alignment

Reuse existing `PermissionSeeder` codes:

| Use | Code |
|-----|------|
| Queue / detail / report | `commission.read` |
| Fees / disputes | `commission.update` |
| Shell create | `commission.create` (Unit 4 only) |
| Journals / accounts / rates read | `accounting.read` |
| Record payment / upsert rates | `accounting.post` |

---

## Code Generation Steps

### Phase A: Domain & Persistence

- [x] **Step 1**: Extend `Commission` + new entities/enums — DONE (2026-07-22)
  - `CommissionFee`, `Payment`, `Dispute`, `Account`, `JournalEntry`, `JournalLine`, `FinanceCounter`
  - Enums: FeeType, PaymentMethod, DisputeStatus, AccountType
  - **FX note**: reuse platform `Tenancy.ExchangeRate` (public) — no tenant `exchange_rates` table (avoids duplicate of existing schema)
- [x] **Step 2**: TenantDbContext DbSets + Fluent configs + indexes — DONE (2026-07-22)
- [x] **Step 3**: EF Tenant migration `AddFinanceCommissionTables` — DONE (2026-07-22)
  - `Migrations/Tenant/20260722063523_AddFinanceCommissionTables.cs`

### Phase B: Services & Seed

- [x] **Step 4**: `IFinanceSeedService` — default CoA + totals backfill; Program + provision hooks — DONE (2026-07-22)
- [x] **Step 5**: `IExchangeRateService` (platform rates) + `IJournalPostingService` (Cash 1100 / Revenue 4100) — DONE (2026-07-22)
- [x] **Step 6**: Permission / role seed touch-up — DONE (2026-07-22)
  - FinanceOfficer already had commission/accounting; OfficeManager gained `accounting.read`

### Phase C: Commission API

- [x] **Step 7**: Commission queries — board, by id, office report — DONE (2026-07-27)
- [x] **Step 8**: Commission commands — UpsertFees, RecordPayment, Open/Resolve Dispute + validators — DONE (2026-07-27)
- [x] **Step 9**: CommissionModule (Carter) — DONE (2026-07-27)

### Phase D: Accounting API

- [x] **Step 10**: Accounting queries — accounts, journal by id, rates list — DONE (2026-07-27)
- [x] **Step 11**: UpsertExchangeRate + AccountingModule — DONE (2026-07-27)

### Phase E: Frontend

- [x] **Step 12**: API clients `commissions.ts`, `accounting.ts` — DONE (2026-07-27)
- [x] **Step 13**: UI — status badge, fee editor, payment sheet, dispute panel — DONE (2026-07-27)
- [x] **Step 14**: Pages — `/workflow/commissions`, `/workflow/commissions/[id]`, `/finance/rates`, `/finance/journals/[id]` — DONE (2026-07-27)

### Phase F: Tests

- [x] **Step 15**: Example-based FinanceCommission tests — DONE (2026-07-27)
- [x] **Step 16**: FsCheck `FinanceCommissionProperties.cs` (TEST-50–58) — DONE (2026-07-27)

### Phase G: Docs

- [x] **Step 17**: Code summary — DONE (2026-07-27)
  - `aidlc-docs/construction/finance-commission/code/code-summary.md`

---

## Recommended execution batches

| Batch | Steps | Rationale |
|-------|-------|-----------|
| 1 | 1–6 | Domain, migration, CoA seed, journal/FX services |
| 2 | 7–11 | Backend Commission + Accounting APIs |
| 3 | 12–14 | Frontend |
| 4 | 15–17 | Tests + summary |

---

## Story Traceability

| Story | Steps |
|-------|-------|
| US-7.01 Commission queue | 7, 9, 14 |
| US-7.02 Fee breakdown | 1, 8, 13, 14 |
| US-7.03 Payment + double-entry | 5, 8, 9, 13 |
| US-7.04 Multi-currency | 5, 11, 13, 14 |
| US-7.05 Bank recon | Deferred |
| US-7.06 Statements | Deferred |
| US-7.07 Disputes | 8, 13, 14 |
| US-7.08 Office reporting | 7, 14 |
| US-7.09 Tax | Deferred |

---

## Out of scope (explicit)

- CoA admin UI, bank reconciliation, P&L/BS/TB, tax UI
- AR-on-fees journal model
- Void/reverse payments
- Auto Commission on Arrived
- Partner/tenant licensing (Unit 6)

---

## Estimated artifacts

| Area | Create | Modify |
|------|--------|--------|
| Domain / Infra / Migration | ~15 | ~4 |
| API Commission/Accounting | ~20 | 0–2 |
| Frontend | ~12 | ~3 (nav, accounting stub) |
| Tests | ~3 | 0 |
| Docs | 1 | aidlc-state/audit |

**Total**: ~45–55 files touched.
