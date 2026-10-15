"use client";

import { useMemo, useState } from "react";
import {
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  type ColumnDef,
} from "@tanstack/react-table";
import { AgeCell } from "@/components/data-table/age-cell";
import { DataTable } from "@/components/data-table/data-table";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import { useBoardQuery } from "@/lib/data-table/use-board-query";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { Badge } from "@/components/ui/badge";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { TrackChip } from "@/components/workflow/status-update-sheet";
import { ArrivalRowActions } from "@/components/workflow/arrival-row-actions";
import { useArrivalBoard, type ArrivalBoardRow } from "@/lib/api/arrival";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";
import { indexColumn } from "@/components/data-table/index-column";
import { identityColumns } from "@/components/data-table/identity-columns";
import { selectionColumn } from "@/components/data-table/selection-column";
import { BulkDownloadButton } from "@/components/workflow/bulk-download-button";

export default function ArrivalBoardPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("arrival.read") || hasPermission("system.admin");
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

  const { candidates, totalCount, isLoading, error, mutate, stageId } = useArrivalBoard({
    search: search || undefined,
    filters: filterParam,
    pageSize: 50,
  });

  const columns = useMemo<ColumnDef<ArrivalBoardRow>[]>(
    () => [
      ...(canSelect ? [selectionColumn<ArrivalBoardRow>()] : []),
      indexColumn<ArrivalBoardRow>(),
      {
        accessorKey: "fullName",
        // The field name the server's whitelist knows this column by. The status tracks and
        // the day counts below have none: they are read off a document on the candidate or
        // worked out per row, and a funnel that quietly matched nothing would be worse than
        // no funnel.
        meta: { filter: { field: "fullName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Name" />,
        cell: ({ row }) => (
          <NameCell href={`/candidates/${row.original.id}`} name={row.original.fullName} />
        ),
      },
      ...identityColumns<ArrivalBoardRow>(),
      {
        id: "destination",
        header: "Destination",
        cell: ({ row }) =>
          row.original.statusValues?.destination || row.original.countryOfTravel || "—",
      },
      {
        id: "arrival",
        header: "Status",
        cell: ({ row }) => (
          <TrackChip value={row.original.statusValues?.arrival} />
        ),
      },
      {
        accessorKey: "daysInStage",
        header: "In stage",
        cell: ({ getValue }) => <AgeCell days={getValue() as number} />,
      },
      {
        accessorKey: "daysSinceRegistered",
        header: "Case age",
        cell: ({ getValue }) => (
          <AgeCell days={getValue() as number} title="Days since the candidate was registered" />
        ),
      },
      {
        id: "flags",
        header: "",
        cell: ({ row }) => (
          <div className="flex flex-wrap gap-1">
            {row.original.commissionLinked && (
              <Badge variant="outline" className="text-xs">
                Commission
              </Badge>
            )}
            {row.original.hasOpenException && (
              <Badge variant="destructive" className="text-xs">
                Exception
              </Badge>
            )}
          </div>
        ),
      },
      {
        id: "actions",
        header: () => <div className="text-center">Actions</div>,
        cell: ({ row }) => (
          <ArrivalRowActions candidate={row.original} onMutate={() => mutate()} stageId={stageId} />
        ),
      },
    ],
    [mutate, canSelect]
  );

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "arrivals", columnIds });

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

  if (!canView) return <AccessDenied resource="Arrival board" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Arrivals"
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
        exportFileName="arrivals"
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
              emptyMessage="No arrivals yet — candidates appear here after “To Arrival” from Departures."
            />
          )}
        </div>
      )}
    </div>
  );
}
