"use client";

import { useRouter } from "next/navigation";

import { useMemo, useState } from "react";
import {
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  type ColumnDef,
} from "@tanstack/react-table";
import { DataTable } from "@/components/data-table/data-table";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import { useBoardQuery } from "@/lib/data-table/use-board-query";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { ExceptionStatusBadge } from "@/components/workflow/remaining-days-badge";
import { useExceptionCases, type ExceptionCaseListItem } from "@/lib/api/exceptions";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";
import { indexColumn } from "@/components/data-table/index-column";
import { identityColumns } from "@/components/data-table/identity-columns";

export default function ExceptionsListPage() {
  const router = useRouter();
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView =
    hasPermission("arrival.read") ||
    hasPermission("arrival.exception") ||
    hasPermission("system.admin");
  const [status, setStatus] = useState<string>("");
  const [type, setType] = useState<string>("");
  // The search box in the toolbar and the funnels under the headings. Both are answered by
  // the API: narrowing the page the browser happens to be holding would answer a different
  // question from the one being asked.
  const { globalFilter, setGlobalFilter, search, filters, setFilters, filterParam } =
    useBoardQuery();

  const { cases, totalCount, isLoading, error, mutate } = useExceptionCases({
    status: status || undefined,
    type: type || undefined,
    search: search || undefined,
    filters: filterParam,
    pageSize: 50,
  });

  const columns = useMemo<ColumnDef<ExceptionCaseListItem>[]>(
    () => [
      indexColumn<ExceptionCaseListItem>(),
      {
        accessorKey: "candidateName",
        // A row here is a case joined to the candidate it is about, so the person's columns
        // answer to the names this board gives them.
        meta: { filter: { field: "candidateName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Candidate" />,
        cell: ({ row }) => (
          <NameCell
            href={`/workflow/exceptions/${row.original.id}`}
            name={row.original.candidateName}
          />
        ),
      },
      ...identityColumns<ExceptionCaseListItem>(),
      {
        accessorKey: "type",
        meta: { filter: { field: "type", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Type" />,
      },
      {
        accessorKey: "status",
        meta: { filter: { field: "status", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Status" />,
        cell: ({ row }) => <ExceptionStatusBadge status={row.original.status} />,
      },
      {
        accessorKey: "openedAt",
        meta: { filter: { field: "openedAt", kind: "date" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Opened" />,
        cell: ({ getValue }) =>
          new Date(getValue() as string).toLocaleDateString(),
      },
      {
        id: "impact",
        meta: { filter: { field: "impact", kind: "number" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Impact" />,
        cell: ({ row }) =>
          row.original.financialImpactAmount != null
            ? `${row.original.financialImpactAmount} ${row.original.financialImpactCurrency ?? ""}`
            : "—",
      },
    ],
    []
  );

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "exceptions", columnIds });

  const table = useReactTable({
    state: {
      globalFilter,
      columnOrder: layout.columnOrder,
      columnVisibility: layout.columnVisibility,
    },
    // The search box belongs to the API. Left as TanStack's own it would narrow the rows the
    // API already chose, hiding the ones it matched on a column that is not on screen.
    globalFilterFn: () => true,
    onGlobalFilterChange: setGlobalFilter,
    data: cases,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    initialState: { pagination: { pageSize: 20 } },
    onColumnOrderChange: layout.setColumnOrder,
    onColumnVisibilityChange: layout.setColumnVisibility,
  });

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin mr-2" />
        Loading…
      </div>
    );
  }

  if (!canView) return <AccessDenied resource="Exceptions" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Exceptions" count={totalCount} />

      {error && <LoadError message={error.message} onRetry={() => mutate()} />}

      {!error && (
        <div className="rounded-lg border bg-card p-4 shadow-sm">
          {isLoading ? (
            <div className="flex items-center justify-center py-12 text-muted-foreground">
              <Loader2 className="mr-2 h-5 w-5 animate-spin" />
              Loading…
            </div>
          ) : (
            <DataTable
              layout={layout}
            onRowClick={(row: { id: string }) => router.push(`/workflow/exceptions/${row.id}`)}
              exportFileName="exceptions"
              table={table}
              paginated
              enableGlobalFilter
              searchPlaceholder="Search name, passport or labour ID…"
              filters={filters}
              onFiltersChange={setFilters}
              toolbarEndActions={
                <div className="flex flex-wrap items-center gap-2">
                  <Select value={status || "all"} onValueChange={(v) => setStatus(v === "all" ? "" : v)}>
                    <SelectTrigger className="h-8 w-[150px]" aria-label="Status">
                      <SelectValue placeholder="Status" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All statuses</SelectItem>
                      <SelectItem value="Open">Open</SelectItem>
                      <SelectItem value="UnderInvestigation">Investigating</SelectItem>
                      <SelectItem value="Resolved">Resolved</SelectItem>
                      <SelectItem value="Closed">Closed</SelectItem>
                    </SelectContent>
                  </Select>
                  <Select value={type || "all"} onValueChange={(v) => setType(v === "all" ? "" : v)}>
                    <SelectTrigger className="h-8 w-[130px]" aria-label="Type">
                      <SelectValue placeholder="Type" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All types</SelectItem>
                      <SelectItem value="Returned">Returned</SelectItem>
                      <SelectItem value="Runaway">Runaway</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              }
              emptyMessage="No exception cases — Returned/Runaway cases will appear here."
            />
          )}
        </div>
      )}
    </div>
  );
}
