# UI Completeness Scope — Clarification

Your answer: finish **all** UI so nothing is nav-only; every page must work with correct success/error messaging and match existing page standards.

## Current audit (nav → reality)

| Nav item | Route | Status |
|----------|-------|--------|
| Dashboard | `/overview` | Page exists (likely thin) |
| Candidates | `/candidates`, `/candidates/[id]` | Real (list/detail/register) |
| Workflow stages | `/workflow/*` | Real stage boards |
| Workflow Config | `/admin/workflow` | **MISSING** (Step 18 deferred) |
| Staff | `/staff` | Page exists |
| Roles | `/roles` | Page exists |
| Tenants | `/tenants` | Page exists |
| Departments | `/departments` | Page exists (nav says **Offices** → `/offices` — **route mismatch / missing**) |
| Partners | `/partners` | **MISSING** |
| Accounting | `/finance/accounting` | **MISSING** |
| Reports | `/reports` | Page exists (likely placeholder) |
| Settings | `/settings` | Page exists (likely thin) |

Unit plan note: Finance / Partners / full Reports belong to **later units** (Unit 3+). Building full ERP UI now without those backends would be fake screens.

## Question 1
What should “all UI done” cover right now?

A) **Unit 2 + shell polish only** — finish Workflow Config (Step 18), fix Offices→departments nav/route, harden Candidates/Workflow/Staff/Roles/Tenants with consistent success/error toasts and access-denied patterns; leave Finance/Partners as “coming in later unit” pages with a proper empty-state (not broken links)

B) **Every nav link is a full working app page now** — also build Offices, Partners, Accounting, Reports end-to-end (implies pulling Unit 3–7 scope forward; needs backend APIs for those modules)

C) **Hide unfinished nav** — remove/hide Finance/Partners/etc. until their units ship; finish all remaining real Unit 1–2 pages (including Workflow Config) to production standard

D) Other (please describe after [Answer]: tag below)

[Answer]: D all

## Question 2
For success/error messaging standard, which pattern should all pages follow?

A) **sonner toasts** (same as Candidates list today) — success on mutate, error with API message; inline field errors via Zod/RHF where forms exist

B) **Page-level Alert banners** + toasts for transient actions

C) Other (please describe after [Answer]: tag below)

[Answer]: C all type
