# NFR Design — Unit 6: Agency ERP

Builds on Unit 1–5 baselines and brownfield `PartnerModule` / `TenantModule`. Specifies how partner catalog/links, tenant license edit, HQ office seed, and pipeline funnel meet Unit 6 NFRs.

---

## 1. Module architecture (brownfield)

```
PartnerModule (Carter, public schema)     ← harden in place
TenantModule (provision + PUT license)    ← extend HQ seed + license dates/status
OfficeModule / Departments                ← HQ seed target
DashboardModule (new, minimal)            ← pipeline funnel query
        │
        ▼
PlatformDbContext + TenantDbContext
AgencyLevelRules (domain)
```

No MediatR rewrite for partners in Unit 6 v1.

---

## 2. Partner API design

### Existing endpoint map (keep)

| Method | Path | Role |
|--------|------|------|
| GET | `/api/partners` | Catalog list OR `?linkedOnly=true` for intake |
| GET | `/api/partners/{id}` | Catalog detail + ActiveLinks count |
| POST | `/api/partners` | Create catalog (SuperAdmin / partner.create) |
| PUT | `/api/partners/{id}` | Update catalog |
| GET | `/api/partners/links/mine` | Tenant’s links |
| POST | `/api/partners/links` | Create link (cap checks) |
| PUT | `/api/partners/links/{id}/status` | Active / Expired / Suspended |

### Unit 6 hardening (gap-fill)

**POST `/api/partners/links`** — add after Art. 40 + level checks:

```csharp
if (tenant.LicensedCountries.Count > 0) {
  var partnerCountry = partner.CountryCode; // or CountryName match
  if (!tenant.LicensedCountries.Any(c =>
        c.Equals(partnerCountry, OrdinalIgnoreCase)
        || c.Equals(partner.CountryName, OrdinalIgnoreCase)))
    return 400 "Partner country is not in agency licensed countries";
}
```

**Performance budget (PERF-62):** cap queries use indexed counts on `PartnerLinks` + `PartnerAgencyId`; target &lt;500ms p95.

---

## 3. Tenant license & provision

### Existing

- `ProvisionTenantCommand` — level, license #, countries, schema migrate
- `PUT /api/tenants/{id}` — partial license edit (AgencyLevel, LicensedCountries, LicenseNumber, address)

### Unit 6 extensions

```csharp
// Extend UpdateTenantRequest + handler
LicenseIssuedAt, LicenseExpiresAt, LicenseStatus (MoLS)

// ProvisionTenantHandler — after schema migrate
await EnsureDefaultHqOfficeAsync(connectionString, schemaName, tenant, ct);
  if (await CountOffices(schema) == 0)
    insert Department { Name = "Head Office", IsActive = true,
      Address/City/Country from tenant HQ fields }

// Harden owner
MustChangePassword = true; IsFirstLogin = true;
```

**RES-63:** HQ seed idempotent — only when office count = 0.

**PERF-65:** Provision budget unchanged (&lt;5s p95 including migrate).

---

## 4. Pipeline funnel (PERF-66)

New minimal module or route on existing dashboard:

| Method | Path | Query |
|--------|------|-------|
| GET | `/api/dashboard/pipeline-funnel` | Stage counts for current tenant |

```csharp
// Pseudocode
var stages = await GetWorkflowStages(tenantId);
var counts = await Candidates.AsNoTracking()
  .Where(c => !c.IsDeleted && c.Status == Active)
  .GroupBy(c => c.CurrentStageId)  // or visibility resolver
  .Select(g => new { StageId = g.Key, Count = g.Count() })
  .ToListAsync();
return stages.OrderBy(s => s.SortOrder)
  .Select(s => new { s.Id, s.Name, s.SortOrder,
    Count = counts.FirstOrDefault(c => c.StageId == s.Id)?.Count ?? 0 });
```

Index: candidate `(Status, CurrentStageId)` or equivalent visibility column.

**RES-66:** Overview renders funnel inside try/catch; API failure → `PageAlert` only on funnel card.

---

## 5. Frontend routes

| Route | Audience | NFR |
|-------|----------|-----|
| `/admin/partners` | SuperAdmin | USAB-60; catalog CRUD |
| `/partners` | Agency Owner | Link-only; hide catalog create |
| `/admin/tenants` | SuperAdmin | License edit sheet (extend PUT) |
| `/overview` | Authenticated | `PipelineFunnel` component |

Nav: add `/admin/partners` under Platform Admin section.

---

## 6. Authorization (SEC-60–66)

| Surface | Permission |
|---------|------------|
| `/admin/partners` CRUD | `system.admin` or `partner.create/update` |
| Tenant `/partners` link | `partner.create` / `partner.update` |
| Intake dropdown | `partner.read` (linkedOnly) |
| Tenant license edit | SuperAdmin policy on `/api/tenants/*` |
| Funnel | `candidate.read` or dashboard claim |

All cap checks **server-side** (SEC-65) — UI shows caps for UX only.

---

## 7. Persistence & indexing (existing + verify)

| Table | Indexes (verify in migration) |
|-------|--------------------------------|
| `PartnerAgencies` | CountryCode, IsActive, Name |
| `PartnerLinks` | UX (TenantId, PartnerAgencyId); IX PartnerAgencyId |
| `Tenants` | Slug unique |
| `Departments` (tenant) | — (HQ seed insert) |

No new platform tables expected for Unit 6 v1.

---

## 8. PBT architecture (TEST-60–68)

| Property | Assert |
|----------|--------|
| Art40NeverExceeded | Active links per partner ≤ tier max |
| LevelPartnersPerCountry | Links in country ≤ GetCaps(level).MaxPartnersPerCountry |
| LevelMaxCountries | Distinct countries ≤ cap when level capped |
| LicensedCountryGate | Non-empty LicensedCountries → link country must match |
| CountriesWithinLimit | License save rejects over-count |
| IntakeSubsetActiveLinks | linkedOnly query returns only Active + active catalog |
| UniqueTenantPartner | Duplicate link → 409 |
| AgreementDatesValid | End ≥ Start |
| HqSeedIdempotent | Second seed call → office count unchanged |

Example-based tests in `AgencyErpServiceTests.cs`; properties in `AgencyErpProperties.cs`.

---

## 9. Error & observability

- Link rejections return structured 400 with `{ error, tier?, current?, max? }` where helpful (USAB-63)
- Log `TenantId`, `PartnerAgencyId`, `UserId` — not agreement PII beyond ids
- Optional metric: link_rejected_total{reason=art40|level|country}

---

## 10. Out of scope (Unit 6 NFR Design)

- Staff CRUD (US-8.01)
- Audit trail viewer (US-8.05)
- Full KPI dashboard (trends, revenue, SignalR live funnel)
- MediatR refactor of PartnerModule
- Capital/bond UI workflow
