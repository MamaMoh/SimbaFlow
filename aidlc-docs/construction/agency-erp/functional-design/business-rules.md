# Business Rules — Unit 6: Agency ERP

## Partner catalog

| ID | Rule |
|----|------|
| BR-P01 | Catalog partners live in **public** schema; tenants never own master rows |
| BR-P02 | Create/update catalog requires SuperAdmin or `partner.create` / `partner.update` / `system.admin` |
| BR-P03 | Inactive (`IsActive=false`) partners cannot receive **new** links; hidden from intake |
| BR-P04 | CapacityTier drives Art. 40 max Active Ethiopian agency links: Low=2, Med=4, High=8 |

## Partner links (ትስስር)

| ID | Rule |
|----|------|
| BR-L01 | Unique active/non-deleted link per `(TenantId, PartnerAgencyId)` |
| BR-L02 | Creating a link enforces Art. 40 capacity on the partner |
| BR-L03 | Creating a link enforces agency level max partners **per country** |
| BR-L04 | Creating a link enforces agency level max **destination countries** (when capped) |
| BR-L05 | AgreementEnd ≥ AgreementStart; default term ~2 years if end omitted |
| BR-L06 | Intake dropdown = tenant’s **Active** links only (and catalog partner Active) |
| BR-L07 | Expired / Suspended links excluded from intake |
| BR-L08 | When `LicensedCountries` is non-empty, reject linking a partner whose country is not licensed (recommended harden) |

## Tenant licensing

| ID | Rule |
|----|------|
| BR-T01 | AgencyLevel ∈ [1, 5] |
| BR-T02 | Count of LicensedCountries must satisfy `AgencyLevelRules.CountriesWithinLimit` |
| BR-T03 | `LicenseStatus` (MoLS) is independent of SaaS `SubscriptionStatus` |
| BR-T04 | SuperAdmin may edit license fields after provision |
| BR-T05 | Provision auto-creates HQ branch office when tenant has zero offices |
| BR-T06 | New Agency Owner should have `MustChangePassword = true` |

## Dashboard

| ID | Rule |
|----|------|
| BR-D01 | Funnel counts are tenant-scoped (and office-scoped if user is office-limited — v1: tenant-wide for owners) |
| BR-D02 | Stage list comes from workflow definition; counts from candidate visibility / current stage |

## Offices

| ID | Rule |
|----|------|
| BR-O01 | Candidates require a registering branch (`OfficeId`); HQ seed ensures at least one office exists after provision |

## Permissions (indicative)

| Permission | Use |
|------------|-----|
| `partner.read` | List catalog / links / intake options |
| `partner.create` | Catalog create (admin) + create link |
| `partner.update` | Catalog update + link status |
| `office.read` / `office.write` | Offices (existing) |
| `system.admin` | SuperAdmin bypass; tenant license edit; `/admin/partners` |
| `candidate.read` | Dashboard funnel |

## Deferred rules (not enforced in Unit 6 v1)

- Staff deactivate / office-scope for Office Manager
- Audit export / recursive-audit prevention UI
- Capital/bond validation thresholds
