"use client";

import { useMemo } from "react";

/**
 * Rows for a table, with an identity that only changes when the rows do.
 *
 * `data?.data || []` reads harmlessly and is a trap. While a request is in flight SWR has no
 * data, so that expression builds a brand-new empty array on every single render — and
 * TanStack Table treats a new data reference as new data: it rebuilds its row models and, with
 * auto-reset enabled, resets the page index, which is a state change, which is another render,
 * which builds another new array.
 *
 * The loop runs as fast as the machine allows and never yields, so the response that would have
 * ended it can never be delivered. The tab stops answering — including the reload the person
 * then tries, because a wedged renderer cannot process that either. It fires when a request is
 * in flight with nothing cached behind it: switching to a board you have not opened yet,
 * picking a stage chip for the first time, pressing Browse catalog.
 *
 * One empty array, kept, is the whole fix.
 */
export function useTableRows<T>(rows: readonly T[] | undefined | null): T[] {
  return useMemo(() => (rows ?? []) as T[], [rows]);
}
