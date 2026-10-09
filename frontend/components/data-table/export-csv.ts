import type { Table } from "@tanstack/react-table";

function escapeCell(value: unknown): string {
  if (value == null) return "";
  const s = typeof value === "object" ? JSON.stringify(value) : String(value);
  // Excel treats a leading =, +, - or @ as a formula; prefix so pasted data can't execute.
  const safe = /^[=+\-@]/.test(s) ? `'${s}` : s;
  return `"${safe.replace(/"/g, '""')}"`;
}

/** Columns that carry no data (selection checkbox, row actions, the # index). */
const SKIP_COLUMN_IDS = new Set(["select", "actions", "index", "badges", "flags"]);

/**
 * CSV of what the user has picked out, or of what they are looking at.
 *
 * Ticking rows and pressing Export used to produce the whole page anyway, so the selection did
 * nothing and the file had to be pruned by hand in Excel. A selection is the narrowest statement
 * of intent on the screen, so it wins: tick nothing and the export is the current filtered,
 * sorted view, which is still never the raw dataset — rows the user filtered out stay out.
 *
 * Only the visible columns, so the file matches the screen.
 */
export function exportTableToCsv<TData>(table: Table<TData>, fileName: string) {
  const columns = table
    .getVisibleLeafColumns()
    .filter((c) => !SKIP_COLUMN_IDS.has(c.id));

  const header = columns.map((c) => {
    const h = c.columnDef.header;
    return escapeCell(typeof h === "string" ? h : c.id);
  });

  const selected = table.getSelectedRowModel().rows;
  const source = selected.length > 0 ? selected : table.getFilteredRowModel().rows;

  const rows = source.map((row) =>
    columns.map((c) => escapeCell(row.getValue(c.id))).join(",")
  );

  const csv = [header.join(","), ...rows].join("\r\n");
  // BOM so Excel opens UTF-8 (Amharic names) correctly instead of mojibake.
  const blob = new Blob(["﻿" + csv], { type: "text/csv;charset=utf-8;" });

  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `${fileName}-${new Date().toISOString().slice(0, 10)}.csv`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);

  return rows.length;
}
