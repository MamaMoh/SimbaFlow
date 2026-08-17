# Unit 6 — Functional Design Plan Approval

**Unit**: Agency ERP (Staff, Office, Partners, Admin)  
**Plan**: `construction/plans/agency-erp-functional-design-plan.md`  
**Domain**: `inception/requirements/partner-agency-and-tenant-licensing.md`

## Brownfield note

Partners catalog + tenant links, MoLS level/Art. 40 enforcement, candidate partner dropdown, and extended tenant provision are **already partially implemented**. Unit 6 FD will document and harden these, then close gaps (staff, audit, KPI dashboard, license edit, HQ office seed).

---

## Question 1 — Unit 6 delivery scope
Unit of Work lists staff, offices, partners, roles, dashboard, audit, tenant licensing. How much in this construction unit?

A) **Phased — Partners + licensing first** (recommended): formalize catalog/links + tenant license edit + HQ office seed + tests; defer staff CRUD, audit viewer, full KPI funnel to Unit 6 Batch 2 or Unit 8

B) **Full Unit 6 as scoped** — staff + offices + partners + dashboard funnel + audit trail in one unit

C) **Partners/licensing only** — skip staff, dashboard, audit entirely (offices already done)

D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 2 — Brownfield posture for partners
`PartnerModule` + `/partners` UI already exist.

A) **Document + harden** (recommended) — treat as v1 baseline; add gaps (SuperAdmin catalog route, agreement renewal UX, tests); no rewrite

B) **Refactor to MediatR/CQRS** — replace inline Carter handlers with commands/queries like Units 3–5

C) **Replace** — new partner aggregate/API from scratch

D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 3 — Tenant license lifecycle
Provision form already captures level + license + countries. What else?

A) **Provision + edit** (recommended) — SuperAdmin can edit tenant license fields after create; enforce caps on link create; auto-create **HQ branch office** on provision

B) **Provision only** — no post-create license edit UI this unit

C) **Full MoLS compliance** — capital/bond amounts, license status workflow, HQ office + forced password change on owner

D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 4 — SuperAdmin partner catalog UX
Who maintains the platform partner catalog?

A) **Dedicated admin route** `/admin/partners` for SuperAdmin catalog CRUD; tenant `/partners` stays link-only (recommended)

B) **Single `/partners` page** — tabs differ by role (SuperAdmin sees catalog edit; Agency Owner sees links only)

C) **API-only catalog** — SuperAdmin uses tenants UI or external tool; no catalog admin UI

D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 5 — Dashboard (US-8.06 / US-10.01)
Overview page shows basic candidate count today.

A) **Pipeline funnel v1** — stage counts + simple bar/funnel from workflow + candidate API (recommended)

B) **Keep stub** — only total candidates + stage count; detailed analytics in Unit 8

C) **Full KPI dashboard** — trends, office comparison, overdue detection in Unit 6

D) Other (please describe after [Answer]: tag below)

[Answer]: A

## Question 6 — Staff + audit
A) **Defer both** to a follow-up batch within Unit 6 or Unit 8 (recommended if Q1=A)

B) **Staff CRUD only** — adapt existing user/staff patterns; defer audit viewer

C) **Include staff + audit viewer** in Unit 6 v1

D) Other (please describe after [Answer]: tag below)

[Answer]: A
