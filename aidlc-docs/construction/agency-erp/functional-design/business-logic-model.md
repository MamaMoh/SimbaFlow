# Business Logic Model — Unit 6: Agency ERP

## Architecture (brownfield)

```
PartnerModule (Carter, public schema)     ← harden, keep inline handlers (approved)
TenantModule (provision + license edit)
OfficeModule / Departments                ← HQ seed on provision
Dashboard / Overview                      ← pipeline funnel query
        │
        ▼
PlatformDbContext: PartnerAgencies, PartnerLinks, Tenants
TenantDbContext: Offices (Departments), Candidates
AgencyLevelRules (domain service)
```

No MediatR rewrite for partners in Unit 6 v1.

---

## BL-P01: Partner catalog CRUD (US-8.03a)

```
CreatePartnerAgency(name, countryCode, countryName?, capacityTier, …)
  1. Assert SuperAdmin or partner.create / system.admin
  2. Validate Name + CountryCode required
  3. Insert PartnerAgency (IsActive=true)
  4. Return Id

UpdatePartnerAgency(id, …)
  1. Assert partner.update / SuperAdmin
  2. Patch fields; IsActive=false → no new links; hide from intake
```

**UI:** `/admin/partners` (SuperAdmin). Tenant users do **not** create catalog rows.

---

## BL-P02: Tenant partner links (US-8.03)

```
CreatePartnerLink(partnerAgencyId, agreementStart?, agreementEnd?, tenantId?)
  1. Resolve tenantId (body or current user)
  2. Load PartnerAgency (must exist + IsActive)
  3. Load TenantInfo
  4. Reject if link already exists (unique)
  5. Art. 40: count Active links for partner ≥ tier cap → reject
  6. Level caps:
       - Active links in same CountryCode ≥ MaxPartnersPerCountry → reject
       - If MaxCountries set and partner country is new and country count ≥ cap → reject
  7. Optional: reject if partner.CountryCode not in Tenant.LicensedCountries (when list non-empty)
  8. Default agreement: start=today, end=start+2y
  9. Insert PartnerLink Status=Active

UpdateLinkStatus(linkId, status)
  1. Tenant must own link (or SuperAdmin)
  2. Set Active | Expired | Suspended
  3. Suspended/Expired → excluded from intake dropdown
```

**List for intake:** `GET /api/partners?linkedOnly=true` — Active links + Active catalog partners only.

---

## BL-T01: Provision tenant with license + HQ (US-8.07)

```
ProvisionTenantCommand(…, agencyLevel, license*, licensedCountries[], …)
  1. Validate level 1–5; CountriesWithinLimit(level, countries.Count)
  2. Create TenantInfo + schema migrate + workflow/finance seed (existing)
  3. Create AgencyOwner user (MustChangePassword=true — harden if still false)
  4. NEW: EnsureDefaultHqOffice(schema)
       - If tenant has zero offices → create "Head Office" / HQ with Address/City/Country from tenant
  5. Return tenant Id
```

---

## BL-T02: Edit tenant license (post-provision)

```
UpdateTenantLicenseCommand(tenantId, agencyLevel?, licenseNumber?, dates?, licenseStatus?, licensedCountries?, address?)
  1. SuperAdmin only
  2. Validate CountriesWithinLimit for new level/countries
  3. Warn (or soft-fail) if existing Active PartnerLinks would violate new caps
       - v1: allow save but block future links that exceed; optional report of over-cap links
  4. Persist; independent of SubscriptionStatus
```

---

## BL-D01: Pipeline funnel v1 (US-8.06 / US-10.01 subset)

```
GetPipelineFunnelQuery()
  1. Load workflow stages (definition)
  2. Count candidates currently visible per stage (tenant-scoped)
  3. Return [{ stageId, stageName, sortOrder, count }]
  4. Overview renders funnel / horizontal bars; click → candidates filtered by stage (if filter exists)
```

**Out of scope v1:** monthly trends, revenue KPIs, SignalR live refresh (nice-to-have mutate on focus), office comparison.

---

## BL-O01: Offices

Existing CRUD remains. No structural change beyond HQ seed.

---

## Deferred (not in Unit 6 v1 BL)

- Staff CRUD (US-8.01)
- Audit trail viewer (US-8.05)
- Full dashboard KPIs (departures this month, revenue, trends)
- Capital/bond UI + license status workflow engine
