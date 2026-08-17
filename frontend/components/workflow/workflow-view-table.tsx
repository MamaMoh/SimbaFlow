"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
  useReactTable,
  getCoreRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  type ColumnDef,
  type SortingState,
} from "@tanstack/react-table";
import { DataTable } from "@/components/data-table/data-table";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { CandidateStatusBadge } from "@/components/workflow/candidate-status-badge";
import { WorkflowActionItems } from "@/components/workflow/workflow-action-items";
import {
  useAvailableActions,
  updateWorkflowStatus,
  type ViewCandidateDto,
} from "@/lib/api/workflow";
import { Button } from "@/components/ui/button";
import { Check, Eye, Loader2, MoreHorizontal } from "lucide-react";
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

type WorkflowViewTableProps = {
  candidates: ViewCandidateDto[];
  isLoading?: boolean;
  onMutate: () => void;
  statusTracks?: string[];
};

function RowActions({
  candidate,
  onMutate,
}: {
  candidate: ViewCandidateDto;
  onMutate: () => void;
}) {
  const { actions, mutate: mutateActions } = useAvailableActions(candidate.id);
  const [markingReady, setMarkingReady] = useState(false);

  const stageName = (candidate.currentStageName ?? "").toLowerCase();
  const isNewContracts = stageName.includes("new contract");
  const currentStatus = candidate.statusValues?.status ?? "";
  const needsReady = isNewContracts && currentStatus.toLowerCase() !== "ready";

  const refresh = () => {
    mutateActions();
    onMutate();
  };

  const markReady = async () => {
    setMarkingReady(true);
    try {
      await updateWorkflowStatus(candidate.id, "status", "Ready");
      toast.success("Marked Ready — To Embassy is now available");
      refresh();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not mark Ready");
    } finally {
      setMarkingReady(false);
    }
  };

  return (
    <div className="flex justify-center">
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="sm" className="h-8 w-8 p-0" aria-label="Row actions">
            <MoreHorizontal className="h-4 w-4" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="z-[200] w-56">
          <DropdownMenuItem asChild>
            <Link href={`/candidates/${candidate.id}`}>
              <Eye className="mr-2 h-4 w-4" />
              View details
            </Link>
          </DropdownMenuItem>
          {needsReady ? (
            <>
              <DropdownMenuSeparator />
              <DropdownMenuItem
                disabled={markingReady}
                onSelect={(e) => {
                  e.preventDefault();
                  void markReady();
                }}
              >
                {markingReady ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Check className="mr-2 h-4 w-4" />
                )}
                Mark Ready
              </DropdownMenuItem>
            </>
          ) : null}
          {actions.length > 0 && <DropdownMenuSeparator />}
          <WorkflowActionItems
            candidateId={candidate.id}
            actions={actions}
            onExecuted={refresh}
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
}: WorkflowViewTableProps) {
  const [sorting, setSorting] = useState<SortingState>([]);

  const columns: ColumnDef<ViewCandidateDto>[] = [
    indexColumn<ViewCandidateDto>(),
    {
      accessorKey: "fullName",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Name" />,
      cell: ({ row }) => (
        <NameCell href={`/candidates/${row.original.id}`} name={row.original.fullName} />
      ),
    },
    {
      accessorKey: "passportNumber",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Passport" />,
    },
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
      header: ({ column }) => <DataTableColumnHeader column={column} title="Country" />,
      cell: ({ getValue }) => (getValue() as string | null) || "—",
    },
    {
      accessorKey: "officeName",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Office" />,
      cell: ({ getValue }) => (getValue() as string | null) || "—",
    },
    {
      id: "actions",
      header: () => <span className="sr-only">Actions</span>,
      cell: ({ row }) => (
        <RowActions candidate={row.original} onMutate={onMutate} />
      ),
    },
  ];

  const table = useReactTable({
    data: candidates,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    initialState: { pagination: { pageSize: 20 } },
  });

  // silence unused isLoading until DataTable supports it
  useEffect(() => {}, [isLoading]);

  return (
    <DataTable
      table={table}
      enableGlobalFilter={false}
      paginated={true}
      emptyMessage="No candidates in this stage yet — they appear here once registered or transitioned in."
    />
  );
}
