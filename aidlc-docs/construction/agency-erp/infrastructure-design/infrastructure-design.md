# Infrastructure Design — Unit 6: Agency ERP

## Deployment context

Same Docker Compose stack (api + frontend + postgres). **No new containers.**

Infrastructure work is mostly **brownfield hardening**:

1. Partner link create: licensed-country gate (app logic only)
2. Provision: idempotent HQ office (`Department`) seed
3. Tenant PUT: license date/status fields (schema may already exist)
4. New Dashboard funnel endpoint + Overview UI
5. Frontend: `/admin/partners` + harden `/partners`
6. Tests: example + FsCheck TEST-60–68

---

## 1. Database schema

### 1a. Platform (already present)

| Table | Status |
|-------|--------|
| `PartnerAgencies` | Exists (migration `AddTenantLicensingAndPartnerCatalog`) |
| `PartnerLinks` | Exists — UX `(TenantId, PartnerAgencyId)` |
| `Tenants` | Has `AgencyLevel`, license fields, `LicensedCountries` |

**Verify indexes** (add only if missing):

```sql
-- PartnerAgencies
CREATE INDEX IF NOT EXISTS ix_partner_agencies_country
  ON "PartnerAgencies" ("CountryCode") WHERE "IsDeleted" = FALSE;
CREATE INDEX IF NOT EXISTS ix_partner_agencies_active
  ON "PartnerAgencies" ("IsActive") WHERE "IsDeleted" = FALSE;

-- PartnerLinks (expect unique already)
-- IX PartnerAgencyId for Art. 40 counts
```

**No new platform tables** for Unit 6 v1.

### 1b. Tenant license columns on PUT

Entity already has `LicenseIssuedAt`, `LicenseExpiresAt`, `LicenseStatus`.  
Extend `UpdateTenantRequest` / PUT handler to accept them if not yet wired in API DTO.

### 1c. Offices / HQ seed target

Offices map to `Department` on **Application/Platform** DbContext (`Departments` table), scoped by `TenantId`.

```csharp
// EnsureDefaultHqOfficeAsync(tenantId, tenant HQ address fields)
var any = await departments.AnyAsync(d => d.TenantId == tenantId && !d.IsDeleted);
if (any) return;
departments.Add(new Department {
  Name = "Head Office",
  Code = "HQ",
  Description = "Default HQ branch (auto-created on provision)",
  TenantId = tenantId,
  IsActive = true
});
```

**Idempotent:** only when zero non-deleted departments for that `TenantId`.  
**Note:** harden `CreateDepartment` to set `TenantId` from current user when missing (gap if not already).

### 1d. Candidate stage for funnel

Funnel groups by `Candidate.CurrentStageId` (+ `CurrentStageName` for display).  
Optional index if missing:

```sql
-- tenant schema
CREATE INDEX IF NOT EXISTS ix_candidates_current_stage
  ON candidates (current_stage_id)
  WHERE is_deleted = FALSE;
```

Only add via tenant migration if profiling shows need; otherwise defer.

---

## 2. Application wiring

### PartnerModule (harden)

```
POST /api/partners/links
  + LicensedCountries gate (after Art. 40 + level checks)
```

No DI changes for partners.

### ProvisionTenantHandler

```
after schema migrate + user create:
  EnsureDefaultHqOfficeAsync(tenant.Id, …)
  assert MustChangePassword = true on owner
```

### TenantModule PUT

```
UpdateTenantRequest += LicenseIssuedAt?, LicenseExpiresAt?, LicenseStatus?
CountriesWithinLimit before save (already partial)
```

### DashboardModule (new, thin)

```
Features/Dashboard/DashboardModule.cs
  GET /api/dashboard/pipeline-funnel
    → GetPipelineFunnelQuery (MediatR) or inline Carter
Permission: candidate.read / system.admin
```

### DI

```
// Optional helper
services.AddScoped<IHqOfficeSeedService, HqOfficeSeedService>();
```

Or private method on `ProvisionTenantHandler` — prefer small dedicated service for testability.

---

## 3. Permissions

Already seeded — **reuse**:

```
partner.read / partner.create / partner.update
office.read / office.write
system.admin (SuperAdmin)
candidate.read (funnel)
```

| Surface | Permission |
|---------|------------|
| `/admin/partners` | SuperAdmin or partner.create/update |
| Tenant link | partner.create / partner.update |
| License edit | SuperAdmin policy on `/api/tenants` |
| Funnel | candidate.read |

No new PermissionSeeder rows required.

---

## 4. Frontend infrastructure

```
app/(main)/(admin)/partners/page.tsx     // NEW — catalog CRUD (or app/(main)/admin/partners)
app/(main)/partners/page.tsx             // HARDEN — hide catalog create; link-only
components/tenants/edit-tenant-license-sheet.tsx  // NEW or extend create-agency-sheet patterns
components/dashboard/pipeline-funnel.tsx // NEW
app/(main)/overview/page.tsx             // integrate funnel
components/layout/nav-items.ts           // add Admin → Partners
lib/api/partners.ts                      // harden / reuse
lib/api/dashboard.ts                     // NEW funnel client
lib/api/tenants.ts                       // extend PUT license fields
```

BFF `/api/proxy/[...path]` unchanged.

E2E: add `/admin/partners` to SuperAdmin nav tests if present; keep `/partners` for agency owner.

---

## 5. Observability & ops

| Concern | Approach |
|---------|----------|
| Logging | Serilog; TenantId + PartnerAgencyId on link reject |
| Health | Unchanged |
| Config | No new env vars |
| Backups | Unchanged (platform tables already covered) |

---

## 6. Test infrastructure

| Suite | Location |
|-------|----------|
| Example | `Services/AgencyErpServiceTests.cs` |
| PBT | `Properties/AgencyErpProperties.cs` (TEST-60–68) |
| Patterns | InMemory PlatformDbContext; reuse AgencyLevelRules pure tests |

No new CI jobs.

---

## 7. Rollback / risk

| Change | Risk | Mitigation |
|--------|------|------------|
| Licensed-country gate | Low | Additive validation; empty list = no gate |
| HQ seed | Low | Idempotent; Code=HQ unique per tenant |
| Funnel API | Low | Read-only; Overview degrades gracefully |
| Admin partners page | Low | UI only; API exists |
| License PUT dates | Low | Columns exist; DTO wiring |

---

## 8. Deliverable checklist (code generation plan)

- [ ] Licensed-country check on `POST /api/partners/links`
- [ ] `EnsureDefaultHqOfficeAsync` + provision hook + MustChangePassword harden
- [ ] Extend tenant PUT (license dates/status)
- [ ] `DashboardModule` + `GetPipelineFunnelQuery`
- [ ] `/admin/partners` page + nav; harden `/partners`
- [ ] Overview `PipelineFunnel`; tenants license edit UI
- [ ] Tests TEST-60–68 + code summary
- [ ] Optional: tenant index on `current_stage_id`; Department TenantId on create
