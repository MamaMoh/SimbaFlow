# Unit 8 — Reporting & Analytics + ERP Enhancements (Functional Design)

## Purpose

Deliver the final AI-DLC unit — reporting & analytics — and layer on a small set of
high-value, low-complexity ERP enhancements that make daily agency operation faster and
clearer: a command-center dashboard, compliance/expiry alerts, a global command palette,
and a per-user task list.

## Scope decisions (confirmed with user)

- **ERP scope:** all four enhancements (command center, compliance alerts, ⌘K palette, my-work).
- **Reporting depth:** on-demand report pages **with Excel + PDF export**. **No** scheduled
  or emailed reports (avoids SMTP + background-job complexity).
- **Design principle:** everything is **derived from existing tables** — **no new DB entity
  and no EF migration**. Persisted follow-up reminders are a deliberate future add-on.

## Stories covered

- **US-10.01–US-10.05** — pipeline, performance, office comparison, overdue detection, financial summary.
- **US-10.06** — Excel/PDF export.
- **US-8.06 (partial)** — KPI dashboard.
- Enhancements (beyond original story set): compliance expiry center, global quick-search, my-work.
- **Deferred (US-10.07):** scheduled reports + email delivery.

## Reports (each self-describing `ReportTable`: columns + rows + optional chart hints)

| Key | Report | Source |
|-----|--------|--------|
| `pipeline` | Active candidates per stage + share | Candidates.CurrentStageId vs active WorkflowDefinition |
| `agency-performance` | Avg days in current stage per stage | Candidates.StageEnteredAt |
| `office-comparison` | Candidates + commission owed per office | Candidates.OfficeName, Commissions.BalanceAmount |
| `overdue` | Candidates stuck > 14 days in a stage | Candidates.StageEnteredAt |
| `financial-summary` | Fees / collected / outstanding | Commissions totals |

Export: `GET /api/reports/{key}/export?format=excel|pdf` → ClosedXML / QuestPDF, gated on `report.export`.

## Command center (`/overview`)

KPI tiles (`/api/dashboard/metrics`): active candidates, new this month, commission owed,
overdue candidates, open exceptions. Trend chart (`/api/dashboard/trends`): registrations,
commissions, exceptions over 12 months. Plus existing pipeline funnel and an alerts strip
linking to Compliance and My Work.

## Compliance (`/compliance`, `GET /api/compliance/alerts`)

Buckets (Expired / ≤30 / ≤90 days) from `Candidate.PassportExpiryDate` and Tasheer status
(`CurrentStatusValues.tasheer == "Expired"`). Tenant-license expiry deferred (cross-context).

## My Work (`/my-work`, `GET /api/tasks/mine`)

Derived action list per tenant: overdue candidates, passports expiring ≤30 days, open
exception cases — with severity and deep links to the candidate.

## Global command palette (⌘K)

`cmdk`-based dialog mounted in the main layout; header search button + ⌘K/Ctrl+K shortcut.
Sources: permission-filtered nav destinations, "Register candidate" quick action, and
debounced candidate search (reuses `GET /api/candidates?search=`).

## Permissions

Reuses existing seeded codes — no new permissions. `report.view` (reports + catalog),
`report.export` (exports), `candidate.read` (dashboard, compliance, my-work).
