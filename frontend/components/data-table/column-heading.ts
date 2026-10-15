import type { Column, Table } from "@tanstack/react-table";
import { toSentenceCase } from "@/lib/utils";

/**
 * What the table calls this column.
 *
 * The column menu used to print the column's id put through sentence case, which on these
 * boards gave "Contact Person2" and "Works In" for headings that read "Contact person (2nd)"
 * and "Worked in". That was tolerable while it was a list of tick boxes; it is not while it is
 * the list somebody arranges their table from, or the name on a filter's tooltip, because all
 * of them have to be recognisably the same thing.
 *
 * Every heading here is `<DataTableColumnHeader title="…" />`, so building the element and
 * reading its title gives the real wording. Building an element does not run the component —
 * nothing is rendered and no hook is called. A column whose heading is shaped some other way
 * falls back to the id, and anything that throws does too.
 */
export function headingOf<TData>(
  column: Column<TData, unknown>,
  table: Table<TData>,
): string {
  const header = column.columnDef.header;
  if (typeof header === "string" && header.trim()) return header;

  if (typeof header === "function") {
    try {
      const element: any = (header as any)({ column, table, header: undefined });
      const title = element?.props?.title;
      if (typeof title === "string" && title.trim()) return title;
    } catch {
      // Falls through to the id.
    }
  }

  return toSentenceCase(column.id);
}
