# Tech Stack Decisions — Unit 6: Agency ERP

## Confirmed stack (inherited)

No new frameworks. Unit 6 hardens existing partner/tenant/office surfaces and adds license-edit + funnel APIs.

| Component | Technology | Unit 6 notes |
|-----------|------------|--------------|
| API | Carter (brownfield PartnerModule) | Keep inline handlers; no MediatR rewrite |
| Platform DB | PlatformDbContext | PartnerAgencies, PartnerLinks, Tenants |
| Tenant DB | TenantDbContext | Offices/Departments; HQ seed |
| Domain rules | `AgencyLevelRules` | Arts. 18–22 + Art. 40 |
| Frontend | Next.js + SWR + sonner | `/admin/partners`, harden `/partners`, Overview funnel |
| PBT | FsCheck | Cap + license + intake invariants |

## Unit 6–specific decisions

### Decision 1: Phased scope
- **Choice**: Partners + licensing + HQ seed + funnel; defer staff/audit/full KPIs
- **Rationale**: Approved FD Q1=A / Q6=A; brownfield already covers most partner path
- **Trade-off**: US-8.01 / US-8.05 remain open

### Decision 2: Harden PartnerModule in place
- **Choice**: Document + gap-fill (licensed-country enforce, admin route, tests)
- **Rationale**: Approved Q2=A; avoids rewrite risk mid-production
- **Trade-off**: Carter handlers stay less uniform than Units 3–5 MediatR style

### Decision 3: UX split catalog vs links
- **Choice**: `/admin/partners` (SuperAdmin catalog) vs `/partners` (tenant links only)
- **Rationale**: Approved Q4=A; prevents Agency Owner creating global duplicates
- **Trade-off**: Two pages to maintain; shared API client

### Decision 4: Enforce licensed countries on link
- **Choice**: Reject link when `LicensedCountries` non-empty and partner country not in list
- **Rationale**: Approved FD Q2=A
- **Trade-off**: Empty licensed list = no country gate (level caps still apply)

### Decision 5: HQ office seed on provision
- **Choice**: Idempotent create of default HQ when office count = 0
- **Rationale**: Candidates require `OfficeId`; approved Q3=A
- **Trade-off**: Naming (“Head Office”) may need localization later

### Decision 6: License edit post-provision
- **Choice**: SuperAdmin `UpdateTenantLicense` API + tenants UI sheet
- **Rationale**: Approved Q3=A; provision form alone is insufficient for renewals
- **Trade-off**: Over-cap existing links → warn/allow; block future violating links

### Decision 7: Pipeline funnel v1
- **Choice**: Stage count API + Overview visualization; not full BI
- **Rationale**: Approved Q5=A; Unit 8 owns deep reporting
- **Trade-off**: No trends/revenue/SignalR live funnel yet

### Decision 8: No new Docker services / packages
- **Choice**: Same Compose + NuGet/npm stack
- **Rationale**: Pure app + seed + UI work
