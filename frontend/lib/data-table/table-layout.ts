/**
 * A person's own arrangement of one table: which columns they keep, and in what order.
 *
 * Pure functions, so the rules are testable without a browser. The hook around them is in
 * use-table-layout.ts.
 *
 * Two columns never move. The tick box and the row number sit at the front because they are how
 * a row is addressed rather than anything about the candidate, and the ⋯ menu is pinned to the
 * right edge of the table — dragging it into the middle would pin the middle.
 */

export type StoredLayout = {
  /** Movable column ids, in the order this person put them. */
  order?: string[];
  /** Only what they have actually turned on or off. Absent means "whatever the board says". */
  visibility?: Record<string, boolean>;
};

export const LAYOUT_PREFIX = "simbaflow:table:";

export function layoutKey(table: string): string {
  return `${LAYOUT_PREFIX}${table}`;
}

/**
 * The order to hand the table: locked ids at each end, the person's arrangement in between.
 *
 * A saved order is never trusted to still describe the table. Columns are added and removed
 * between releases, so ids that no longer exist are dropped and ids that are new are appended —
 * a new column arrives at the end of somebody's arrangement rather than silently reshuffling
 * what they set up, and it can be dragged from there.
 */
export function resolveOrder(
  all: string[],
  saved: string[] | undefined,
  locked: { start: string[]; end: string[] },
): string[] {
  const movable = all.filter(
    (id) => !locked.start.includes(id) && !locked.end.includes(id),
  );

  const known = new Set(movable);
  const seen = new Set<string>();
  const arranged: string[] = [];

  for (const id of saved ?? []) {
    if (known.has(id) && !seen.has(id)) {
      arranged.push(id);
      seen.add(id);
    }
  }
  for (const id of movable) {
    if (!seen.has(id)) arranged.push(id);
  }

  return [
    ...locked.start.filter((id) => all.includes(id)),
    ...arranged,
    ...locked.end.filter((id) => all.includes(id)),
  ];
}

/**
 * What the board hides out of the box, overlaid with what this person has since decided.
 *
 * Their choices win, but only for columns they have expressed one about. A column added in a
 * later release follows the board's own default until it is touched, which is why only the
 * changes are stored and not the whole map.
 */
export function resolveVisibility(
  all: string[],
  defaults: Record<string, boolean>,
  saved: Record<string, boolean> | undefined,
): Record<string, boolean> {
  const resolved: Record<string, boolean> = {};
  for (const [id, visible] of Object.entries(defaults)) {
    if (all.includes(id)) resolved[id] = visible;
  }
  for (const [id, visible] of Object.entries(saved ?? {})) {
    if (all.includes(id)) resolved[id] = visible;
  }
  return resolved;
}

/**
 * `order` with `id` moved one place, within the movable run only.
 *
 * Returns the same array when there is nowhere to go, so a button at the end of the list is a
 * no-op rather than something that quietly pushes a column past the pinned one.
 */
export function moveWithin(
  order: string[],
  id: string,
  delta: -1 | 1,
  locked: { start: string[]; end: string[] },
): string[] {
  const movable = order.filter(
    (c) => !locked.start.includes(c) && !locked.end.includes(c),
  );
  const from = movable.indexOf(id);
  if (from < 0) return order;

  const to = from + delta;
  if (to < 0 || to >= movable.length) return order;

  const next = [...movable];
  next.splice(from, 1);
  next.splice(to, 0, id);

  return [
    ...order.filter((c) => locked.start.includes(c)),
    ...next,
    ...order.filter((c) => locked.end.includes(c)),
  ];
}

/** `order` with `dragged` dropped onto the place `target` holds. */
export function moveOnto(
  order: string[],
  dragged: string,
  target: string,
  locked: { start: string[]; end: string[] },
): string[] {
  const movable = order.filter(
    (c) => !locked.start.includes(c) && !locked.end.includes(c),
  );
  const from = movable.indexOf(dragged);
  const to = movable.indexOf(target);
  if (from < 0 || to < 0 || from === to) return order;

  const next = [...movable];
  next.splice(from, 1);
  next.splice(to, 0, dragged);

  return [
    ...order.filter((c) => locked.start.includes(c)),
    ...next,
    ...order.filter((c) => locked.end.includes(c)),
  ];
}

/** Whether this person has arranged anything, or is still on the board's own layout. */
export function isArranged(
  all: string[],
  defaults: Record<string, boolean>,
  saved: StoredLayout | null,
  locked: { start: string[]; end: string[] },
): boolean {
  if (!saved) return false;
  if (saved.visibility && Object.keys(saved.visibility).length > 0) return true;
  if (!saved.order) return false;

  const theirs = resolveOrder(all, saved.order, locked);
  const ours = resolveOrder(all, undefined, locked);
  return theirs.join("\u0000") !== ours.join("\u0000");
}
