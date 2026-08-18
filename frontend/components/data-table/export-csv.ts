import type { Table } from "@tanstack/react-table";

/**
 * CSV of what the user is currently looking at.
 *
 * Uses the table's filtered+sorted row model and only the visible columns, so the file matches the
 * screen — exporting the raw dataset instead would quietly hand back rows the user had filtered out.
 */
function escapeCell(value: unknown): string {
  if (value == null) return "";
  const s = typeof value === "object" ? JSON.stringify(value) : String(value);
  // Excel treats a leading =, +, - or @ as a formula; prefix so pasted data can't execute.
  const safe = /^[=+\-@]/.test(s) ? `'${s}` : s;
  return `"${safe.replace(/"/g, '""')}"`;
}

/** Columns that carry no data (selection checkbox, row actions, the # index). */
const SKIP_COLUMN_IDS = new Set(["select", "actions", "index", "badges", "flags"]);

export function exportTableToCsv<TData>(table: Table<TData>, fileName: string) {
  const columns = table
    .getVisibleLeafColumns()
    .filter((c) => !SKIP_COLUMN_IDS.has(c.id));

  const header = columns.map((c) => {
    const h = c.columnDef.header;
    return escapeCell(typeof h === "string" ? h : c.id);
  });

  const rows = table.getFilteredRowModel().rows.map((row) =>
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
