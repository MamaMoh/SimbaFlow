"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import {
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  type ColumnDef,
  type SortingState,
} from "@tanstack/react-table";
import { withdrawFromPipeline } from "@/lib/api/candidates";
import { MoveBackStageItem } from "@/components/workflow/move-back-stage-item";
import { MarkReadyDialog } from "@/components/workflow/mark-ready-dialog";
import { DataTable } from "@/components/data-table/data-table";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import type { ColumnFilter } from "@/lib/data-table/column-filters";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { CandidateStatusBadge } from "@/components/workflow/candidate-status-badge";
import { WorkflowActionItems } from "@/components/workflow/workflow-action-items";
import { useAvailableActions, type ViewCandidateDto } from "@/lib/api/workflow";
import { Button } from "@/components/ui/button";
import { Check, Eye, MoreHorizontal, Undo2 } from "lucide-react";
import { toast } from "sonner";
import { NameCell } from "@/components/data-table/name-cell";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { indexColumn } from "@/components/data-table/index-column";
import { identityColumns } from "@/components/data-table/identity-columns";
import { usePermissions } from "@/lib/tenant/tenant-provider";

type WorkflowViewTableProps = {
  candidates: ViewCandidateDto[];
  isLoading?: boolean;
  onMutate: () => void;
  statusTracks?: string[];
  /**
   * The board this table is standing in for, so the arrangement is remembered per stage.
   *
   * Every agency-added stage shares this one component but not its columns — the status
   * tracks differ per stage — so one remembered arrangement for all of them would put a
   * column somebody moved on one stage onto a stage that does not have it.
   */
  layoutKey?: string;
  /** The toolbar's search box and the funnel row, both answered by the API. */
  search: {
    globalFilter: string;
    setGlobalFilter: (next: string) => void;
    filters: ColumnFilter[];
    setFilters: (next: ColumnFilter[]) => void;
  };
};

function RowActions({
  candidate,
  onMutate,
}: {
  candidate: ViewCandidateDto;
  onMutate: () => void;
}) {
  const { hasPermission } = usePermissions();
  const canRead = hasPermission("candidate.read");
  // Marking Ready and taking a candidate off the pipeline both go through the workflow, so they
  // take the permission that moves candidates rather than the one that lists them.
  const canExecute = hasPermission("workflow.execute");
  const { actions, mutate: mutateActions } = useAvailableActions(candidate.id);

  const stageName = (candidate.currentStageName ?? "").toLowerCase();
  const isNewContracts = stageName.includes("new contract");
  const currentStatus = candidate.statusValues?.status ?? "";
  const needsReady = isNewContracts && currentStatus.toLowerCase() !== "ready";

  const refresh = () => {
    mutateActions();
    onMutate();
  };

  const withdraw = async () => {
    try {
      await withdrawFromPipeline(candidate.id);
      toast.success("Back in intake — off every board");
      refresh();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not take the candidate off");
    }
  };

  const [readyOpen, setReadyOpen] = useState(false);

  if (!canRead && !canExecute) return null;

  return (
    <div className="flex justify-center">
      <MarkReadyDialog
        open={readyOpen}
        onOpenChange={setReadyOpen}
        candidateId={candidate.id}
        candidateName={candidate.fullName}
        countryOfTravel={candidate.countryOfTravel}
        onDone={refresh}
      />
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="sm" className="h-8 w-8 p-0" aria-label="Row actions">
            <MoreHorizontal className="h-4 w-4" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="z-[200] w-56">
          {canRead && (
            <DropdownMenuItem asChild>
              <Link href={`/candidates/${candidate.id}`}>
                <Eye className="mr-2 h-4 w-4" />
                View details
              </Link>
            </DropdownMenuItem>
          )}
          {canExecute && needsReady ? (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={() => setReadyOpen(true)}>
                <Check className="mr-2 h-4 w-4" />
                Mark Ready
              </DropdownMenuItem>
            </>
          ) : null}
          {canExecute && (
            <>
              <DropdownMenuSeparator />
              <MoveBackStageItem
                candidateId={candidate.id}
                candidateName={candidate.fullName}
                stageId={candidate.currentStageId ?? undefined}
                isMirror={candidate.isMirror}
                onDone={refresh}
              />
              <DropdownMenuItem
                onSelect={(e) => {
                  e.preventDefault();
                  void withdraw();
                }}
              >
                <Undo2 className="mr-2 h-4 w-4" />
                Take off the pipeline
              </DropdownMenuItem>
            </>
          )}
          <WorkflowActionItems
            candidateId={candidate.id}
            actions={actions}
            onExecuted={refresh}
            separatorBefore
          />
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}

export function WorkflowViewTable({
  candidates,
  isLoading,
  onMutate,
  statusTracks = [],
  layoutKey = "stage",
  search,
}: WorkflowViewTableProps) {
  const [sorting, setSorting] = useState<SortingState>([]);

  const columns: ColumnDef<ViewCandidateDto>[] = [
    indexColumn<ViewCandidateDto>(),
    {
      accessorKey: "fullName",
      // The field name the server's whitelist knows this column by. Stage/Status and the
      // per-stage tracks have none: they live in a document on the candidate rather than in a
      // column, and a funnel that quietly matched nothing would be worse than no funnel.
      meta: { filter: { field: "fullName", kind: "text" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Name" />,
      cell: ({ row }) => (
        <NameCell href={`/candidates/${row.original.id}`} name={row.original.fullName} />
      ),
    },
    ...identityColumns<ViewCandidateDto>(),
    {
      id: "stage",
      header: "Stage / Status",
      cell: ({ row }) => (
        <CandidateStatusBadge
          stageName={row.original.currentStageName}
          statusValues={row.original.statusValues}
          isMirror={row.original.isMirror}
        />
      ),
    },
    ...statusTracks.map(
      (track): ColumnDef<ViewCandidateDto> => ({
        id: `track-${track}`,
        header: track,
        cell: ({ row }) => row.original.statusValues?.[track] || "—",
      })
    ),
    {
      accessorKey: "countryOfTravel",
      meta: { filter: { field: "countryOfTravel", kind: "text" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Country" />,
      cell: ({ getValue }) => (getValue() as string | null) || "—",
    },
    {
      accessorKey: "partnerName",
      meta: { filter: { field: "partnerName", kind: "text" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Partner" />,
      cell: ({ getValue }) => (getValue() as string | null) || "—",
    },
    {
      id: "actions",
      header: () => <span className="block text-center">Actions</span>,
      cell: ({ row }) => (
        <RowActions candidate={row.original} onMutate={onMutate} />
      ),
    },
  ];

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(
    () => columns.map((c: any) => c.id ?? c.accessorKey),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [statusTracks.join("|")],
  );

  // How this person arranged this stage, remembered on this computer.
  const layout = useTableLayout({ table: layoutKey, columnIds });

  const table = useReactTable({
    data: candidates,
    columns,
    state: {
      sorting,
      globalFilter: search.globalFilter,
      columnOrder: layout.columnOrder,
      columnVisibility: layout.columnVisibility,
    },
    // The search box belongs to the API. Left as TanStack's own it would narrow the rows the
    // API already chose, hiding the ones it matched on a column that is not on screen.
    globalFilterFn: () => true,
    onGlobalFilterChange: search.setGlobalFilter,
    onSortingChange: setSorting,
    onColumnOrderChange: layout.setColumnOrder,
    onColumnVisibilityChange: layout.setColumnVisibility,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    initialState: { pagination: { pageSize: 20 } },
  });

  // silence unused isLoading until DataTable supports it
  useEffect(() => {}, [isLoading]);

  return (
    <DataTable
      rowClickOpensActions
      table={table}
      layout={layout}
      enableGlobalFilter
      searchPlaceholder="Search name, passport or labour ID…"
      exportFileName={layoutKey}
      filters={search.filters}
      onFiltersChange={search.setFilters}
      paginated={true}
      emptyMessage="No candidates in this stage yet — they appear here once registered or transitioned in."
    />
  );
}
