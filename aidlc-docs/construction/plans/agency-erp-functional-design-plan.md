# Functional Design Plan — Unit 6: Agency ERP (Staff, Office, Partners, Admin)

## Unit Context
- **Unit**: Agency ERP (Unit 6)
- **Stories**: US-8.01–US-8.07, US-8.03a, US-10.01 (dashboard funnel)
- **Dependencies**: Unit 1 (Tenant, RBAC), Unit 2 (Candidate intake uses PartnerLink)
- **Domain reference**: `inception/requirements/partner-agency-and-tenant-licensing.md`
- **Brownfield**: Partial implementation already shipped (partners catalog/links, tenant level/license on provision, offices CRUD, basic overview)

## Already in codebase (audit 2026-07-27)

| Area | Status |
|------|--------|
| `PartnerAgency` + `PartnerLink` (public) | Entities + migration |
| `PartnerModule` | Catalog CRUD, tenant links, Art. 40 + level caps |
| `/partners` UI | Catalog / linked tabs, link + unlink |
| Candidate `partnerAgencyId` | Field + intake dropdown |
| Tenant provision | Agency level, license fields, licensed countries |
| Offices | CRUD page + API |
| Overview | Basic counts stub |
| Staff / Audit / KPI funnel | Not built |

## Plan

- [x] Step 1: Confirm Unit 6 scope vs brownfield — DONE (Q1=A phased)
- [x] Step 2: Resolve partner catalog admin UX — DONE (Q4=A `/admin/partners`)
- [x] Step 3: Resolve tenant license lifecycle — DONE (Q3=A provision+edit + HQ seed)
- [x] Step 4: Resolve staff + dashboard + audit depth — DONE (Q5=A funnel; Q6=A defer staff/audit)
- [x] Step 5: Generate domain-entities, business-logic-model, business-rules, frontend-components — DONE
- [x] Step 6: Functional design approval questions — DONE

**Artifacts**: `construction/agency-erp/functional-design/`  
**Approval**: `construction/agency-erp/functional-design-approval-questions.md`  
**Plan answers**: Q1–Q6 all A (user: `A,A,A,A,AA`)
