# Unit 5 Functional Design — Approval

**Unit**: Finance & Commission (ERP)  
**Stories**: US-7.01–US-7.09 (phased)  
**Artifacts**:
- `construction/finance-commission/functional-design/domain-entities.md`
- `construction/finance-commission/functional-design/business-logic-model.md`
- `construction/finance-commission/functional-design/business-rules.md`
- `construction/finance-commission/functional-design/frontend-components.md`

## Design summary

- **Commission-first**: fees, payments, disputes, queue + reports; **no** CoA admin / bank recon / statements / tax UI
- **Extend** Unit 4 `Commission` shell; Unit 4 remains sole creator
- **Every payment** posts balanced JournalEntry (seeded CoA)
- **ETB + FX rates** for foreign payments
- **CommissionModule + AccountingModule**

## Question 1
Approve Unit 5 Functional Design?

A) **Approve** — proceed to Unit 5 NFR Requirements

B) **Approve with changes** (describe after Answer)

C) **Request changes** before approving (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2
Confirm payment journal model for v1:

A) **Cash/Bank Dr · Revenue Cr** per payment (as designed)

B) **AR on fees + clear AR on payment** (more classic; heavier)

C) **Payment row only** — skip journals until later

D) Other (please describe after [Answer]: tag below)

[Answer]: A
