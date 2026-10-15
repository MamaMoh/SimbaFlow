"use client";

import { useRouter } from "next/navigation";

import { useMemo, useState } from "react";
import {
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  type ColumnDef,
  type SortingState,
} from "@tanstack/react-table";
import { DataTable } from "@/components/data-table/data-table";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import { useBoardQuery } from "@/lib/data-table/use-board-query";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { CommissionStatusBadge } from "@/components/finance/commission-status-badge";
import { formatEtb, useCommissionBoard, type CommissionBoardRow } from "@/lib/api/commissions";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";
import { indexColumn } from "@/components/data-table/index-column";
import { identityColumns } from "@/components/data-table/identity-columns";

export default function CommissionsPage() {
  const router = useRouter();
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("commission.read") || hasPermission("system.admin");
  const [sorting, setSorting] = useState<SortingState>([]);
  const [statusFilter, setStatusFilter] = useState("");
  // The search box in the toolbar and the funnels under the headings. Both are answered by
  // the API: narrowing the page the browser happens to be holding would answer a different
  // question from the one being asked.
  const { globalFilter, setGlobalFilter, search, filters, setFilters, filterParam } =
    useBoardQuery();

  const { items, totalCount, isLoading, error, mutate } = useCommissionBoard({
    status: statusFilter || undefined,
    search: search || undefined,
    filters: filterParam,
    pageSize: 100,
  });

  const columns = useMemo<ColumnDef<CommissionBoardRow>[]>(
    () => [
      indexColumn<CommissionBoardRow>(),
      {
        accessorKey: "candidateName",
        // A row here is a commission joined to the candidate it is about, so the person's
        // columns answer to the names this board gives them.
        meta: { filter: { field: "candidateName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Candidate" />,
        cell: ({ row }) => (
          <NameCell
            href={`/workflow/commissions/${row.original.id}`}
            name={row.original.candidateName}
          />
        ),
      },
      ...identityColumns<CommissionBoardRow>(),
      {
        accessorKey: "countryOfTravel",
        meta: { filter: { field: "countryOfTravel", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Country" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "partnerName",
        meta: { filter: { field: "partnerName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Partner" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "status",
        meta: { filter: { field: "status", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Status" />,
        cell: ({ getValue }) => <CommissionStatusBadge status={String(getValue() || "")} />,
      },
      {
        accessorKey: "totalFeesAmount",
        meta: { filter: { field: "totalFeesAmount", kind: "number" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Fees (ETB)" />,
        cell: ({ getValue }) => formatEtb(getValue() as number),
      },
      {
        accessorKey: "totalPaidAmount",
        meta: { filter: { field: "totalPaidAmount", kind: "number" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Paid" />,
        cell: ({ getValue }) => formatEtb(getValue() as number),
      },
      {
        accessorKey: "balanceAmount",
        meta: { filter: { field: "balanceAmount", kind: "number" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Balance" />,
        cell: ({ getValue }) => formatEtb(getValue() as number),
      },
      {
        accessorKey: "openedAt",
        meta: { filter: { field: "openedAt", kind: "date" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Opened" />,
        cell: ({ getValue }) => {
          const v = getValue() as string;
          return v ? new Date(v).toLocaleDateString() : "—";
        },
      },
    ],
    []
  );

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "commissions", columnIds });

  const table = useReactTable({
    data: items,
    columns,
    state: {
      sorting,
      globalFilter,
      columnOrder: layout.columnOrder,
      columnVisibility: layout.columnVisibility,
    },
    // The search box belongs to the API. Left as TanStack's own it would narrow the rows the
    // API already chose, hiding the ones it matched on a column that is not on screen.
    globalFilterFn: () => true,
    onGlobalFilterChange: setGlobalFilter,
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
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

  if (!canView) return <AccessDenied resource="Commissions" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Commissions"
        count={totalCount}
      />

      {error ? (
        <LoadError
          message={error instanceof Error ? error.message : "Failed to load"}
          onRetry={() => mutate()}
        />
      ) : null}


      {!error ? (
        <div className="rounded-lg border bg-card p-4 shadow-sm">
          <DataTable
            layout={layout}
            onRowClick={(row: { id: string }) => router.push(`/workflow/commissions/${row.id}`)}
        exportFileName="commissions"
            table={table}
            paginated
            enableGlobalFilter
            searchPlaceholder="Search name or passport…"
            filters={filters}
            onFiltersChange={setFilters}
            toolbarEndActions={
              <div className="flex rounded-md border p-0.5" role="group" aria-label="Filter by status">
                {["", "Open", "Partial", "Settled", "Disputed"].map((s) => (
                  <button
                    key={s || "all"}
                    type="button"
                    onClick={() => setStatusFilter(s)}
                    className={
                      "rounded px-2.5 py-1 text-xs font-medium transition " +
                      (statusFilter === s
                        ? "bg-green-800 text-white"
                        : "text-muted-foreground hover:text-foreground")
                    }
                  >
                    {s || "All"}
                  </button>
                ))}
              </div>
            }
            emptyMessage="No commissions yet — open one from the Arrivals board (Add to Commission)."
          />
        </div>
      ) : null}

    </div>
  );
}
