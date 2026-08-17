# Unit 4 NFR Requirements — Approval

**Unit**: Travel, Departure & Arrival  
**Artifacts**:
- `construction/travel-arrival/nfr-requirements/nfr-requirements.md`
- `construction/travel-arrival/nfr-requirements/tech-stack-decisions.md`

## Highlights

- Board queries &lt; 300–400ms; intents &lt; 500–600ms (Not Departed / Add to Commission)
- Arrival ledger scales with pagination; countdown excludes canceled
- Permissions: `travel.ticket|departure|arrival|exception`
- PBT: canceled exclusion, Not Departed fork, Arrival permanence, Commission shell idempotency, exception invariants
- **New tenant tables** for ExceptionCase / notes / liability / Commission shell; **no bot packages**

## Question 1
Approve Unit 4 NFR Requirements?

A) **Approve** — proceed to Unit 4 NFR Design

B) **Approve with changes** (describe after Answer)

C) **Request changes** before approving (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]: A
