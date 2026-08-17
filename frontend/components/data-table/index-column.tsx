import type { ColumnDef } from "@tanstack/react-table";

/**
 * Standard leading "#" row-number column. Every data table starts with this so
 * staff can refer to a row by number when talking to each other.
 */
export function indexColumn<T>(): ColumnDef<T> {
  return {
    id: "index",
    header: "#",
    cell: ({ row }) => (
      <span className="text-muted-foreground">{row.index + 1}</span>
    ),
    size: 40,
    enableSorting: false,
  };
}
