import { Icons } from "@/components/ui/icons"


export interface NavItem {
  title: string
  href?: string
  disabled?: boolean
  external?: boolean
  permission?: string
  icon?: keyof typeof Icons
  label?: string
  description?: string
}

export interface NavItemWithOptionalChildren extends NavItem {
  items?: NavItemWithOptionalChildren[]
}

export type MainNavItem = NavItemWithOptionalChildren

export type recordStatus = 1 | 2 | 3

// Centralized record status constants and helpers (single source of truth)
export const RECORD_STATUS = {
  Inactive: 1 as recordStatus,
  Active: 2 as recordStatus,
  New: 3 as recordStatus,
} as const

export const RECORD_STATUS_LABELS: Record<recordStatus, string> = {
  1: "Inactive",
  2: "Active",
  3: "New",
}

export const RECORD_STATUS_OPTIONS: Option[] = [
  { label: RECORD_STATUS_LABELS[RECORD_STATUS.Active], value: String(RECORD_STATUS.Active) },
  { label: RECORD_STATUS_LABELS[RECORD_STATUS.Inactive], value: String(RECORD_STATUS.Inactive) },
  { label: RECORD_STATUS_LABELS[RECORD_STATUS.New], value: String(RECORD_STATUS.New) },
]

export function getRecordStatusMeta(status: recordStatus): {
  label: string
  className: string
} {
  const label = RECORD_STATUS_LABELS[status] ?? "Unknown"
  let className = "px-2 py-0.5 rounded text-xs font-medium"

  switch (status) {
    case RECORD_STATUS.New:
      className += " bg-blue-100 text-blue-800 dark:bg-blue-900/40 dark:text-blue-300"
      break
    case RECORD_STATUS.Active:
      className += " bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300"
      break
    case RECORD_STATUS.Inactive:
      className += " bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-300"
      break
    default:
      className += " bg-gray-100 text-gray-800 dark:bg-gray-900/40 dark:text-gray-300"
  }

  return { label, className }
}

// Generic multi-select filter function for TanStack Table columns.
// - Accepts any underlying cell value (number, string, etc.)
// - Coerces to string to match faceted filter option values
// - Treats empty filter as pass-through
export function multiSelectStringIncludesFilter(
  row: any,
  id: string,
  filterValues: string[] | undefined,
): boolean {
  if (!Array.isArray(filterValues) || filterValues.length === 0) return true
  const value = row.getValue(id)
  const normalized = typeof value === "number" ? String(value) : String(value ?? "")
  return filterValues.includes(normalized)
}

export interface Option {
  label: string
  value: string
  icon?: React.ComponentType<{ className?: string }>
}

export interface DataTableSearchableColumn<TData> {
  id: keyof TData
  title: string
  // Optional: map to backend query param name
  queryKey?: string
}

export interface DataTableFilterableColumn<TData>
  extends DataTableSearchableColumn<TData> {
  options: Option[]
}

