# Unit 3 NFR Requirements — Approval

**Unit**: Embassy & LMIS Processing  
**Artifacts**:
- `construction/embassy-lmis/nfr-requirements/nfr-requirements.md`
- `construction/embassy-lmis/nfr-requirements/tech-stack-decisions.md`

## Highlights

- Board queries &lt; 300ms; intent updates &lt; 500ms; mirror activation &lt; 1s (same request)
- Permission split: Embassy Officer vs Case Executive vs LMIS
- PBT for track independence, mirrors, milestone sequence, Paid→Available, To LMIS cleanup
- **No new packages**; intent modules + JSONB metadata + mirror-only Case Executive stage

## Question 1
Approve Unit 3 NFR Requirements?

A) **Approve** — proceed to Unit 3 NFR Design

B) **Approve with changes** (describe after Answer)

C) **Request changes** before approving (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]: A
