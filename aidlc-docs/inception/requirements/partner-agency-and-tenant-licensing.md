# Partner Agencies, Agency Levels & Tenant Licensing

**Status**: Accepted domain decision (2026-07-22)  
**Regulatory source**: የኢትዮጵያ የውጭ ሀገር ሥራ ስምሪት አፈጻጸም መመሪያ ቁጥር **1126/2018** (MoLS), esp. Arts. 16–23, 26–27, 40  
**Also see**: `docs/PARTNER_AGENCY_COMPLIANCE.md` (engineering summary; this file is AI-DLC source of truth)  
**Affects units**: Unit 1 (TenantInfo enrichment), Unit 2 (intake field semantics), **Unit 6** (Partners + tenant license UI)

---

## 1. Terminology (corrected)

| AppSheet / ops | Directive | SimbaFlow |
|----------------|-----------|-----------|
| OFFICE (e.g. Etenaa Resources) | ተቀባይ አገር ወኪል / ኤጀንሲ | **Partner agency** |
| DESTINATION | ተቀባይ ሀገር | **Country of travel** |
| Local office / branch | ቅርንጫፍ ቢሮ | **Registering branch** (`office_id`) |
| Our agency | ኤጀንሲ (ላኪ) | **Tenant** |

Candidate intake must not treat AppSheet “Office” as the local branch. Branch = `office_id`; partner = `partner_agency_id` (denormalized name snapshot allowed).

---

## 2. Architecture decision: shared catalog + tenant links

**ADR — Partner ownership**

| Layer | Schema | Who manages | Contents |
|-------|--------|-------------|----------|
| **Partner catalog** | `public` (platform) | SuperAdmin | Master partner: name, country, foreign license id, Art. 40 capacity tier (Low/Med/High), active flag |
| **Partner link (ትስስር)** | Tenant or public with `tenant_id` | Agency Owner | Which catalog partners this agency uses + agreement start/end + status |
| **Intake** | Tenant candidate | Staff | Dropdown of **this tenant’s Active links** only, filtered by destination country |

**Rejected**: Per-tenant private partner lists with no shared identity (breaks Art. 40 cross-tenant caps).  
**Rejected**: SuperAdmin assigns partners for every candidate (agency owns MoUs day-to-day).

```
SuperAdmin  →  Partner catalog (public)
AgencyOwner →  Link partner + agreement (~2y renew)
Staff       →  Candidate.partner_agency_id from Active links
```

---

## 3. Agency levels (ደረጃ 1–5) — Arts. 18–22

Store on **TenantInfo** (platform):

| Level | Placement scope (summary) | Max partners / country | Max licensed countries |
|-------|---------------------------|------------------------|------------------------|
| 1 | All occupations | ≤ 20 | Unlimited |
| 2 | Domestic, labour, semi/skilled | ≤ 20 | ≤ 8 |
| 3 | Domestic + labour | ≤ 16 | ≤ 8 |
| 4 | Domestic + labour | ≤ 8 | ≤ 4 |
| 5 | Domestic only | ≤ 4 | ≤ 2 |

Enforcement at: partner-link create, placement request (Art. 16), candidate partner assignment.

---

## 4. Foreign partner capacity — Art. 40

Catalog field `CapacityTier`:

| Tier | Max Ethiopian agencies linked |
|------|-------------------------------|
| Low | 2 |
| Medium | 4 |
| High | 8 |

(Seafarer-sending agencies excepted.) Enforced when Agency Owner creates a link.

---

## 5. Agreements & license renewal

- **Partner agreement** on the link: `agreement_start`, `agreement_end`, status Active|Expired|Suspended. Ops often renew ~**2 years** (confirm per MoU / Proclamation 1389/2017). Expired → hidden from intake.
- **Agency license** on tenant: license number, issued/expiry, MoLS license status (distinct from SaaS `SubscriptionStatus`), licensed countries[], optional capital/bond amounts.

---

## 6. Tenant provisioning — current vs required

### Implemented today (US-8.07 partial)

- Agency name, slug, contact email/phone  
- Agency Owner account + temp password  
- Schema create + migrate + default workflow seed  
- SaaS `SubscriptionStatus`  

### Missing for compliance (extend US-8.07 / Unit 1+6)

| Field / behavior | Priority |
|------------------|----------|
| Agency level 1–5 | P0 |
| License number + issue/expiry | P0 |
| Licensed destination countries | P0 |
| HQ address/city/country (entity exists, not on create form) | P1 |
| Auto-create default HQ **branch office** on provision | P0 (candidates need `office_id`) |
| Capital + bond amounts | P2 |
| MoLS license status vs SaaS status | P1 |
| Force `MustChangePassword` on new owner | P1 (bug today: false) |
| Partner catalog + links | Unit 6 (not at provision) |

---

## 7. Target entities (Unit 6 + TenantInfo delta)

```
TenantInfo (+ add)
  AgencyLevel : 1..5
  LicenseNumber : string?
  LicenseIssuedAt : DateOnly?
  LicenseExpiresAt : DateOnly?
  LicenseStatus : Active | Suspended | Revoked | Pending
  LicensedCountries : string[]   // ISO or names
  CapitalEtb : decimal?
  BondUsd : decimal?

PartnerAgency (public)
  Name, CountryCode, ForeignLicenseId?, CapacityTier, IsActive, Contact…

PartnerLink
  TenantId, PartnerAgencyId
  AgreementStart, AgreementEnd, Status
  // enforce ትስስር + Art.40

Candidate (+ add when Partners ships)
  PartnerAgencyId : Guid?
  OfficeName : string?   // snapshot; deprecate as sole source
```

---

## 8. Story / FR impact

| ID | Change |
|----|--------|
| FR-01.8 | Country of travel + **Partner agency** (not free-text Office Name as master) |
| FR-08.3 | Split: platform catalog + tenant links + level/Art.40 rules |
| FR-08.7 (new) | Tenant licensing metadata (level, license, countries) |
| US-8.03 | Rewrite: Agency Owner links from catalog; SuperAdmin maintains catalog |
| US-8.03a (new) | SuperAdmin manage partner catalog |
| US-8.07 | Extend provision form with level, license, countries, HQ; seed HQ office |
| Unit 6 | Scope includes PartnerModule (catalog admin + tenant links), Tenant license fields |

**Out of scope for Unit 3** (Embassy/LMIS): keep `officeName` text until Unit 6; labels already say Partner Agency.
