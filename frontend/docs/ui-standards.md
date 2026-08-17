# SimbaFlow UI Standards

One look across every page. When building or editing a page, use these — do not hand-roll
tables, status colors, headers, or form layouts.

## Status colors (single source of truth)
- All status colors live in `lib/ui/status.ts` as **tones**: `success | warning | danger | info | progress | neutral`.
- Render every status with `<StatusBadge>` (`components/ui/status-badge.tsx`).
  - `<StatusBadge value="Issued" />` — auto-derives the tone.
  - `<StatusBadge tone={commissionTone(s)} value={s} />` — domain helper for exact mapping.
- Domain helpers: `commissionTone`, `exceptionTone`, `tenantTone`, `remainingDays`, `statusTone`.
- Never write `bg-green-*/bg-red-*` for a status inline. Add/adjust the mapping in `status.ts` instead.

## Page layout
- Top of every page: `<PageHeader title description actions />` (`components/ui/page-header.tsx`).
  Title left, **primary action button on the right**. Page content below in a `flex flex-col gap-6`.

## Tables
- Use the shared table `components/data-table/data-table.tsx` (TanStack + toolbar + pagination + print).
  No raw `<table>` for data lists. Column headers via `DataTableColumnHeader`.
- **Always wrap the table in the standard card:** `<div className="rounded-lg border bg-card p-4 shadow-sm">`.
- **Never gate the table on having rows.** Render it whenever there's no load error and pass
  `emptyMessage` — the table shows its own headers + empty state. (A `rows.length > 0 &&` gate
  produces a blank page, which is what we fixed.)
- **Every table starts with a `#` row-number column:** `indexColumn<Row>()` from
  `components/data-table/index-column.tsx`, as the first entry in the columns array.
- **Names and other long text:** use `components/data-table/name-cell.tsx` (`<NameCell>`), which
  clamps to one line with an ellipsis and keeps the full value as a tooltip. Long candidate names
  otherwise stretch the column and push the rest of the table off screen.

## Detail pages
- Header: back link, avatar, **name clamped to two lines** (`line-clamp-2` + `title`), key
  identifiers, then status chips. One **primary** action (Edit) plus a ⋯ menu for the rest —
  never a stack of equal-weight buttons.
- **Hide empty fields by default.** A record has dozens of optional fields; showing them all as
  "—" buries the real data. Sections drop fields with no value, hide themselves when everything
  is empty, show a quiet "N not filled" hint, and the page offers a "Show all fields" toggle.

## Row actions
- Exactly **one** ⋯ (`MoreHorizontal`) trigger per row, **centered** in the `actions` column:
  `<Button variant="ghost" size="sm" className="h-8 w-8 p-0" aria-label="Row actions">`.
- Everything goes inside that menu: "View details" first, then stage-specific actions, then the
  candidate's workflow transitions via `components/workflow/workflow-action-items.tsx`
  (`<WorkflowActionItems>`). No loose/stacked buttons in the Actions column, and no labelled
  "<Stage> actions" dropdowns.
- "View details" is available to everyone; gate only the mutating items on the relevant permission.

## Buttons
- Use `components/ui/button.tsx`. Primary action = default variant; secondary = `outline`; cancel = `ghost`;
  destructive = `destructive` (always behind a confirm dialog). Primary action sits top-right (in `PageHeader.actions`)
  or bottom-right (in `FormActions`).

## Side sheets (create / edit / status)
- `Sheet` + `SheetHeader` (title/description) + scrollable body + `SheetFooter`.
- `SheetFooter` is the standard action bar: pinned bottom, **Cancel then Save right-aligned** on
  `sm`+ (stacked with the primary on top on narrow screens). Don't override its direction.

## Forms (simple for non-technical users)
- Build with `components/ui/form-kit.tsx`: `FormSection` (optionally `collapsible`), `Field` (label + required `*`
  + help + inline error), `FormActions` (Cancel then Save, right-aligned).
- Keep the **required** set minimal; put optional/advanced groups in collapsible sections.
- Plain-language labels and short help text. Validate inline, show one clear error under the field.
