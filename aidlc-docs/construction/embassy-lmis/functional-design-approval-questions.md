# Unit 3 Functional Design — Approval

**Unit**: Embassy & LMIS Processing  
**Stories**: US-3.01–US-3.11, US-4.01–US-4.05  
**Artifacts**:
- `construction/embassy-lmis/functional-design/domain-entities.md`
- `construction/embassy-lmis/functional-design/business-logic-model.md`
- `construction/embassy-lmis/functional-design/business-rules.md`
- `construction/embassy-lmis/functional-design/frontend-components.md`

## Design summary

- **No new aggregates** — Embassy/LMIS state via WorkflowEvent + `CurrentStatusValues` + mirrors
- **Case Executive** = mirror-only stage (never primary `CurrentStageId`); activated when `visa` is Ready/Submitted
- **EmbassyModule / LmisModule** = intent commands wrapping `IWorkflowEngineService`
- **Pages**: `/workflow/embassy`, `/workflow/case-executive`, `/workflow/lmis`
- **Side-effects**: Fit+Book Done → LMIS mirror; Insurance Paid → Available; Rejected requires reason + Resubmit → Ready

## Question 1
Approve Unit 3 Functional Design?

A) **Approve** — proceed to Unit 3 NFR Requirements

B) **Approve with changes** (describe after Answer)

C) **Request changes** before approving (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2
Case Executive board model — confirm preferred approach:

A) **Mirror-only stage** as designed (recommended — fits Unit 2 engine)

B) **Filtered Embassy view** only (no Case Executive stage / mirror rule — just UI filter on visa Ready|Submitted)

C) **Separate primary stage** (candidates leave Embassy when Ready — diverges from FR-03.5 “remain in Embassy”)

D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3
API surface for Unit 3:

A) **Dedicated EmbassyModule + LmisModule** with intent endpoints (BookMedical, …) as in application design (recommended)

B) **Generic WorkflowModule only** — frontend calls UpdateStatus/ExecuteTransition with raw track names (faster to ship, less domain clarity)

C) **Both** — intent modules + keep generic endpoints for admin/advanced use

D) Other (please describe after [Answer]: tag below)

[Answer]: A
