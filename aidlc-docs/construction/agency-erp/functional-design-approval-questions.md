# Unit 6 Functional Design — Approval

**Unit**: Agency ERP (Staff, Office, Partners, Admin)  
**Stories**: US-8.02–8.03a, US-8.06 (funnel), US-8.07 (extend); **defer** US-8.01 staff, US-8.05 audit  
**Plan answers**: Q1–Q6 all **A**  
**Artifacts**:
- `construction/agency-erp/functional-design/domain-entities.md`
- `construction/agency-erp/functional-design/business-logic-model.md`
- `construction/agency-erp/functional-design/business-rules.md`
- `construction/agency-erp/functional-design/frontend-components.md`

## Design summary

- **Phased**: harden partners + tenant license edit + HQ office seed + pipeline funnel; defer staff/audit
- **Brownfield**: keep existing `PartnerModule` / entities (no MediatR rewrite)
- **UX split**: `/admin/partners` catalog vs `/partners` tenant links
- **Provision**: auto HQ office; SuperAdmin can edit license post-create
- **Overview**: stage-count funnel v1

## Question 1
Approve Unit 6 Functional Design?

A) **Approve** — proceed to Unit 6 NFR Requirements

B) **Approve with changes** (describe after Answer)

C) **Request changes** before approving (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2
Confirm licensed-country enforcement on partner **link** create:

A) **Enforce** — reject link if partner country ∉ tenant `LicensedCountries` when list is non-empty (recommended)

B) **Warn only** — allow link; show banner

C) **No check** — level caps only (current code behavior)

D) Other (please describe after [Answer]: tag below)

[Answer]: A
