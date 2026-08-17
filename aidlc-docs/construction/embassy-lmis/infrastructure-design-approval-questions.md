# Unit 3 Infrastructure Design — Approval

**Unit**: Embassy & LMIS Processing  
**Artifact**: `construction/embassy-lmis/infrastructure-design/infrastructure-design.md`

## Highlights

- **No Docker changes** — same api/frontend/postgres stack
- Tenant delta: `candidates.stage_entered_at` + index
- `IWorkflowDefinitionUpgrader` for Case Executive stage + mirror (existing tenants)
- Platform permissions `embassy.*` / `lmis.*`
- Engine: status chain, mirror cleanup on To LMIS, StageEnteredAt
- Carter EmbassyModule + LmisModule; three frontend boards

## Question 1
Approve Unit 3 Infrastructure Design?

A) **Approve** — proceed to Code Generation Plan

B) **Approve with changes** (describe after Answer)

C) **Request changes** before approving (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]: A
