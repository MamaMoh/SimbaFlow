# Unit 6 Code Gen — Batch 1 Approval

**Batch 1** (Steps 1–4) complete. API build succeeded.

## Delivered

| Step | What |
|------|------|
| 1 | Licensed-country gate on `POST /api/partners/links` (when `LicensedCountries` non-empty) |
| 2 | `IHqOfficeSeedService` / `HqOfficeSeedService` — idempotent HQ `Department`; hooked into `ProvisionTenantHandler` (`MustChangePassword` already true) |
| 3 | Tenant PUT extended: `LicenseIssuedAt`, `LicenseExpiresAt`, `LicenseStatus` / `LicenseStatusName` |
| 4 | `CreateDepartment` sets `TenantId` from current user; code uniqueness scoped per tenant |

## Rules enforced

- Empty licensed list → no country gate (level/Art. 40 still apply)
- HQ seed only when tenant has zero offices; Code=`HQ`
- License expiry ≥ issued date

## Question 1
Approve Batch 1 and continue?

A) **Approve** — start Batch 2 (Steps 5–6: dashboard funnel API)

B) **Approve** — pause (manual QA / review first)

C) **Request changes** (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]: A
