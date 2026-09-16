"use client";

import type { ColumnDef } from "@tanstack/react-table";
import { Checkbox } from "@/components/ui/checkbox";

/**
 * The leading checkbox column, so a table can be worked on in batches.
 *
 * Paired with `getRowId: (row) => row.id` and `enableRowSelection: true` on the table, which is
 * what makes the selection keyed by candidate id rather than by row index — without it a sort or a
 * page change silently reassigns the selection to different people.
 */
export function selectionColumn<T>(): ColumnDef<T> {
  return {
    id: "select",
    header: ({ table }) => (
      <Checkbox
        checked={
          table.getIsAllPageRowsSelected() ||
          (table.getIsSomePageRowsSelected() && "indeterminate")
        }
        onCheckedChange={(value) => table.toggleAllPageRowsSelected(!!value)}
        aria-label="Select all"
      />
    ),
    cell: ({ row }) => (
      <Checkbox
        checked={row.getIsSelected()}
        onCheckedChange={(value) => row.toggleSelected(!!value)}
        aria-label="Select row"
        // The boards open the row's ⋯ menu when the row body is clicked; ticking a box is not
        // that, and without this the menu opens over the checkbox the moment you use it.
        onClick={(e) => e.stopPropagation()}
      />
    ),
    size: 36,
    enableSorting: false,
    enableHiding: false,
  };
}
