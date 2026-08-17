# Unit 6 Code Gen — Agency ERP (Batch 4 summary)

## Scope (phased)

Partners + licensing + HQ office seed + pipeline funnel. **Deferred:** staff CRUD (US-8.01), audit viewer (US-8.05), full KPI suite.

## What was implemented

### Backend
- **Licensed-country gate** on `POST /api/partners/links` (when `LicensedCountries` non-empty)
- **`PartnerLinkRules`** — shared pure validation (Art. 40, level caps, license country, agreement dates, intake eligibility)
- **`IHqOfficeSeedService` / `HqOfficeSeedService`** — idempotent HQ `Department` (`Code=HQ`) on provision
- **Tenant PUT** — `AgencyLevel`, license #/dates/`LicenseStatus`, `LicensedCountries`
- **`CreateDepartment`** — sets `TenantId`; code uniqueness per tenant
- **`GET /api/dashboard/pipeline-funnel`** — Active candidates by `CurrentStageId` (`candidate.read`)

### Frontend
- `/admin/partners` — SuperAdmin partner catalog + nav “Partner catalog”
- `/partners` — default **Linked** tab; catalog create SuperAdmin-only
- `EditAgencySheet` — MoLS level, license fields, destination countries
- Overview — `PipelineFunnel` wired to funnel API
- Clients: `lib/api/dashboard.ts`, `lib/api/partners.ts`

## Test coverage
- Example: `AgencyErpServiceTests.cs` (TEST-60–68 + HQ skip-when-exists)
- FsCheck: `AgencyErpProperties.cs` (TEST-60–68)
- Playwright: funnel on overview; SuperAdmin `/admin/partners`
- **Results (2026-07-29)**: **116/116** backend; **30/30** Playwright

## Files of interest
- `Domain/Services/PartnerLinkRules.cs`, `AgencyLevelRules.cs`
- `Infrastructure/Services/HqOfficeSeedService.cs`
- `API/Features/Partners/PartnerModule.cs`
- `API/Features/Dashboard/*`
- `frontend/app/(main)/admin/partners/page.tsx`
- `frontend/components/dashboard/pipeline-funnel.tsx`
- `frontend/components/tenants/edit-agency-sheet.tsx`
