# UI Standardization, Simpler Forms & Configurable Flow (post-Unit 8)

Delivered in 5 phases. Users are largely non-technical agency/field staff, so the goals were:
one consistent look on every page, short forms, per-agency configurable step ownership, and
verification by logging in as every role.

## Phase 0 — Design-system foundation
- `frontend/lib/ui/status.ts` — **the** source of truth: every status → a semantic tone
  (`success | warning | danger | info | progress | neutral`) + domain helpers
  (`commissionTone`, `exceptionTone`, `tenantTone`, `remainingDays`, `statusTone`).
- `components/ui/status-badge.tsx` — the single status chip used everywhere.
- `components/ui/page-header.tsx` — title + description + right-aligned actions.
- `components/ui/form-kit.tsx` — `FormSection` (collapsible), `Field` (required/help/error), `FormActions`.
- `frontend/docs/ui-standards.md` — the written standard.

## Phase 1 — Standards rolled across ALL pages
- The 3 legacy badge components + the board `TrackChip` now delegate to `StatusBadge`/`status.ts`,
  so **all** status colors are unified with zero per-page duplication.
- **Padding bug fixed:** 24 pages had `p-6` inside an already-padded `<main>` → content started at
  different offsets. Removed; every page now uses `flex flex-col gap-6`.
- Title typography normalized; **21 pages converted to `<PageHeader>`**.
- All raw `<table>` markup converted to the shared table primitives (finance/accounting,
  journals detail, commission detail, report view, workflow config, fee editor).
- **Empty states:** the shared DataTable now renders column headers plus a friendly in-table
  empty message (`emptyMessage` prop). Redundant "no data" alert cards removed from every list —
  an empty page is now a real table saying what will appear there.

## Phase 2 — Simpler registration
The candidate form is a 5-step stepper but only 5 fields are truly required. Added a **"Save now"**
action that appears as soon as name/passport/DOB/gender are filled, so staff can register in
seconds and add details later; optional steps are labelled "Optional".
Fixed a real bug: the form's `onSubmit` converted every submit into "next step", so a mid-flow save
silently did nothing — "Save now" now calls the submit handler directly (verified HTTP 201).

## Phase 3 — Workflow step builder (per-agency flow)
The engine already stored `AllowedRoles` per transition; it just wasn't editable.
- **New API:** `PUT/DELETE /api/workflow/config/transitions/{id}`, `DELETE /api/workflow/config/stages/{id}`
  (stage delete refuses if candidates still occupy it, and cascades to its transitions).
- **New UI:** `components/workflow/transition-editor.tsx` — tick which of the 8 roles may perform a
  step (none ticked = anyone with access); transitions table gained a "Who can do it" column plus
  Edit/Remove, and stages gained Remove.
- **UX fix in the engine:** steps blocked purely by role are now *hidden* instead of shown disabled
  ("Insufficient role"), since a role block is never actionable by that user. Condition/field blocks
  stay visible-but-disabled because the user can still satisfy them.
- Result: Agency A can give register+contract to one role; Agency B can split them across two.

## Phase 4 — Multi-account role testing
Created a user per role in the Demo tenant (`rt.*` / `Role@123!`) via the app's own API — which also
re-confirmed the earlier security fixes (all landed in the caller's tenant, `IsSuperAdmin=false`).
- **Permission matrix** verified across 9 endpoints × 7 roles (e.g. FieldAgent: candidates only;
  FinanceOfficer: commissions/accounting/reports; `tenants` 403 for all; `users` 403 for all).
- **Step gating proven live:** restricting a step to OfficeManager removed it from FieldAgent's and
  EmbassyOfficer's action lists, and execution by a non-permitted role returns 400.
- New low-privilege roles **cannot** modify workflow config (403 on all 3 new endpoints).

## Verification
- Backend **146/146** (added `WorkflowRoleGatingTests`, `UserAccessGuardTests`).
- Frontend `tsc --noEmit` clean.
- Playwright **47/47**, including new `role-access.spec.ts` (7 roles) and `quick-registration.spec.ts`.

## Notes / follow-ups
- Form labels in the candidate form lack `htmlFor`/`id` association (clicking a label doesn't focus
  the field; affects screen readers). Worth a mechanical pass.
- `rt.*` role users are left seeded in the Demo tenant for future manual/role testing.
