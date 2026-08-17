# Tech Stack Decisions — Unit 5: Finance & Commission

## Confirmed stack (inherited)

No new frameworks. Unit 5 adds finance tables + Commission/Accounting modules on tenant schema.

| Component | Technology | Unit 5 notes |
|-----------|------------|--------------|
| API | Carter + MediatR | `CommissionModule`, `AccountingModule` |
| Validation | FluentValidation | Fees, payments, disputes, rates |
| Persistence | TenantDbContext | Extend Commission + new finance entities |
| Frontend | Next.js + SWR + sonner | Commission queue/detail; rates page |
| PBT | FsCheck | Finance property suite |

## Unit 5–specific decisions

### Decision 1: Commission-first slice
- **Choice**: Fees/payments/disputes/queue/report + payment journals; defer CoA UI, recon, statements, tax
- **Rationale**: Approved FD; delivers US-7.01–7.04, 7.07–7.08 value without blocking on full ERP UI
- **Trade-off**: US-7.05/7.06/7.09 marked deferred explicitly

### Decision 2: Extend Unit 4 Commission row
- **Choice**: Alter `commissions` table + child tables; keep CandidateId unique
- **Rationale**: Avoid dual aggregates; shells already exist in production tenants after Unit 4
- **Trade-off**: Migration must add nullable totals then backfill 0

### Decision 3: Cash Dr / Revenue Cr on payment
- **Choice**: Seeded accounts 1100 (Cash/Bank) and 4100 (Commission Revenue)
- **Rationale**: Approved Q2=A; simplest balanced posting for labour-export agencies
- **Future**: Optional AR-on-fees model later without changing Payment API

### Decision 4: Journal service in Accounting module
- **Choice**: `IJournalPostingService.PostCommissionPaymentAsync` called from payment handler
- **Rationale**: Keeps double-entry rules out of Commission handlers; testable in isolation
- **Trade-off**: Cross-module call inside one transaction via shared DbContext

### Decision 5: ETB functional currency
- **Choice**: All journal lines in ETB; `ExchangeRate` for conversion
- **Rationale**: Agency reporting in Ethiopia; matches FD
- **Trade-off**: Not full multi-currency CoA

### Decision 6: Idempotent CoA seed
- **Choice**: `EnsureDefaultChartOfAccountsAsync` on tenant migrate/provision (like workflow upgrader)
- **Rationale**: RES-52 — payments must not fail mysteriously on new tenants
- **Trade-off**: System accounts not editable in Unit 5 UI

### Decision 7: Permissions reuse existing codes
- **Choice**: `commission.read/update/create`, `accounting.read/post` (already in PermissionSeeder)
- **Rationale**: Same pattern as Unit 4 travel/arrival mapping
- **Trade-off**: Role maps may need finance officer defaults for new tenants

### Decision 8: No new Docker services
- **Choice**: Same Compose stack
- **Rationale**: Pure app + DB schema work
