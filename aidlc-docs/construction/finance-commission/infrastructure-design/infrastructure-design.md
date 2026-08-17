# Infrastructure Design — Unit 5: Finance & Commission

## Deployment context

Same Docker Compose stack (api + frontend + postgres). No new containers or external ledgers.

Infrastructure work:

1. Tenant schema: extend `commissions` + Fee / Payment / Dispute / Account / Journal / ExchangeRate tables
2. CoA seed (`EnsureDefaultChartOfAccountsAsync`) on migrate/provision
3. Permission role touch-up (reuse existing `commission.*` / `accounting.*`)
4. Carter modules + `IJournalPostingService` DI
5. Frontend routes (replace commissions stub)

---

## 1. Database schema delta (tenant)

### 1a. Alter commissions

```sql
ALTER TABLE commissions
  ADD COLUMN IF NOT EXISTS total_fees_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS total_paid_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS balance_amount NUMERIC(18,2) NOT NULL DEFAULT 0;

-- status already int; ensure Partial/Settled/Disputed values used by app
CREATE INDEX IF NOT EXISTS ix_commissions_status ON commissions (status) WHERE is_deleted = FALSE;
CREATE INDEX IF NOT EXISTS ix_commissions_opened_at ON commissions (opened_at DESC) WHERE is_deleted = FALSE;
```

### 1b. New tables

```sql
CREATE TABLE commission_fees (
  id UUID PRIMARY KEY,
  commission_id UUID NOT NULL REFERENCES commissions(id),
  fee_type INT NOT NULL,
  description TEXT NULL,
  amount NUMERIC(18,2) NOT NULL,
  currency VARCHAR(8) NOT NULL DEFAULT 'ETB',
  amount_etb NUMERIC(18,2) NOT NULL,
  sort_order INT NOT NULL DEFAULT 0,
  -- BaseEntity columns...
);

CREATE INDEX ix_commission_fees_commission ON commission_fees (commission_id) WHERE is_deleted = FALSE;

CREATE TABLE payments (
  id UUID PRIMARY KEY,
  commission_id UUID NOT NULL REFERENCES commissions(id),
  amount NUMERIC(18,2) NOT NULL,
  currency VARCHAR(8) NOT NULL,
  exchange_rate_to_etb NUMERIC(18,8) NOT NULL,
  amount_etb NUMERIC(18,2) NOT NULL,
  paid_at TIMESTAMPTZ NOT NULL,
  method INT NOT NULL,
  reference TEXT NULL,
  notes TEXT NULL,
  journal_entry_id UUID NULL,
  recorded_by_user_id UUID NOT NULL,
  -- BaseEntity...
);

CREATE INDEX ix_payments_commission ON payments (commission_id) WHERE is_deleted = FALSE;

CREATE TABLE disputes (
  id UUID PRIMARY KEY,
  commission_id UUID NOT NULL REFERENCES commissions(id),
  status INT NOT NULL,
  reason TEXT NOT NULL,
  opened_at TIMESTAMPTZ NOT NULL,
  opened_by_user_id UUID NOT NULL,
  resolved_at TIMESTAMPTZ NULL,
  resolution_notes TEXT NULL,
  resolved_by_user_id UUID NULL,
  -- BaseEntity...
);

CREATE UNIQUE INDEX ux_disputes_commission_open
  ON disputes (commission_id)
  WHERE is_deleted = FALSE AND status = 0; -- Open

CREATE TABLE accounts (
  id UUID PRIMARY KEY,
  code VARCHAR(32) NOT NULL,
  name VARCHAR(256) NOT NULL,
  type INT NOT NULL,
  currency VARCHAR(8) NOT NULL DEFAULT 'ETB',
  is_system BOOLEAN NOT NULL DEFAULT FALSE,
  is_active BOOLEAN NOT NULL DEFAULT TRUE,
  -- BaseEntity...
);

CREATE UNIQUE INDEX ux_accounts_code ON accounts (code) WHERE is_deleted = FALSE;

CREATE TABLE journal_entries (
  id UUID PRIMARY KEY,
  entry_number VARCHAR(64) NOT NULL,
  posted_at TIMESTAMPTZ NOT NULL,
  description TEXT NOT NULL,
  source_type VARCHAR(64) NOT NULL,
  source_id UUID NULL,
  posted_by_user_id UUID NOT NULL,
  -- BaseEntity...
);

CREATE UNIQUE INDEX ux_journal_entries_number ON journal_entries (entry_number) WHERE is_deleted = FALSE;
CREATE INDEX ix_journal_entries_source ON journal_entries (source_type, source_id);

CREATE TABLE journal_lines (
  id UUID PRIMARY KEY,
  journal_entry_id UUID NOT NULL REFERENCES journal_entries(id),
  account_id UUID NOT NULL REFERENCES accounts(id),
  debit NUMERIC(18,2) NOT NULL DEFAULT 0,
  credit NUMERIC(18,2) NOT NULL DEFAULT 0,
  memo TEXT NULL,
  -- BaseEntity...
);

CREATE INDEX ix_journal_lines_entry ON journal_lines (journal_entry_id);

CREATE TABLE exchange_rates (
  id UUID PRIMARY KEY,
  from_currency VARCHAR(8) NOT NULL,
  to_currency VARCHAR(8) NOT NULL,
  rate NUMERIC(18,8) NOT NULL,
  effective_date DATE NOT NULL,
  source VARCHAR(64) NULL,
  -- BaseEntity...
);

CREATE UNIQUE INDEX ux_exchange_rates_pair_date
  ON exchange_rates (from_currency, to_currency, effective_date)
  WHERE is_deleted = FALSE;
```

FK: `payments.journal_entry_id` → `journal_entries.id` (nullable until posted; set in same transaction).

### 1c. EF migration

```
dotnet ef migrations add AddFinanceCommissionTables \
  --project src/SimbaFlow.Infrastructure \
  --context TenantDbContext \
  --output-dir Migrations/Tenant
```

Via existing `ITenantSchemaMigrator`.

---

## 2. CoA / finance seeder

```
IFinanceSeedService
  EnsureDefaultChartOfAccountsAsync(ITenantDbContext, ct)
  EnsureUnit5BackfillAsync(...) // totals defaults
```

| Code | Name | Type |
|------|------|------|
| 1100 | Cash / Bank | Asset |
| 1200 | Accounts Receivable — Commissions | Asset |
| 2100 | Clearing / Suspense | Liability |
| 4100 | Commission Revenue | Revenue |

Idempotent upsert by `Code`. Hook after migrate in Program + `ProvisionTenantCommand`.

Entry numbers: tenant sequence table or `MAX(entry_number)+1` with row lock — prefer simple `JE-{yyyyMMdd}-{seq}` using DB sequence or counter row `finance_counters`.

**v1:** `finance_counters` single-row table (`next_journal_number INT`) updated in payment transaction.

---

## 3. Platform permissions

Already seeded:

```
commission.read / create / update
accounting.read / post / reconcile
```

Optional new-tenant role map:

| Role | Permissions |
|------|-------------|
| Finance Officer | commission.read, commission.update, accounting.read, accounting.post |
| Office Manager | commission.read + report |

No new permission codes required unless product wants `commission.dispute` later.

---

## 4. Application wiring

### DI

```
services.AddScoped<IJournalPostingService, JournalPostingService>();
services.AddScoped<IFinanceSeedService, FinanceSeedService>();
services.AddScoped<IExchangeRateService, ExchangeRateService>();
```

### Carter

```
Features/Commissions/CommissionModule.cs
Features/Accounting/AccountingModule.cs
```

Discovery via existing assembly scan.

### Transaction pattern

`RecordPaymentHandler` casts `TenantDbContext` / uses `BeginTransactionAsync` — same Unit 4 pattern; suppress InMemory warning in tests.

---

## 5. Frontend infrastructure

```
app/(main)/workflow/commissions/page.tsx          // replace stub
app/(main)/workflow/commissions/[id]/page.tsx
app/(main)/finance/rates/page.tsx
```

Optional: link from `/finance/accounting` stub to rates + “statements deferred”.

Clients: `lib/api/commissions.ts`, `lib/api/accounting.ts`.

BFF proxy unchanged.

---

## 6. Observability & ops

| Concern | Approach |
|---------|----------|
| Logging | Serilog; AmountEtb + CommissionId |
| Health | Unchanged |
| Backups | pg_dump covers new tables |
| Config | No new env vars |

---

## 7. Test infrastructure

| Suite | Location |
|-------|----------|
| Example | `SimbaFlow.API.Tests/Services/FinanceCommission*Tests.cs` |
| PBT | `Properties/FinanceCommissionProperties.cs` |
| Patterns | Unit 4 TravelArrival + InMemory transaction ignore |

No new CI jobs.

---

## 8. Rollback / risk

| Change | Risk | Mitigation |
|--------|------|------------|
| Alter commissions | Low | Defaults 0 |
| New finance tables | Medium | Additive migration |
| CoA seed | Low | Idempotent |
| Payment+journal | Medium | Atomic tx + balance assert |
| Entry number race | Medium | Counter row in same tx |

---

## 9. Deliverable checklist (code generation plan)

- [ ] Domain entities + enums + Commission extension
- [ ] TenantDbContext + migration
- [ ] FinanceSeedService + Program/provision hooks
- [ ] JournalPostingService + ExchangeRateService
- [ ] CommissionModule + AccountingModule + validators
- [ ] Frontend queue/detail/rates + API clients
- [ ] Tests (example + FsCheck TEST-50–58)
- [ ] Code summary under `construction/finance-commission/code/`
