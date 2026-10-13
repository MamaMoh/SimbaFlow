import type { ColumnDef } from "@tanstack/react-table";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";

/**
 * The two numbers a candidate is known by, as a pair.
 *
 * Passport is how the agency's own staff find somebody; the labour ID is how the Ministry, the
 * embassy and the destination agent refer to the same person. Anyone reconciling a list from
 * outside has only the second one, and until now only the first was on screen — so every board
 * meant opening the candidate to read one number off their record.
 *
 * Defined once and spread into every table rather than written out eleven times: they belong
 * next to each other, and two columns copied by hand into eleven files do not stay next to each
 * other for long.
 */
export function identityColumns<T>(): ColumnDef<T>[] {
  return [passportColumn<T>(), labourIdColumn<T>()];
}

export function passportColumn<T>(): ColumnDef<T> {
  return {
    accessorKey: "passportNumber",
    header: ({ column }) => <DataTableColumnHeader column={column} title="Passport" />,
    cell: ({ getValue }) => (
      <span className="whitespace-nowrap tabular-nums">{(getValue() as string) || "—"}</span>
    ),
  };
}

export function labourIdColumn<T>(): ColumnDef<T> {
  return {
    accessorKey: "labourId",
    header: ({ column }) => <DataTableColumnHeader column={column} title="Labour ID" />,
    // Em dash rather than blank: most candidates have no labour ID until the Ministry issues
    // one, and an empty cell reads as a column that is broken rather than as a number not yet
    // given out.
    cell: ({ getValue }) => {
      const value = getValue() as string | null | undefined;
      return value ? (
        <span className="whitespace-nowrap tabular-nums">{value}</span>
      ) : (
        <span className="text-muted-foreground">—</span>
      );
    },
  };
}
