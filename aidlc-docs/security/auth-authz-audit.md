# SaaS Authentication & Authorization Audit

**Date:** 2026-08-17 · **Scope:** multi-tenant auth, tenant isolation, user management
**Method:** static review + live black-box testing against the running stack (5 tenants + platform admin)

## Summary

Authentication primitives were sound, but **multi-tenant isolation and the user-management
privilege boundary were broken** — the highest-severity class of SaaS defect. Three critical
issues let any authenticated agency user reach other tenants' data and escalate to platform
admin. All were reproduced live, fixed, and re-tested to confirm closure. Backend suite: 141/141.

## What was already correct ✅
- Passwords hashed via ASP.NET Identity; strong password policy (length + upper/lower/digit/special).
- Wrong password / no token / tampered token → **401**; signature validation enforced.
- **Account lockout** works (repeated failures → HTTP 423).
- Short-lived access tokens (**15 min**) + refresh tokens; JWT carries `tenant_id`.
- Permission model (`IRequirePermission` + `AuthorizationBehavior`) enforced where declared
  (e.g. agency owner → `/api/tenants` = **403**).
- User **list** query was tenant-scoped for non-SuperAdmins.

## Critical findings (all FIXED)

### C1 — Cross-tenant data breach via forged `X-Tenant-Id` header
`CurrentUserService.TenantId` read the client `X-Tenant-Id` header **before** the JWT claim with
no SuperAdmin check. The `TenantConnectionInterceptor` uses that value to set the Postgres
`search_path`, so any authenticated user could read/write another agency's schema.
- **Proof:** Ethio Star owner (own tenant empty) + `X-Tenant-Id: <Demo>` → returned Demo Agency's
  9 candidates with full PII (names + passport numbers).
- **Fix:** header honored only when `IsSuperAdmin`; everyone else is pinned to their JWT tenant.
  `CurrentUserService.cs`.

### C2 — Privilege escalation to platform SuperAdmin
`CreateUserCommand` / `UpdateUserCommand` set `IsSuperAdmin` straight from the request body.
- **Proof:** Ethio Star owner created a user with `isSuperAdmin:true` → DB row `IsSuperAdmin=t`.
- **Fix:** only a SuperAdmin may set the flag; tenant admins are forced to `false`.

### C3 — Cross-tenant user management (create / read / modify / delete)
`CreateUserCommand` accepted an arbitrary `TenantId`; every user query/mutation
(`GetUserById`, `GetUserRoles`, `Update`, `ResetPassword`, `ToggleStatus`, `Delete`,
`AssignRoles`, `ForceLogout`) loaded the target by ID with **no tenant check** — enabling
cross-tenant account takeover (reset another agency's admin password, disable/delete them, etc.).
- **Proof:** Ethio Star owner created a user assigned to the Demo tenant, and enumerated Demo's users via header.
- **Fix:** new `UserAccessGuard.CanManage(caller, target)` — non-SuperAdmins may only act on
  users in their own tenant and never on a SuperAdmin; applied to all user handlers. Create forces
  the caller's own `TenantId`.

## Re-test after fix (same attacks, now blocked)
| Attack | Before | After |
|--------|--------|-------|
| ethiostar + `X-Tenant-Id=Demo` → candidates | 9 (breach) | 0 (own tenant) |
| create user `isSuperAdmin:true` | became SuperAdmin | forced `IsSuperAdmin=false` |
| create user `tenantId=Demo` | landed in Demo | forced into own tenant |
| list users + `X-Tenant-Id=Demo` | Demo's users | own tenant only |
| SuperAdmin + `X-Tenant-Id=Demo` (legit) | 9 | **9 (still works)** |

Regression guard: `tests/.../Behaviors/UserAccessGuardTests.cs` (6 tests).

## MFA hardening (DONE — 2026-08-17)

The prior MFA implementation had three problems, all fixed and live-tested:

- **M1 — TOTP-only login (2FA collapsed to 1FA).** `VerifyMfa` (`/api/auth/login/mfa`) took only
  username + code, no password, so a TOTP code alone yielded a session. **Fix:** verify-mfa now
  re-checks the password and only issues tokens when both factors pass.
- **M2 — No brute-force protection on the code.** Invalid codes didn't count toward lockout.
  **Fix:** lockout is checked and `AccessFailedAsync` is called on bad password/code.
- **M3 — Enrollment was impossible.** `/mfa/setup` generated a key but nothing set
  `TwoFactorEnabled`. **Fix:** new `/api/auth/mfa/enable` (confirm first TOTP → turn on) and
  `/api/auth/mfa/disable` (password-confirmed).

**Enforcement (configurable, default OFF):** `Mfa` config section — `Enforce`, `RequiredRoles`
(default `AgencyOwner`), `RequireForSuperAdmin`. When on, a privileged user without MFA gets only
a **limited enrollment token** at login (`RequiresMfaSetup=true`, no refresh token) that carries no
permissions/roles/tenant — so business endpoints return 403 while only the MFA enrollment endpoints
work, until they enroll. Default is OFF so the current client keeps working; turn it on after the UI
supports the `RequiresMfaSetup` flow.

Live-tested end-to-end: enforce→setup-token (business 403 / setup 200)→enroll with real TOTP→
re-login challenges→wrong-password+valid-code = 401 (binding)→correct password+code = full tokens→
disable. Default-off login unchanged. Backend 141/141; login/reporting e2e green.

## Remaining follow-ups (not yet done)
- **`X-Current-Location` header** is still honored for any user (office-scoping within a tenant);
  lower severity than C1 but worth gating similarly for office-level isolation.
- **Client enrollment UI** for the `RequiresMfaSetup` flow, so enforcement can be switched on.
- **Refresh-token rotation/reuse detection** and **audit logging of admin user actions**.
- Consider **removing `IsSuperAdmin` and `TenantId` from the Create/Update DTOs** entirely (defense
  in depth on top of the handler checks).

## C4 — Cross-tenant session leak in the Next.js proxy (FIXED 2026-08-17)

Found while testing the Telegram bot: an e2e test intermittently saw the **wrong tenant's** data.

`frontend/lib/api/session-cache.ts` memoized the session in a **module-level global with a 5-second
TTL and no key**. The API proxy (`app/api/proxy/[...path]/route.ts`) reads
`session.user.accessToken` from it and forwards it as the backend Bearer token. So any user hitting
the server within 5 seconds of another was served the **first user's session**, and their requests
executed as that user against that user's tenant.

- **Impact:** cross-tenant data exposure between concurrent users (not just a test flake).
- **Repro observed:** the partners spec (Demo Agency) ran shortly after specs logging in as Ethio
  Star / platform admin and received the other tenant's empty partner list — failing 2 of 3
  full-suite runs while passing 3/3 standalone.
- **Fix:** removed the cross-request cache. The session is now resolved per request, memoized only
  *within* a request via React `cache()`. `getServerSession` verifies a signed cookie with no DB
  round-trip, so the original performance argument did not justify the risk.
- **Verified:** full Playwright suite **49/49 three consecutive runs** (previously flaky).

**Lesson for review:** the earlier audit covered backend authorization thoroughly but this leak lived
in the frontend BFF/proxy layer. Any server-side cache that holds identity must be keyed by identity.
