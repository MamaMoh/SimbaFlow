# NFR Requirements — Unit 6: Agency ERP

Inherits Unit 1–5 NFR baselines. Adds targets for partner catalog/links, tenant license edit, HQ office seed, and pipeline funnel. **Out of scope for this unit’s UI NFRs:** staff CRUD, audit trail viewer, full KPI suite (trends/revenue/office comparison).

**FD decisions:** harden brownfield partners; `/admin/partners` vs tenant `/partners`; provision + license edit + HQ seed; funnel v1; **enforce** licensed-country on link create.

## NFR-PERF: Performance Requirements

| ID | Requirement | Target | Context |
|----|-------------|--------|---------|
| PERF-60 | Partner catalog list (admin) | < 300ms p95 | Filter by country/active |
| PERF-61 | Tenant linked partners list | < 250ms p95 | Join PartnerLink + PartnerAgency |
| PERF-62 | Create partner link (with cap checks) | < 500ms p95 | Art. 40 + level + licensed-country queries |
| PERF-63 | Update link status | < 200ms p95 | Single row |
| PERF-64 | Update tenant license | < 400ms p95 | Platform TenantInfo only |
| PERF-65 | Provision + HQ office seed | < 5s p95 | Schema migrate + seeds (existing budget) |
| PERF-66 | Pipeline funnel query | < 400ms p95 | Stage counts for tenant |
| PERF-67 | Intake linked partners (linkedOnly) | < 200ms p95 | Candidate form dropdown |

## NFR-SCALE: Scalability Requirements

| ID | Requirement | Target | Strategy |
|----|-------------|--------|----------|
| SCALE-60 | Partner catalog size | 5,000+ | Indexes CountryCode, IsActive, Name |
| SCALE-61 | PartnerLinks platform-wide | 50,000+ | Unique (TenantId, PartnerAgencyId); index PartnerAgencyId |
| SCALE-62 | Active links per tenant | ≤ level caps | Enforced on create |
| SCALE-63 | Concurrent partner admins | 10+ SuperAdmin / 20+ agency | SWR + disable submit |
| SCALE-64 | Funnel with 100k candidates | < 400ms p95 | Group-by current stage / visibility index |

## NFR-SEC: Security Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| SEC-60 | Catalog create/update SuperAdmin-scoped | `/admin/partners` + `partner.create/update` / `system.admin` |
| SEC-61 | Tenant cannot create catalog duplicates | Hide catalog create on `/partners` for non-SuperAdmin |
| SEC-62 | Link create/status tenant-scoped | TenantId match unless SuperAdmin |
| SEC-63 | Tenant license edit SuperAdmin only | `UpdateTenantLicense` |
| SEC-64 | Intake sees Active links only | Server filter Status + IsActive |
| SEC-65 | Cap checks server-side always | Art. 40 + level + licensed countries — never UI-only |
| SEC-66 | Existing `partner.*` / `office.*` permissions | Reuse PermissionSeeder codes |

## NFR-RES: Resiliency Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| RES-60 | Link create rejects when Art. 40 full | Clear 400 message with tier + counts |
| RES-61 | Link create rejects when level country/partner caps exceeded | Clear 400 with level + caps |
| RES-62 | Link create rejects when country not licensed | When `LicensedCountries` non-empty |
| RES-63 | HQ seed idempotent | Only create if zero offices; safe re-run |
| RES-64 | License edit validates CountriesWithinLimit | Fail before save |
| RES-65 | Deactivating catalog partner does not delete links | Blocks new links; existing links kept |
| RES-66 | Funnel failure does not break Overview | Show PageAlert; rest of page usable |

## NFR-TEST: PBT Requirements (Unit-Specific)

| ID | Requirement | PBT Rule | Implementation |
|----|-------------|----------|----------------|
| TEST-60 | Art. 40 capacity never exceeded | PBT-03 | Active links ≤ tier max |
| TEST-61 | Level partners-per-country never exceeded | PBT-03 | After link create |
| TEST-62 | Level max countries never exceeded | PBT-03 | When capped levels |
| TEST-63 | LicensedCountries gate | PBT-04 | Non-empty list → reject unlicensed country |
| TEST-64 | CountriesWithinLimit on license save | PBT-04 | Invalid country count fails |
| TEST-65 | Intake set ⊆ Active links | PBT-03 | LinkedOnly query invariant |
| TEST-66 | Unique (TenantId, PartnerAgencyId) | PBT-03 | Duplicate link → 409 |
| TEST-67 | AgreementEnd ≥ AgreementStart | PBT-04 | Validator |
| TEST-68 | HQ seed creates exactly one when empty | PBT-03 | Idempotent |

## NFR-USAB: Usability Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| USAB-60 | Separate admin catalog vs tenant link UX | `/admin/partners` vs `/partners` |
| USAB-61 | Show Art. 40 tier + max agencies on catalog | Badge / column |
| USAB-62 | Show agency level caps when linking | Helper text from `AgencyLevelRules.Describe` |
| USAB-63 | Cap rejection messages actionable | Include current vs max |
| USAB-64 | Funnel clickable or linked to boards | Stage → candidates/workflow |
| USAB-65 | License edit shows MoLS vs SaaS status separately | Two fields labeled |
| USAB-66 | Deferred staff/audit clearly not claiming complete | No fake “done” stubs |

## Tech Stack Additions (Unit 6-Specific)

| Package | Purpose |
|---------|---------|
| (None required) | Reuses Carter, EF Core, Identity, SWR, existing partner/tenant APIs |
| Platform migration | Only if license-edit / HQ seed needs schema deltas (mostly already migrated) |
| Tenant migration | HQ office is data seed, not necessarily new tables |

## Testable Properties Summary

1. Art. 40 + agency level caps on every link create  
2. Licensed-country enforcement when list non-empty  
3. Intake options ⊆ Active tenant links  
4. Idempotent HQ office seed  
5. License country count vs level  
