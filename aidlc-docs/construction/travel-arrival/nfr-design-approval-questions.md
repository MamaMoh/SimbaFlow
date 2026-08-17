# Unit 4 NFR Design — Approval

**Unit**: Travel, Departure & Arrival  
**Artifacts**:
- `construction/travel-arrival/nfr-design/nfr-design.md`

## Highlights

- Travel / Arrival / Exception intent APIs with atomic status+transition(+shell/case) transactions
- Departure countdown: exclude canceled; Remaining Days projected; sort by flight_date
- New tenant tables + unique Commission shell; Arrival permanence via RemoveFromSource=false
- NoOp notifier; travel.* permissions; PBT map for TEST-40–53

## Question 1
Approve Unit 4 NFR Design?

A) **Approve** — proceed to Unit 4 Infrastructure Design

B) **Approve with changes** (describe after Answer)

C) **Request changes** before approving (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]: A
