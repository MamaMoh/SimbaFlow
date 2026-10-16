"use client";

import { useMemo, useState } from "react";
import { Loader2, FileSpreadsheet, FileText } from "lucide-react";
import {
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  type ColumnDef,
  type SortingState,
} from "@tanstack/react-table";
import { LoadError } from "@/components/ui/page-alert";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import {
  useReport,
  reportExportUrl,
  formatCell,
  type ReportColumnType,
  type ReportTable,
} from "@/lib/api/reports";
import { ReportChart } from "@/components/reports/report-chart";
import { DataTable } from "@/components/data-table/data-table";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { indexColumn } from "@/components/data-table/index-column";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import { useBoardQuery } from "@/lib/data-table/use-board-query";
import { applyColumnFilters } from "@/lib/data-table/apply-filters";
import type { FilterKind } from "@/lib/data-table/column-filters";

function download(url: string) {
  const a = document.createElement("a");
  a.href = url;
  a.rel = "noopener";
  document.body.appendChild(a);
  a.click();
  a.remove();
}

const NUMERIC: ReportColumnType[] = ["Number", "Money", "Percent"];

/** What a report column's funnel compares as. */
const kindOf = (type: ReportColumnType): FilterKind =>
  NUMERIC.includes(type) ? "number" : type === "Date" ? "date" : "text";

type Row = Record<string, unknown>;

export function ReportView({
  reportKey,
  canExport,
}: {
  reportKey: string;
  canExport: boolean;
}) {
  const { data, error, isLoading, mutate } = useReport(reportKey);

  if (isLoading) {
    return (
      <div className="flex h-64 items-center justify-center rounded-lg border bg-card text-sm text-muted-foreground">
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Loading report…
      </div>
    );
  }

  if (error) {
    return <LoadError message={error.message} onRetry={() => mutate()} />;
  }

  if (!data) return null;

  return (
    <div className="space-y-5">
      <div>
        <h2 className="text-sm font-semibold">{data.title}</h2>
        {data.subtitle && <p className="text-sm text-muted-foreground">{data.subtitle}</p>}
      </div>

      {data.chartLabelKey && data.chartValueKey && data.rows.length > 0 && (
        <div className="rounded-lg border bg-card p-5 shadow-sm">
          <ReportChart report={data} />
        </div>
      )}

      {/* Keyed by report: each has its own columns, so its layout and filters start fresh. */}
      <ReportDataTable key={data.key} report={data} reportKey={reportKey} canExport={canExport} />
    </div>
  );
}

/**
 * The report's rows as the same table as every other board. The columns are the report's own,
 * so they are built from what it says it has; a report has no row actions, so there is no ⋯.
 *
 * Excel and PDF sit under Export with the CSV — the server's formatted versions, for anyone who
 * needs the report as a document rather than the rows as they are filtered on screen.
 */
function ReportDataTable({
  report,
  reportKey,
  canExport,
}: {
  report: ReportTable;
  reportKey: string;
  canExport: boolean;
}) {
  const [sorting, setSorting] = useState<SortingState>([]);
  const { globalFilter, setGlobalFilter, filters, setFilters } = useBoardQuery();

  const kinds = useMemo(
    () => Object.fromEntries(report.columns.map((c) => [c.key, kindOf(c.type)])),
    [report],
  );
  const rows = useMemo(() => applyColumnFilters<Row>(report.rows, filters, kinds), [report, filters, kinds]);

  const columns: ColumnDef<Row>[] = useMemo(
    () => [
      indexColumn<Row>(),
      ...report.columns.map<ColumnDef<Row>>((c) => ({
        id: c.key,
        accessorFn: (row) => row[c.key],
        meta: { filter: { field: c.key, kind: kindOf(c.type) } },
        header: ({ column }) => <DataTableColumnHeader column={column} title={c.label} />,
        cell: ({ row }) => (
          <span className={NUMERIC.includes(c.type) ? "tabular-nums" : undefined}>
            {formatCell(row.original[c.key], c.type)}
          </span>
        ),
      })),
    ],
    [report],
  );

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c) => c.id as string), [columns]);

  // How this person arranged this report, remembered on this computer.
  const layout = useTableLayout({ table: `report-${reportKey}`, columnIds });

  const table = useReactTable({
    data: rows,
    columns,
    state: {
      sorting,
      globalFilter,
      columnOrder: layout.columnOrder,
      columnVisibility: layout.columnVisibility,
    },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    initialState: { pagination: { pageSize: 20 } },
    onColumnOrderChange: layout.setColumnOrder,
    onColumnVisibilityChange: layout.setColumnVisibility,
  });

  return (
    <div className="rounded-lg border bg-card p-4 shadow-sm">
      <DataTable
        layout={layout}
        table={table}
        enableGlobalFilter
        searchPlaceholder="Search this report…"
        paginated
        exportFileName={canExport ? `report-${reportKey}` : undefined}
        exportActions={
          canExport ? (
            <>
              <DropdownMenuItem onClick={() => download(reportExportUrl(reportKey, "excel"))}>
                <FileSpreadsheet className="mr-2 h-4 w-4" /> Excel
              </DropdownMenuItem>
              <DropdownMenuItem onClick={() => download(reportExportUrl(reportKey, "pdf"))}>
                <FileText className="mr-2 h-4 w-4" /> PDF
              </DropdownMenuItem>
            </>
          ) : undefined
        }
        filters={filters}
        onFiltersChange={setFilters}
        emptyMessage="No data for this report yet."
      />
    </div>
  );
}
