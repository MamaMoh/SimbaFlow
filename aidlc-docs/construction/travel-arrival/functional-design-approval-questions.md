# Unit 4 Functional Design — Approval

**Unit**: Travel, Departure & Arrival  
**Stories**: US-5.01–US-5.08, US-6.01–US-6.07  
**Artifacts**:
- `construction/travel-arrival/functional-design/domain-entities.md`
- `construction/travel-arrival/functional-design/business-logic-model.md`
- `construction/travel-arrival/functional-design/business-rules.md`
- `construction/travel-arrival/functional-design/frontend-components.md`

## Design summary

- **TravelModule + ArrivalModule + ExceptionModule** wrapping `IWorkflowEngineService`
- **Full exception aggregates**: ExceptionCase, InvestigationNote, LiabilityAssignment
- **Not Departed**: required reason → **Back to Ticket** (rebook) or **Cancel departure** (`canceled=true`, stay on Departure, hidden from countdown)
- **Notify**: mark Notified only (bot = Unit 7)
- **Add to Commission**: transition with `RemoveFromSource=false` **+ Commission shell row** (finance detail Unit 5)
- **Arrival ledger permanent** — soft-copy / duplicate rows rejected (Q6 interpreted as **A**, not C)

## Question 1
Approve Unit 4 Functional Design?

A) **Approve** — proceed to Unit 4 NFR Requirements

B) **Approve with changes** (describe after Answer)

C) **Request changes** before approving (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]:A

## Question 2
Confirm Q6 / Arrival + Commission model:

A) **RemoveFromSource=false** — stay on Arrival forever; also visible in Commission (recommended — used in this design)

B) Arrival becomes mirror; Commission is primary

C) Soft-copy duplicate rows (rejected — conflicts with single-candidate model)

D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3
Confirm Commission shell in Unit 4 (your Q5 = B):

A) **Create Commission shell** on Add to Commission as designed (Open + snapshots)

B) **Transition only** — no Commission entity until Unit 5

C) **Defer Add to Commission button** until Unit 5

D) Other (please describe after [Answer]: tag below)

[Answer]: A
