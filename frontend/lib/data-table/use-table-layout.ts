"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import type { OnChangeFn, VisibilityState } from "@tanstack/react-table";
import {
  isArranged,
  layoutKey,
  moveOnto,
  moveWithin,
  resolveOrder,
  resolveVisibility,
  type StoredLayout,
} from "@/lib/data-table/table-layout";

/** Never movable: these two are how a row is addressed, not anything about it. */
const LOCKED_START = ["select", "index"];
/** Pinned to the right edge by the table itself. */
const LOCKED_END = ["actions"];

export type TableLayout = {
  columnOrder: string[];
  columnVisibility: VisibilityState;
  setColumnOrder: OnChangeFn<string[]>;
  setColumnVisibility: OnChangeFn<VisibilityState>;
  /** One place left or right, for a thumb or a keyboard. */
  moveColumn: (id: string, delta: -1 | 1) => void;
  /** Dropped onto another column's place, for a mouse. */
  dropColumn: (dragged: string, target: string) => void;
  /** Back to the board's own layout. */
  reset: () => void;
  arranged: boolean;
  locked: (id: string) => boolean;
};

/**
 * Remembers how one person arranged one table.
 *
 * Kept in the browser rather than on their account, deliberately. A column layout is about the
 * screen in front of somebody — the desk on a wide monitor and the same person on a laptop in
 * the evening do not want the same six columns — and it is not worth a round trip, a migration
 * and a conflict when two tabs disagree. The cost is that it does not follow them to another
 * machine, which for this is the right trade.
 *
 * Reading can throw and can come back empty: a private window, cleared site data, a browser
 * that refuses storage. Every read and write is guarded and the table renders from the board's
 * own defaults when there is nothing to read, so a blocked store is a table that does not
 * remember rather than a table that does not draw.
 */
export function useTableLayout({
  table,
  columnIds,
  defaultVisibility = {},
  managed = [],
}: {
  /** Storage key. One per board, and stable — renaming it forgets everyone's layout. */
  table: string;
  /** Every column the board declares, in the order the code lists them. Memoise it. */
  columnIds: string[];
  /** What this board hides out of the box. */
  defaultVisibility?: VisibilityState;
  /**
   * Columns the board itself turns on and off, which are never written down as a choice.
   *
   * Candidates has one: "Deleted by" appears only on the Inactive list and goes away again on
   * the way back. That is the board following the filter, not somebody arranging their screen,
   * and storing it would leave a column of dashes on the active list forever after one look at
   * the inactive one.
   */
  managed?: string[];
}): TableLayout {
  const locked = useMemo(() => ({ start: LOCKED_START, end: LOCKED_END }), []);
  const [saved, setSaved] = useState<StoredLayout | null>(null);

  // Read after mount, not during render: localStorage does not exist on the server, and a value
  // read during the first render would not match what the server sent.
  useEffect(() => {
    try {
      const raw = window.localStorage.getItem(layoutKey(table));
      setSaved(raw ? (JSON.parse(raw) as StoredLayout) : null);
    } catch {
      setSaved(null);
    }
  }, [table]);

  const persist = useCallback(
    (next: StoredLayout | null) => {
      setSaved(next);
      try {
        if (next === null) window.localStorage.removeItem(layoutKey(table));
        else window.localStorage.setItem(layoutKey(table), JSON.stringify(next));
      } catch {
        // A browser that will not store it still shows it for this session.
      }
    },
    [table],
  );

  const columnOrder = useMemo(
    () => resolveOrder(columnIds, saved?.order, locked),
    [columnIds, saved?.order, locked],
  );

  const columnVisibility = useMemo(
    () => resolveVisibility(columnIds, defaultVisibility, saved?.visibility),
    // defaultVisibility is written inline by every caller, so a new object each render; its
    // contents are what matter.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [columnIds, JSON.stringify(defaultVisibility), saved?.visibility],
  );

  const setColumnOrder = useCallback<OnChangeFn<string[]>>(
    (updater) => {
      const next = typeof updater === "function" ? updater(columnOrder) : updater;
      persist({ ...saved, order: next.filter((id) => !locked.start.includes(id) && !locked.end.includes(id)) });
    },
    [columnOrder, locked, persist, saved],
  );

  const setColumnVisibility = useCallback<OnChangeFn<VisibilityState>>(
    (updater) => {
      const next = typeof updater === "function" ? updater(columnVisibility) : updater;

      // Only the differences from the board's own defaults are kept, so a column added in a
      // later release follows the board until this person says otherwise.
      const changed: Record<string, boolean> = {};
      for (const id of columnIds) {
        if (managed.includes(id)) continue;
        const wanted = next[id] ?? true;
        const standard = defaultVisibility[id] ?? true;
        if (wanted !== standard) changed[id] = wanted;
      }
      persist({ ...saved, visibility: changed });
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [columnIds, columnVisibility, defaultVisibility, managed.join(","), persist, saved],
  );

  const moveColumn = useCallback(
    (id: string, delta: -1 | 1) => setColumnOrder(moveWithin(columnOrder, id, delta, locked)),
    [columnOrder, locked, setColumnOrder],
  );

  const dropColumn = useCallback(
    (dragged: string, target: string) =>
      setColumnOrder(moveOnto(columnOrder, dragged, target, locked)),
    [columnOrder, locked, setColumnOrder],
  );

  const reset = useCallback(() => persist(null), [persist]);

  return {
    columnOrder,
    columnVisibility,
    setColumnOrder,
    setColumnVisibility,
    moveColumn,
    dropColumn,
    reset,
    arranged: isArranged(columnIds, defaultVisibility, saved, locked),
    locked: (id: string) => locked.start.includes(id) || locked.end.includes(id),
  };
}
