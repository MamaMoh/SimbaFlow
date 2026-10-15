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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { TrackChip } from "@/components/workflow/status-update-sheet";
import { LmisRowActions } from "@/components/workflow/lmis-row-actions";
import { useLmisBoard, type LmisBoardRow } from "@/lib/api/lmis";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";
import { indexColumn } from "@/components/data-table/index-column";
import { identityColumns } from "@/components/data-table/identity-columns";
import { selectionColumn } from "@/components/data-table/selection-column";
import { BulkDownloadButton } from "@/components/workflow/bulk-download-button";

export default function LmisBoardPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("lmis.read") || hasPermission("system.admin");
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
  const [insurance, setInsurance] = useState<string>("all");
  const [milestone, setMilestone] = useState<string>("all");
  const [mirrorOnly, setMirrorOnly] = useState<string>("all");

  const { candidates, totalCount, isLoading, error, mutate, stageId } = useLmisBoard({
    search: search || undefined,
    filters: filterParam,
    insurance: insurance === "all" ? undefined : insurance,
    milestone: milestone === "all" ? undefined : milestone,
    mirrorOnly: mirrorOnly === "mirror" ? true : undefined,
    pageSize: 50,
  });

  const columns = useMemo<ColumnDef<LmisBoardRow>[]>(
    () => [
      ...(canSelect ? [selectionColumn<LmisBoardRow>()] : []),
      indexColumn<LmisBoardRow>(),
      {
        accessorKey: "fullName",
        // The field name the server's whitelist knows this column by. Insurance, Milestone and
        // the day counts have none: the first two live in a document on the candidate and the
        // rest are worked out per row, so they are asked for by the switches in the toolbar
        // instead of by a funnel that would quietly match nothing.
        meta: { filter: { field: "fullName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Name" />,
        cell: ({ row }) => (
          <div className="flex items-center gap-2">
            <NameCell href={`/candidates/${row.original.id}`} name={row.original.fullName} />
            {row.original.isMirror && (
              <Badge variant="outline" className="text-amber-700 border-amber-300 text-xs">
                Mirror
              </Badge>
            )}
          </div>
        ),
      },
      ...identityColumns<LmisBoardRow>(),
      {
        id: "insurance",
        header: "Insurance",
        cell: ({ row }) => (
          <TrackChip
            value={row.original.insurance ?? row.original.statusValues?.insurance}
          />
        ),
      },
      {
        id: "milestone",
        header: "Milestone",
        cell: ({ row }) => (
          <TrackChip
            value={row.original.milestone ?? row.original.statusValues?.milestone}
          />
        ),
      },
      {
        accessorKey: "source",
        header: "Source",
        cell: ({ row }) => row.original.source || (row.original.isMirror ? "Mirror" : "Primary"),
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
        id: "actions",
        header: () => <div className="text-center">Actions</div>,
        cell: ({ row }) => (
          <LmisRowActions candidate={row.original} onMutate={() => mutate()} stageId={stageId} />
        ),
      },
    ],
    [mutate, canSelect]
  );

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "lmis", columnIds });

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

  if (!canView) return <AccessDenied resource="LMIS board" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="LMIS"
        count={totalCount}
      />

      {error && (
        <LoadError
          message={error instanceof Error ? error.message : String(error)}
          onRetry={() => mutate()}
        />
      )}


      <div className="rounded-lg border bg-card p-4 shadow-sm">
        {isLoading ? (
          <div className="flex items-center justify-center py-12 text-muted-foreground">
            <Loader2 className="h-5 w-5 animate-spin mr-2" />
            Loading board…
          </div>
        ) : (
          <DataTable
            layout={layout}
            rowClickOpensActions
        exportFileName="lmis"
            exportActions={
              <BulkDownloadButton
                candidateIds={Object.keys(rowSelection).filter((id) => rowSelection[id])}
                onDownloaded={() => setRowSelection({})}
              />
            }
            table={table}
            enableGlobalFilter
            searchPlaceholder="Search name, passport or labour ID…"
            filters={filters}
            onFiltersChange={setFilters}
            paginated
            toolbarEndActions={
              /* Insurance and Milestone are status tracks and Source is about how a row got
                 here, so none of the three is a column the database can be asked about. They
                 are the board's own switches, which is why they sit here rather than under a
                 heading. */
              <div className="flex flex-wrap items-center gap-2">
                <Select value={insurance} onValueChange={setInsurance}>
                  <SelectTrigger className="h-8 w-[150px]" aria-label="Insurance">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">Any insurance</SelectItem>
                    <SelectItem value="Insurance Unpaid">Unpaid</SelectItem>
                    <SelectItem value="Insurance Paid">Paid</SelectItem>
                    <SelectItem value="Available">Available</SelectItem>
                  </SelectContent>
                </Select>
                <Select value={milestone} onValueChange={setMilestone}>
                  <SelectTrigger className="h-8 w-[150px]" aria-label="Milestone">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">Any milestone</SelectItem>
                    <SelectItem value="Uploaded">Uploaded</SelectItem>
                    <SelectItem value="Check Verified">Check Verified</SelectItem>
                    <SelectItem value="Issued">Issued</SelectItem>
                  </SelectContent>
                </Select>
                <Select value={mirrorOnly} onValueChange={setMirrorOnly}>
                  <SelectTrigger className="h-8 w-[160px]" aria-label="Source">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">Primary + Mirror</SelectItem>
                    <SelectItem value="mirror">Mirror only</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            }
            emptyMessage="No candidates on the LMIS board yet — mirror rows appear when Medical=Fit and Tasheer=Book Done; primary rows after “To LMIS”."
          />
        )}
      </div>
    </div>
  );
}
