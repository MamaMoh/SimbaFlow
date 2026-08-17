# Domain Entities — Unit 6: Agency ERP

## Design posture (approved)

| Topic | Choice |
|-------|--------|
| Scope | **Phased — Partners + licensing first** + pipeline funnel v1; defer staff CRUD + audit viewer |
| Partners | **Document + harden** existing `PartnerAgency` / `PartnerLink` / `PartnerModule` — no rewrite |
| License | **Provision + edit** SuperAdmin; auto-create **HQ branch office** on provision |
| Catalog UX | Dedicated **`/admin/partners`** for SuperAdmin; tenant `/partners` = link-only |
| Dashboard | **Pipeline funnel v1** on Overview |
| Staff / Audit | **Deferred** (follow-up batch or Unit 8) |

**Stories in scope (v1):** US-8.02 (done/harden), US-8.03, US-8.03a, US-8.06 (funnel subset), US-8.07 (extend).  
**Deferred:** US-8.01 (staff), US-8.05 (audit), US-8.04 (roles — already Unit 1), full US-8.06 KPI suite.

---

## Existing: PartnerAgency (public / platform)

```
PartnerAgency : BaseEntity (public schema)
├── Name : string
├── CountryCode : string          // ISO / short code
├── CountryName : string
├── ForeignLicenseId : string?
├── CapacityTier : PartnerCapacityTier  // Low | Medium | High (Art. 40)
├── ContactEmail, ContactPhone, Address : string?
├── IsActive : bool
└── Notes : string?
```

**Ownership:** SuperAdmin / `partner.create|update` / `system.admin`.  
**Art. 40:** Low→2, Medium→4, High→8 max Active Ethiopian agency links.

---

## Existing: PartnerLink (public, with TenantId)

```
PartnerLink : BaseEntity (public schema)
├── TenantId : Guid
├── PartnerAgencyId : Guid
├── AgreementStart : DateOnly
├── AgreementEnd : DateOnly
└── Status : PartnerLinkStatus  // Active | Expired | Suspended
```

**Ownership:** Agency Owner (tenant-scoped) creates links; SuperAdmin may create for any tenant.  
**Unique:** `(TenantId, PartnerAgencyId)` when not deleted.

---

## Existing: TenantInfo licensing fields (public)

```
TenantInfo (+ MoLS)
├── AgencyLevel : int          // 1–5
├── LicenseNumber : string?
├── LicenseIssuedAt / LicenseExpiresAt : DateOnly?
├── LicenseStatus : AgencyLicenseStatus  // Active | Suspended | Revoked | Pending
├── LicensedCountries : string[]
├── CapitalEtb, BondUsd : decimal?       // stored; UI edit deferred (not Q3=C)
├── Address, City, Country               // HQ
└── SubscriptionStatus                   // SaaS — independent of LicenseStatus
```

---

## Existing: Office / Department (tenant)

Registering branch (`office_id` on Candidate). Offices CRUD already exists.

**Unit 6 delta:** On provision, auto-create default **HQ** office in tenant schema if none exists.

---

## Candidate (already extended)

```
Candidate
├── PartnerAgencyId : Guid?     // from Active PartnerLink
├── OfficeName : string?        // snapshot of partner name
└── OfficeId : Guid             // registering branch
```

---

## No new aggregates for Unit 6 v1

StaffProfile / Audit viewer / Scheduled KPI entities are **out of scope** for this phase.

---

## Caps reference (AgencyLevelRules)

| Level | Max partners / country | Max licensed countries |
|-------|------------------------|------------------------|
| 1 | 20 | Unlimited |
| 2 | 20 | 8 |
| 3 | 16 | 8 |
| 4 | 8 | 4 |
| 5 | 4 | 2 |
