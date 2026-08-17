# Unit 8 Code Gen — Reporting & Analytics + ERP Enhancements (summary)

## Scope

Final unit: on-demand reports (Excel/PDF), command-center dashboard, compliance/expiry
alerts, global ⌘K palette, my-work task list. **Derived from existing tables — no new
entity, no EF migration.** Deferred: scheduled/emailed reports, persisted reminders.

## What was implemented

### Backend (new dependency: `ClosedXML` 0.104.2; `QuestPDF` already present)
- **Shared model** — `ReportTable`/`ReportColumn`/`ReportCatalogItem` (`Application/Common/Models/ReportTable.cs`)
- **Export service** — `IReportExportService` + `ReportExportService` (ClosedXML + QuestPDF), DI-registered
- **Reports feature** — `Features/Reports/ReportModule.cs` + `Queries/ReportQueries.cs`
  - `GET /api/reports` (catalog), `GET /api/reports/{key}` (data), `GET /api/reports/{key}/export?format=`
  - 5 reports: pipeline, agency-performance, office-comparison, overdue, financial-summary
- **Dashboard analytics** — extended `DashboardModule`: `GET /api/dashboard/metrics`, `GET /api/dashboard/trends`
- **Compliance** — `Features/Compliance/ComplianceModule.cs` + `GET /api/compliance/alerts` (passport + Tasheer buckets)
- **My Work** — `Features/Tasks/TaskModule.cs` + `GET /api/tasks/mine` (derived overdue / expiring / exceptions)

### Frontend (recharts + cmdk already installed)
- **Command center** — rebuilt `app/(main)/overview/page.tsx`: `StatTiles`, `TrendChart` (recharts), `AlertsStrip`, existing funnel
- **Reports** — real `app/(main)/reports/page.tsx` (catalog + `ReportView` with chart, table, Excel/PDF export)
- **Compliance** — `app/(main)/compliance/page.tsx`
- **My Work** — `app/(main)/my-work/page.tsx`
- **⌘K palette** — `components/command/command-palette.tsx` + `lib/stores/command-store.ts`, mounted in main layout, header search trigger
- **API hooks** — `lib/api/reports.ts`, `lib/api/insights.ts`, extended `lib/api/dashboard.ts`
- **Nav** — added "My Work" and "Compliance" entries

## Test coverage
- **Backend**: `dotnet build` clean; suite **134 stable / 135** (1 pre-existing flaky FsCheck
  `DisputeValidators` — passes on re-run, unrelated to this unit; flagged separately)
- **Frontend**: `tsc --noEmit` clean for all new/changed files
- **Playwright**: `e2e/reporting.spec.ts` (command center, reports+export buttons, compliance,
  my-work, ⌘K navigate) and updated `navigation.spec.ts` — require the running stack to execute

## Known pre-existing issues (NOT introduced here)
- `frontend/lib/ocr/passport-ocr.ts` (untracked) has a TS error that blocks `next build`
  (`ignoreBuildErrors: false`) — flagged for separate fix.

## Files of interest
- `backend/src/SimbaFlow.API/Features/Reports/*`
- `backend/src/SimbaFlow.Infrastructure/Services/Reporting/ReportExportService.cs`
- `backend/src/SimbaFlow.API/Features/Compliance/*`, `Features/Tasks/*`
- `backend/src/SimbaFlow.API/Features/Dashboard/Queries/DashboardQueries.cs`
- `frontend/app/(main)/{overview,reports,compliance,my-work}/page.tsx`
- `frontend/components/{dashboard,reports,command}/*`
