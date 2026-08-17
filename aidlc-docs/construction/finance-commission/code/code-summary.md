# Unit 5 Code Gen — Finance & Commission (Batch 4 summary)

## What was implemented in this Unit

### Backend (Unit 5 Batch 2: Commission + Accounting APIs)
- `CommissionModule` (Carter routes under `/api/commissions`)
  - Queue / board: `GET /api/commissions/board` (and alias `GET /api/commissions/`)
  - Detail: `GET /api/commissions/{id}`
  - Office report: `GET /api/commissions/reports/by-office`
  - Fees editor: `PUT /api/commissions/{id}/fees`
  - Payment recording (FX + journal posting): `POST /api/commissions/{id}/payments`
  - Disputes: `POST /api/commissions/{id}/disputes`, `POST /api/commissions/disputes/{disputeId}/resolve`
- `AccountingModule` (Carter routes under `/api/accounting`)
  - Accounts: `GET /api/accounting/accounts`
  - Journal read: `GET /api/accounting/journals/{id}`
  - Exchange rates list: `GET /api/accounting/rates`
  - Exchange rates upsert: `POST /api/accounting/rates`

Key command/query handlers:
- `GetCommissionBoardQuery` / `GetCommissionByIdQuery` / `GetCommissionReportQuery`
- `UpsertCommissionFeesCommand`, `RecordPaymentCommand`, `OpenDisputeCommand`, `ResolveDisputeCommand`
- `GetAccountsQuery`, `GetJournalEntryByIdQuery`, `GetExchangeRatesQuery`
- `UpsertExchangeRateCommand`

Important domain services:
- `ExchangeRateService` (platform/public `ExchangeRates` schema)
- `JournalPostingService` (posts Cash/Bank 1100 Dr vs Commission Revenue 4100 Cr in one journal entry)

### Frontend (Unit 5 Batch 3: Finance UI)
- Commission UI:
  - `/workflow/commissions` (queue)
  - `/workflow/commissions/[id]` (detail: fees editor, payments list + record payment sheet, disputes panel)
- Rates UI:
  - `/finance/rates` (upsert exchange rates for non-ETB payments)
- Journal deep link:
  - `/finance/journals/[id]` (read-only)

Key TS/React files:
- `frontend/lib/api/commissions.ts`, `frontend/lib/api/accounting.ts`
- `frontend/components/finance/*` (status badge, fee editor, payment sheet, dispute panel)
- `frontend/app/(main)/workflow/commissions/*`, `frontend/app/(main)/finance/rates/*`

## Status/validation rules enforced (from Unit 5 design)
- Payment requires fees to exist.
- `Settled` commissions block fee edits.
- Every payment posts exactly one balanced journal entry (ETB) using:
  - Cash/Bank (1100) debit
  - Commission Revenue (4100) credit
- FX conversion to ETB uses platform exchange rates as-of the payment date.
- Disputes:
  - one open dispute at a time
  - resolving requires `ResolutionNotes`
  - commission status recalc follows balances unless disputed

## Test coverage
- Example-based tests in `backend/tests/SimbaFlow.API.Tests/Services/FinanceCommissionServiceTests.cs` (TEST-50..57).
- FsCheck property-based invariants in `backend/tests/SimbaFlow.API.Tests/Properties/FinanceCommissionProperties.cs` (TEST-50..58).
- **Results (2026-07-27)**: 97/97 backend tests passed; 28/28 Playwright E2E passed (API + frontend required).

## Bugfix during Batch 4
- `RecordPaymentHandler` removed redundant `commission.Payments.Add(payment)` after `_context.Payments.Add(payment)` — InMemory/EF was double-counting `TotalPaidAmount`.

## Files of interest
- Backend:
  - `backend/src/SimbaFlow.API/Features/Finance/CommissionModule.cs`
  - `backend/src/SimbaFlow.API/Features/Finance/Queries/CommissionQueries.cs`
  - `backend/src/SimbaFlow.API/Features/Finance/Commands/CommissionCommands.cs`
  - `backend/src/SimbaFlow.API/Features/Accounting/AccountingModule.cs`
  - `backend/src/SimbaFlow.API/Features/Accounting/Queries/AccountingQueries.cs`
  - `backend/src/SimbaFlow.API/Features/Accounting/Commands/AccountingCommands.cs`
- Frontend:
  - `frontend/app/(main)/workflow/commissions/page.tsx`
  - `frontend/app/(main)/workflow/commissions/[id]/page.tsx`
  - `frontend/app/(main)/finance/rates/page.tsx`

