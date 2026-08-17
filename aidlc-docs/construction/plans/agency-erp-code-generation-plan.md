# Code Generation Plan — Unit 6: Agency ERP

## Unit Context
- **Unit**: Agency ERP (Unit 6) — Partners + licensing first (phased)
- **Workspace Root**: `/Users/mama/Dev/simbaflow`
- **Stories**: US-8.02 (harden), US-8.03, US-8.03a, US-8.06 (funnel subset), US-8.07 (extend)
- **Deferred**: US-8.01 staff, US-8.05 audit, full US-8.06 KPI suite
- **Dependencies**: Unit 1 (Tenant, RBAC), Unit 2 (intake PartnerLink)
- **Design decisions (approved)**:
  - Harden brownfield `PartnerModule` (no MediatR rewrite)
  - `/admin/partners` catalog vs tenant `/partners` links
  - Enforce licensed-country on link create
  - Provision + license edit + HQ office seed
  - Pipeline funnel v1 on Overview
  - No new Docker services / no new partner tables

## Permission code alignment

Reuse existing `PermissionSeeder` codes:

| Use | Code |
|-----|------|
| Catalog / links read | `partner.read` |
| Catalog create / link create | `partner.create` |
| Catalog update / link status | `partner.update` |
| Offices | `office.read` / `office.write` |
| SuperAdmin tenants / catalog | `system.admin` |
| Funnel | `candidate.read` |

---

## Code Generation Steps

### Phase A: Backend harden (partners + provision + license)

- [x] **Step 1**: Licensed-country gate on `POST /api/partners/links` — DONE (2026-07-29)
- [x] **Step 2**: `IHqOfficeSeedService` / `EnsureDefaultHqOfficeAsync` — DONE (2026-07-29)
- [x] **Step 3**: Extend `UpdateTenantRequest` + PUT handler — DONE (2026-07-29)
- [x] **Step 4**: Harden `CreateDepartment` to set `TenantId` — DONE (2026-07-29)

### Phase B: Dashboard API

- [x] **Step 5**: `GetPipelineFunnelQuery` (+ handler) — DONE (2026-07-29)
- [x] **Step 6**: `DashboardModule` — `GET /api/dashboard/pipeline-funnel` — DONE (2026-07-29)

### Phase C: Frontend

- [x] **Step 7**: API clients — extend partners/tenants; add `lib/api/dashboard.ts` — DONE (2026-07-29)
- [x] **Step 8**: `/admin/partners` catalog page + nav item (SuperAdmin) — DONE (2026-07-29)
- [x] **Step 9**: Harden `/partners` — hide catalog create for non-SuperAdmin; link-only UX — DONE (2026-07-29)
- [x] **Step 10**: Tenant license edit UI (sheet on tenants admin) — DONE (2026-07-29)
- [x] **Step 11**: Overview `PipelineFunnel` component + wire funnel API — DONE (2026-07-29)

### Phase D: Tests

- [x] **Step 12**: Example-based `AgencyErpServiceTests.cs` (link caps, license gate, HQ seed) — DONE (2026-07-29)
- [x] **Step 13**: FsCheck `AgencyErpProperties.cs` (TEST-60–68) — DONE (2026-07-29)

### Phase E: Docs

- [x] **Step 14**: Code summary — DONE (2026-07-29)
  - `aidlc-docs/construction/agency-erp/code/code-summary.md`
- [x] **Step 15**: Playwright — `/admin/partners` + funnel on overview — DONE (2026-07-29)

---

## Recommended execution batches

| Batch | Steps | Rationale |
|-------|-------|-----------|
| 1 | 1–4 | Partner gate + HQ seed + license PUT + Department TenantId |
| 2 | 5–6 | Dashboard funnel API |
| 3 | 7–11 | Frontend admin/partners, harden partners, license UI, overview funnel |
| 4 | 12–15 | Tests + summary + E2E touch-up |

---

## Story Traceability

| Story | Steps |
|-------|-------|
| US-8.03 Partner links | 1, 7, 9 |
| US-8.03a Partner catalog | 7, 8 |
| US-8.07 Provision / license | 2, 3, 10 |
| US-8.02 Offices | 2, 4 (HQ seed) |
| US-8.06 Dashboard funnel | 5, 6, 11 |
| US-8.01 Staff | Deferred |
| US-8.05 Audit | Deferred |

---

## Out of scope (explicit)

- Staff CRUD UI
- Audit trail viewer
- Full KPI dashboard (trends, revenue, SignalR live)
- MediatR refactor of PartnerModule
- Capital/bond UI / license status workflow engine
- New partner platform tables

---

## Estimated artifacts

| Area | Create | Modify |
|------|--------|--------|
| Backend services / modules | ~4 | ~4 (PartnerModule, Provision, Tenant PUT, Department) |
| Frontend | ~6 | ~4 (partners page, nav, overview, tenants) |
| Tests | ~2 | 0 |
| Docs | 1 | aidlc-state/audit |

**Total**: ~20–25 files touched (brownfield — lighter than Unit 5).
