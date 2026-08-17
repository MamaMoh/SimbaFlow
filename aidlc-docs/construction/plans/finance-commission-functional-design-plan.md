# Functional Design Plan — Unit 5: Finance & Commission (ERP)

## Unit Context
- **Unit**: Finance & Commission (Unit 5)
- **Stories**: US-7.01–US-7.09
- **Dependencies**: Unit 2 (Candidate / events), Unit 4 (Commission shell + Arrival → Commission)
- **Existing**: `Commission` shell entity (Open + snapshots); `/workflow/commissions` stub page; `commission.*` permissions

## Plan

- [x] Step 1: Confirm module shape (Commission vs Accounting split)
- [x] Step 2: Resolve Commission shell extension vs new finance model
- [x] Step 3: Resolve double-entry / CoA / multi-currency / statements depth for v1
- [x] Step 4: Generate domain-entities, business-logic-model, business-rules, frontend-components
- [x] Step 5: Functional design approval questions

**Artifacts**: `construction/finance-commission/functional-design/`  
**Approval**: `construction/finance-commission/functional-design-approval-questions.md`

---

## Clarifying Questions

### Question 1 — Delivery scope for Unit 5
Unit of Work lists full ERP (CoA, journals, FX, commission, bank recon, statements, tax). How much in this unit?

A) **Phased — Commission-first** (recommended): extend Commission shell (fees, payments, disputes, queue); minimal journal posting on payment; defer full CoA UI, bank recon, statements, tax to a later finance slice

B) **Full Unit 5 as scoped** — Chart of Accounts + journals + commission + recon + statements + tax in one construction unit

C) **Commission + CoA/journals only** — skip bank recon, statements, and tax UI for now (keep posting model ready)

D) Other (please describe after [Answer]: tag below)

[Answer]:A

### Question 2 — Commission shell from Unit 4
Unit 4 already creates `Commission` (Open + country/office snapshots).

A) **Extend the same entity** (recommended) — add fees, payments, balances, statuses (Partial/Settled/Disputed)

B) **Replace shell** with a new aggregate and migrate/backfill shells

C) **Keep shell read-only**; new `CommissionLedger` linked 1:1 for finance detail

D) Other (please describe after [Answer]: tag below)

[Answer]:A

### Question 3 — Double-entry posting
When a payment is recorded (US-7.03):

A) **Post JournalEntry + JournalLines** always (recommended) — even if CoA is seeded defaults only

B) **Payment row only** in Unit 5; journal posting deferred

C) **Optional posting** if tenant has CoA configured; otherwise payment-only

D) Other (please describe after [Answer]: tag below)

[Answer]:A

### Question 4 — Multi-currency (US-7.04)
A) **ETB primary + FX rate table** — store payment currency + rate-to-ETB; convert for reports (recommended for agencies)

B) **Single currency (ETB) only** for v1 — ignore FX stories until later

C) **Full multi-currency CoA balances** per currency (heavier)

D) Other (please describe after [Answer]: tag below)

[Answer]:A

### Question 5 — Module shape
A) **CommissionModule + AccountingModule** as in Unit of Work (recommended)

B) **Single FinanceModule** covering both

C) **CommissionModule only** this unit; AccountingModule later

D) Other (please describe after [Answer]: tag below)

[Answer]:A

### Question 6 — Initialize commission trigger
Unit 4 already creates shell on Add to Commission. US-7 also mentions CandidateArrived → InitializeCommission.

A) **Keep Unit 4 shell as source of truth** — Unit 5 only enriches existing rows; no auto-create on Arrived (recommended — matches approved Unit 4)

B) **Also auto-create on Arrived** if no shell yet (duplicate path with Add to Commission)

C) **Move shell creation into Unit 5 only** — Unit 4 transition becomes visibility-only (requires Unit 4 follow-up)

D) Other (please describe after [Answer]: tag below)

[Answer]:A
