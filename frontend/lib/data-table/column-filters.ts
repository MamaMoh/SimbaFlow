/**
 * The filter row under the headings: what a person asked of one column.
 *
 * These travel to the server with the query. Boards are paginated there, so narrowing only the
 * rows the browser happens to be holding would answer a different question from the one being
 * asked — an agency with two thousand people on the books, filtering a page of twenty and
 * concluding nobody matches.
 *
 * Pure, so the rules can be tested without a browser.
 */

export type FilterKind = "text" | "number" | "date";

export type FilterOperator =
  | "contains"
  | "startswith"
  | "eq"
  | "neq"
  | "gt"
  | "gte"
  | "lt"
  | "lte"
  | "between"
  | "blank"
  | "notblank";

export type ColumnFilter = {
  /** The column's id, which is also the name the board's whitelist knows it by. */
  field: string;
  op: FilterOperator;
  value?: string;
  value2?: string;
};

/** What a column declares about itself, in its `meta`. */
export type ColumnFilterSpec = {
  /** The name the server's whitelist knows this column by. Usually the column id. */
  field: string;
  kind: FilterKind;
};

const LABELS: Record<FilterOperator, string> = {
  contains: "Contains",
  startswith: "Starts with",
  eq: "Equals",
  neq: "Does not equal",
  gt: "Greater than",
  gte: "Greater than or equal to",
  lt: "Less than",
  lte: "Less than or equal to",
  between: "Between",
  blank: "Blank",
  notblank: "Not blank",
};

/** Dates read better as earlier and later than as greater and less. */
const DATE_LABELS: Partial<Record<FilterOperator, string>> = {
  gt: "After",
  gte: "On or after",
  lt: "Before",
  lte: "On or before",
  eq: "On",
  neq: "Not on",
};

export function operatorLabel(op: FilterOperator, kind: FilterKind): string {
  return (kind === "date" && DATE_LABELS[op]) || LABELS[op];
}

/**
 * The operators a column of this kind offers.
 *
 * Text gets Contains first, because that is what somebody typing part of a name means and
 * asking them to choose it every time would be the wrong default. Numbers and dates get the
 * comparisons, which is the whole reason to have an operator at all.
 */
export function operatorsFor(kind: FilterKind): FilterOperator[] {
  if (kind === "text") {
    return ["contains", "eq", "neq", "startswith", "blank", "notblank"];
  }
  return ["eq", "neq", "gt", "gte", "lt", "lte", "between", "blank", "notblank"];
}

export function defaultOperator(kind: FilterKind): FilterOperator {
  return kind === "text" ? "contains" : "eq";
}

export function needsValue(op: FilterOperator): boolean {
  return op !== "blank" && op !== "notblank";
}

export function needsSecondValue(op: FilterOperator): boolean {
  return op === "between";
}

/** Whether this filter is finished enough to ask the server about. */
export function isComplete(filter: ColumnFilter): boolean {
  if (!filter.field) return false;
  if (needsValue(filter.op) && !filter.value?.trim()) return false;
  if (needsSecondValue(filter.op) && !filter.value2?.trim()) return false;
  return true;
}

/**
 * The `filters` query parameter, or undefined when there is nothing to ask.
 *
 * Half-typed filters are left out rather than sent: "greater than" with an empty box is the
 * ordinary state while somebody is still typing, and it should leave the list alone rather
 * than empty it and then fill it again on the next keystroke.
 */
export function toQueryParam(filters: ColumnFilter[]): string | undefined {
  const ready = filters.filter(isComplete).map((f) => ({
    field: f.field,
    op: f.op,
    ...(needsValue(f.op) ? { value: f.value?.trim() } : {}),
    ...(needsSecondValue(f.op) ? { value2: f.value2?.trim() } : {}),
  }));

  return ready.length > 0 ? JSON.stringify(ready) : undefined;
}

/** `filters` with this column's filter replaced, or removed when it has been cleared. */
export function withFilter(
  filters: ColumnFilter[],
  field: string,
  next: ColumnFilter | null,
): ColumnFilter[] {
  const others = filters.filter((f) => f.field !== field);
  return next ? [...others, next] : others;
}

export function filterFor(
  filters: ColumnFilter[],
  field: string,
): ColumnFilter | undefined {
  return filters.find((f) => f.field === field);
}

/** How a set filter reads on the funnel's tooltip, and to a screen reader. */
export function describe(
  filter: ColumnFilter,
  heading: string,
  kind: FilterKind,
): string {
  const op = operatorLabel(filter.op, kind).toLowerCase();
  if (!needsValue(filter.op)) return `${heading} is ${op}`;
  if (needsSecondValue(filter.op))
    return `${heading} ${op} ${filter.value} and ${filter.value2}`;
  return `${heading} ${op} ${filter.value}`;
}
