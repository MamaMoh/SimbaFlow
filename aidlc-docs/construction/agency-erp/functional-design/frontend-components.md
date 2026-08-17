# Frontend Components — Unit 6: Agency ERP

## Pages

| Route | Audience | Purpose |
|-------|----------|---------|
| `/admin/partners` | SuperAdmin | Partner **catalog** CRUD (new dedicated route) |
| `/partners` | Agency Owner / staff | **Link-only**: linked partners, link from catalog, unlink/suspend |
| `/admin/tenants` (extend) | SuperAdmin | Edit license fields on existing tenant |
| `/offices` | Agency Owner | Existing CRUD (unchanged) |
| `/overview` | Authenticated | Pipeline funnel v1 (replace stub counts) |

**Deferred UI:** `/staff`, Audit trail viewer, full KPI charts (trends / revenue / destinations).

---

## Admin Partner Catalog (`/admin/partners`)

- Table: Name, Country, Capacity tier, Foreign license, Active, Active links count
- Actions: Create / Edit sheet; deactivate
- Client: extend `lib/api/partners.ts` (or reuse existing partner fetches with admin flag)
- Nav: Platform Admin section only

---

## Tenant Partners (`/partners`)

**Harden existing page:**

| Tab / mode | Behavior |
|------------|----------|
| Linked | Active (+ optionally Expired/Suspended) links; Unlink → Suspended |
| Link partner | Sheet: pick from catalog (filtered by licensed countries if set), agreement dates |
| Catalog create | **Remove or hide** for non-SuperAdmin — catalog create only on `/admin/partners` |

Agreement renewal: allow edit end date / re-activate Expired links (status update).

---

## Tenant license edit

- On tenants list/detail: Edit license sheet — AgencyLevel, License #, dates, LicenseStatus, LicensedCountries[], HQ address
- Show `AgencyLevelRules.Describe(level)` helper text (caps)
- Over-cap warning if existing links exceed new caps (informational)

---

## Overview — Pipeline funnel v1

- Horizontal bars or simple funnel: stage name + count
- Total candidates header
- Link each stage to `/candidates?stage=…` if filter supported; else deep-link workflow boards
- Loading / empty / access-denied patterns match existing `PageAlert`

---

## Shared components

| Component | Role |
|-----------|------|
| `CreatePartnerSheet` | Move/reuse for **admin** catalog create |
| `LinkPartnerSheet` | Tenant link from catalog (existing) |
| `EditTenantLicenseSheet` | New — SuperAdmin |
| `PipelineFunnel` | New — overview chart |
| Capacity / level badges | Display Art. 40 tier + level caps |

---

## API clients

```
lib/api/partners.ts          // catalog, links, status (existing — harden)
lib/api/tenants.ts           // provision + UpdateTenantLicense (extend)
lib/api/dashboard.ts         // GetPipelineFunnel (new)
```

---

## UX notes

- Tenant Partners page copy: “Link overseas partners your agency may send to”
- Admin catalog copy: “Shared partner master data (Art. 40)”
- Do not let Agency Owner create global catalog duplicates
- Funnel is read-only analytics — no stage transitions from Overview
