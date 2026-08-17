# Unit 6 Infrastructure Design — Approval

**Unit**: Agency ERP  
**Artifact**: `construction/agency-erp/infrastructure-design/infrastructure-design.md`

## Highlights

- Same Compose stack; **no new services / no new partner tables**
- Harden link create (licensed-country) + HQ `Department` seed on provision
- Extend tenant PUT license dates/status; thin Dashboard funnel API
- Frontend: `/admin/partners` + harden `/partners` + Overview funnel
- Reuse existing `partner.*` / `office.*` permissions

## Question 1
Approve Unit 6 Infrastructure Design?

A) **Approve** — proceed to Unit 6 Code Generation plan

B) **Approve with changes** (describe after Answer)

C) **Request changes** before approving (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]: A
