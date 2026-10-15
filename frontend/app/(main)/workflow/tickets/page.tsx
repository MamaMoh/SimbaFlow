"use client";

import { PendingCell } from "@/components/data-table/pending-cell";

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
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { TrackChip } from "@/components/workflow/status-update-sheet";
import { TravelRowActions } from "@/components/workflow/travel-row-actions";
import { useTicketBoard, type TravelBoardRow } from "@/lib/api/travel";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";
import { indexColumn } from "@/components/data-table/index-column";
import { identityColumns } from "@/components/data-table/identity-columns";
import { selectionColumn } from "@/components/data-table/selection-column";
import { BulkDownloadButton } from "@/components/workflow/bulk-download-button";

export default function TicketBoardPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("travel.read") || hasPermission("system.admin");
  // Ticking rows is only worth anything if something can be done with the selection, and the one
  // batch action — downloading their paperwork — is read under candidate.read. Reaching this board
  // is a different permission, so without it the column is dead weight in every row.
  const canSelect = hasPermission("candidate.read");
  // The search box in the toolbar and the funnels under the headings. Both are answered by
  // the API: narrowing the page the browser happens to be holding would answer a different
  // question from the one being asked.
  const { globalFilter, setGlobalFilter, search, filters, setFilters, filterParam } =
    useBoardQuery();
  const [rowSelection, setRowSelection] = useState<Record<string, boolean>>({});

  const { candidates, totalCount, isLoading, error, mutate, stageId } = useTicketBoard({
    search: search || undefined,
    filters: filterParam,
    pageSize: 50,
  });

  const columns = useMemo<ColumnDef<TravelBoardRow>[]>(
    () => [
      ...(canSelect ? [selectionColumn<TravelBoardRow>()] : []),
      indexColumn<TravelBoardRow>(),
      {
        accessorKey: "fullName",
        // The field name the server's whitelist knows this column by. A column without one
        // gets no funnel, which is the right answer for the status tracks below: they live in
        // a document on the candidate rather than in a column, and a funnel that quietly
        // matched nothing would be worse than none.
        meta: { filter: { field: "fullName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Name" />,
        cell: ({ row }) => (
          <NameCell href={`/candidates/${row.original.id}`} name={row.original.fullName} />
        ),
      },
      ...identityColumns<TravelBoardRow>(),
      {
        id: "destination",
        header: "Destination",
        cell: ({ row }) =>
          row.original.statusValues?.destination || row.original.countryOfTravel || "—",
      },
      {
        id: "flightDate",
        header: "Flight date",
        cell: ({ row }) => (
          <PendingCell
            value={row.original.statusValues?.flight_date}
            pendingLabel="Not booked"
          />
        ),
      },
      {
        id: "ticket",
        header: "Ticket",
        cell: ({ row }) => (
          <TrackChip value={row.original.statusValues?.ticket_status} />
        ),
      },
      {
        accessorKey: "partnerName",
        meta: { filter: { field: "partnerName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Partner" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        id: "actions",
        header: () => <div className="text-center">Actions</div>,
        cell: ({ row }) => (
          <TravelRowActions
            stageId={stageId}
            candidate={row.original}
            onMutate={() => mutate()}
            board="ticket"
          />
        ),
      },
    ],
    [mutate, canSelect]
  );

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "tickets", columnIds });

  const table = useReactTable({
    state: {
      globalFilter,
      rowSelection,
      columnOrder: layout.columnOrder,
      columnVisibility: layout.columnVisibility,
    },
    // The search box belongs to the API. Left as TanStack's own it would narrow the rows the
    // API already chose, hiding the ones it matched on a column that is not on screen.
    globalFilterFn: () => true,
    onGlobalFilterChange: setGlobalFilter,
    onRowSelectionChange: setRowSelection,
    getRowId: (row) => row.id,
    enableRowSelection: true,
    data: candidates,
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

  if (!canView) return <AccessDenied resource="Ticket board" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Tickets"
        count={totalCount}
      />

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
            rowClickOpensActions
        exportFileName="tickets"
            exportActions={
              <BulkDownloadButton
                candidateIds={Object.keys(rowSelection).filter((id) => rowSelection[id])}
                onDownloaded={() => setRowSelection({})}
              />
            }
              table={table}
              paginated
              enableGlobalFilter
              searchPlaceholder="Search name, passport or labour ID…"
              filters={filters}
              onFiltersChange={setFilters}
              emptyMessage="No candidates awaiting tickets — they appear here after “To Ticket” from LMIS."
            />
          )}
        </div>
      )}
    </div>
  );
}
