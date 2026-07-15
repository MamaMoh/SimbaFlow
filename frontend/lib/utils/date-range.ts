/**
 * Reusable date range utilities for the date range picker.
 * Keeps comparison and normalization logic separate from UI.
 */

/** Normalize to date-only (noon UTC) to avoid timezone edge cases in day comparison */
export function toDateOnly(d: Date): Date {
  return new Date(Date.UTC(d.getFullYear(), d.getMonth(), d.getDate()));
}

export function isSameDay(a: Date, b: Date): boolean {
  return toDateOnly(a).getTime() === toDateOnly(b).getTime();
}

export function isBeforeDay(a: Date, b: Date): boolean {
  return toDateOnly(a).getTime() < toDateOnly(b).getTime();
}

export function isAfterDay(a: Date, b: Date): boolean {
  return toDateOnly(a).getTime() > toDateOnly(b).getTime();
}

/** Returns true if date is strictly before today (date-only comparison) */
export function isPastDate(date: Date): boolean {
  return isBeforeDay(date, new Date());
}

/** Normalize range so from <= to. Handles reversed selection. */
export function normalizeRange(range: {
  from?: Date;
  to?: Date;
}): { from?: Date; to?: Date } | undefined {
  if (!range?.from) return range?.to ? undefined : range;
  const from = range.from;
  const to = range.to;
  if (!to) return { from, to: undefined };
  if (isSameDay(from, to)) return { from, to };
  return isBeforeDay(from, to) ? { from, to } : { from: to, to: from };
}

/** Get sorted [start, end] timestamps for a range (from hover or selection) */
export function getRangeBounds(from: Date, to: Date): [number, number] {
  const tFrom = toDateOnly(from).getTime();
  const tTo = toDateOnly(to).getTime();
  return tFrom <= tTo ? [tFrom, tTo] : [tTo, tFrom];
}

export function isDateInRange(date: Date, low: number, high: number): boolean {
  const t = toDateOnly(date).getTime();
  return t >= low && t <= high;
}

export function isRangeStart(date: Date, low: number, high: number): boolean {
  return toDateOnly(date).getTime() === low;
}

export function isRangeEnd(date: Date, low: number, high: number): boolean {
  return toDateOnly(date).getTime() === high;
}

export function isRangeMiddle(date: Date, low: number, high: number): boolean {
  const t = toDateOnly(date).getTime();
  return t > low && t < high;
}
