/**
 * The same funnels, answered in the browser.
 *
 * Most boards are paginated by the API, so their filters travel: narrowing the twenty rows the
 * browser happens to be holding would answer a different question from the one being asked.
 * A handful are not. Roles, agencies, the partner catalogue and the exchange rates arrive whole
 * — there is no page two — and for those the rows in hand are all the rows there are, so
 * evaluating the filter here narrows the same set the server would have.
 *
 * It exists so that those boards look and behave like every other one. The alternative was to
 * leave them with the plain boxes they had, which would have meant two kinds of filter row in
 * the app and a person learning which board had which.
 *
 * The operators are the ones the server implements, and deliberately read the same way: blank
 * covers null and empty string together, text compares case-insensitively, and a value that
 * will not parse as a number or a date leaves the list alone rather than emptying it.
 */

import {
  isComplete,
  needsSecondValue,
  needsValue,
  type ColumnFilter,
  type FilterKind,
} from "@/lib/data-table/column-filters";

/** How a row answers for one field. Defaults to the property of that name. */
export type FieldReader<T> = (row: T, field: string) => unknown;

const byProperty = <T,>(row: T, field: string): unknown =>
  (row as Record<string, unknown>)[field];

const isBlank = (value: unknown): boolean =>
  value === null || value === undefined || String(value).trim() === "";

const num = (raw: unknown): number | null => {
  if (isBlank(raw)) return null;
  const n = Number(String(raw).trim());
  return Number.isFinite(n) ? n : null;
};

/**
 * A day, as a number, so two of them can be compared.
 *
 * Only the date part. A timestamp is almost never exactly midnight, so comparing the moment
 * would make "on today" match nothing and "on or before today" miss everything that happened
 * today — the same widening the server does.
 */
const day = (raw: unknown): number | null => {
  if (isBlank(raw)) return null;
  const text = String(raw).trim();
  const iso = text.match(/^(\d{4})-(\d{2})-(\d{2})/);
  if (iso) return Date.UTC(+iso[1], +iso[2] - 1, +iso[3]);
  const parsed = new Date(text);
  if (Number.isNaN(parsed.getTime())) return null;
  return Date.UTC(parsed.getFullYear(), parsed.getMonth(), parsed.getDate());
};

function compare(left: number, op: ColumnFilter["op"], right: number, right2: number): boolean {
  switch (op) {
    case "eq":
      return left === right;
    case "neq":
      return left !== right;
    case "gt":
      return left > right;
    case "gte":
      return left >= right;
    case "lt":
      return left < right;
    case "lte":
      return left <= right;
    case "between": {
      // Taken as a range whichever way round it was typed.
      const [low, high] = right <= right2 ? [right, right2] : [right2, right];
      return left >= low && left <= high;
    }
    default:
      return true;
  }
}

/**
 * Whether one row answers one filter.
 *
 * `true` for anything it cannot judge — an operator that needs a number against a box holding
 * "banana", a filter half typed. A list that empties itself while somebody is still typing is
 * worse than one that waits.
 */
export function matches(value: unknown, filter: ColumnFilter, kind: FilterKind): boolean {
  if (filter.op === "blank") return isBlank(value);
  if (filter.op === "notblank") return !isBlank(value);
  if (!needsValue(filter.op)) return true;

  const wanted = filter.value?.trim();
  if (!wanted) return true;
  if (needsSecondValue(filter.op) && !filter.value2?.trim()) return true;

  if (kind === "text") {
    const haystack = isBlank(value) ? "" : String(value).toLowerCase();
    const needle = wanted.toLowerCase();
    switch (filter.op) {
      case "contains":
        return haystack.includes(needle);
      case "startswith":
        return haystack.startsWith(needle);
      case "eq":
        return haystack === needle;
      case "neq":
        return haystack !== needle;
      default:
        return true;
    }
  }

  const read = kind === "date" ? day : num;
  const right = read(wanted);
  if (right === null) return true;
  const right2 = needsSecondValue(filter.op) ? read(filter.value2) : right;
  if (right2 === null) return true;

  const left = read(value);
  // Nothing recorded is not greater than, less than or equal to anything. "Not equal to 5"
  // included every blank row before this line, which is not what anybody means by it.
  if (left === null) return false;

  return compare(left, filter.op, right, right2);
}

/**
 * The rows that answer every filter.
 *
 * `kinds` says what each field is, so the same "2024-01-05" is compared as a date on one column
 * and as text on another. A field with no kind given is treated as text, which is what an
 * unfamiliar column most likely is.
 */
export function applyColumnFilters<T>(
  rows: T[],
  filters: ColumnFilter[],
  kinds: Record<string, FilterKind> = {},
  read: FieldReader<T> = byProperty,
): T[] {
  const ready = filters.filter(isComplete);
  if (ready.length === 0) return rows;

  return rows.filter((row) =>
    ready.every((filter) =>
      matches(read(row, filter.field), filter, kinds[filter.field] ?? "text"),
    ),
  );
}

/** The `kinds` map for a board, read off the column definitions it already wrote. */
export function kindsOf(columns: { meta?: unknown }[]): Record<string, FilterKind> {
  const kinds: Record<string, FilterKind> = {};
  for (const column of columns) {
    const spec = (column.meta as { filter?: { field: string; kind: FilterKind } } | undefined)
      ?.filter;
    if (spec) kinds[spec.field] = spec.kind;
  }
  return kinds;
}
