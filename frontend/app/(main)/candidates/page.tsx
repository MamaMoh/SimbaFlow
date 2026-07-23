"use client";

import { useMemo, useState } from "react";
import useSWR from "swr";
import Link from "next/link";
import { useRouter } from "next/navigation";
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
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { PipelineTracker } from "@/components/workflow/pipeline-tracker";
import { FlagBadge, StatusPill, TimingChip } from "@/components/workflow/status-pill";
import { ContentLoading } from "@/components/loading/loading-components";
import { Button } from "@/components/ui/button";
import { DeleteDialog } from "@/components/ui/delete-dialog";
import { EditCandidateSheet } from "@/components/candidates/edit-candidate-sheet";
import { candidatesApi, USE_MOCKS } from "@/lib/api/candidates-api";
import type { CandidateListItem } from "@/types/candidate";
import { DEMO_STAGES } from "@/lib/demo/demo-data";
import { AlertTriangle, Eye, Pencil, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";

function stageHref(stageName?: string | null) {
  const match = DEMO_STAGES.find((s) => s.name === stageName);
  return match ? `/workflow/${match.slug}` : "/candidates";
}

export default function CandidatesPage() {
  const router = useRouter();
  const [sorting, setSorting] = useState<SortingState>([{ id: "registeredAt", desc: true }]);
  const [globalFilter, setGlobalFilter] = useState("");
  const [editTarget, setEditTarget] = useState<{ id: string; fullName: string } | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const { data, isLoading, mutate } = useSWR(
    ["candidates-all"],
    () => candidatesApi.list(),
    { revalidateOnFocus: false },
  );

  const candidates: CandidateListItem[] = data?.data?.items ?? [];

  const columns: ColumnDef<CandidateListItem>[] = useMemo(
    () => [
      {
        accessorKey: "fullName",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Name" />,
        cell: ({ row }) => (
          <div className="min-w-[140px]">
            <Link href={`/candidates/${row.original.id}`} className="font-semibold hover:underline">
              {row.original.fullName}
            </Link>
            {row.original.isOverdue && (
              <div className="mt-1">
                <FlagBadge tone="danger">
                  <AlertTriangle className="h-3 w-3" /> Stuck
                </FlagBadge>
              </div>
            )}
          </div>
        ),
      },
      {
        accessorKey: "applicationNo",
        header: ({ column }) => <DataTableColumnHeader column={column} title="App #" />,
        cell: ({ getValue }) => <span className="font-mono text-xs">{(getValue() as string) || "—"}</span>,
      },
      {
        accessorKey: "passportNumber",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Passport" />,
        cell: ({ getValue }) => <span className="font-mono text-xs">{getValue() as string}</span>,
      },
      {
        accessorKey: "labourId",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Labour ID" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "currentStageName",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Stage" />,
        cell: ({ row }) => (
          <Link href={stageHref(row.original.currentStageName)} className="inline-flex">
            <StatusPill value={row.original.currentStageName || "Intake"} size="sm" />
          </Link>
        ),
      },
      {
        accessorKey: "countryOfTravel",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Destination" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "officeName",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Office" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "daysInStage",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Days in stage" />,
        cell: ({ row }) => (
          <TimingChip days={row.original.daysInStage ?? 0} overdue={row.original.isOverdue} />
        ),
      },
      {
        accessorKey: "lastActionLabel",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Last action" />,
        cell: ({ getValue }) => <span className="text-xs">{(getValue() as string) || "—"}</span>,
      },
      {
        accessorKey: "registeredAt",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Registered" />,
        cell: ({ getValue }) => new Date(getValue() as string).toLocaleDateString(),
      },
      {
        id: "actions",
        header: "Actions",
        cell: ({ row }) => (
          <div className="flex items-center gap-0.5">
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8"
              title="View"
              onClick={() => router.push(`/candidates/${row.original.id}`)}
            >
              <Eye className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8"
              title="Edit"
              onClick={() => setEditTarget({ id: row.original.id, fullName: row.original.fullName })}
            >
              <Pencil className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 text-destructive"
              title="Delete"
              onClick={() => setDeleteTarget({ id: row.original.id, name: row.original.fullName })}
            >
              <Trash2 className="h-4 w-4" />
            </Button>
          </div>
        ),
        enableSorting: false,
      },
    ],
    [router],
  );

  const table = useReactTable({
    data: candidates,
    columns,
    state: { sorting, globalFilter },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    initialState: { pagination: { pageSize: 12 } },
  });

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setIsDeleting(true);
    const result = await candidatesApi.remove(deleteTarget.id);
    if (result.isSuccess) {
      toast.success("Candidate deleted");
      mutate();
    } else {
      toast.error(result.error || "Delete failed");
    }
    setIsDeleting(false);
    setDeleteTarget(null);
  };

  return (
    <div className="flex flex-col gap-5 p-4 md:p-6">
      <PipelineTracker />

      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Candidates</h1>
          <p className="text-sm text-muted-foreground">
            {candidates.length} total{USE_MOCKS ? " · demo dataset" : ""}
          </p>
        </div>
        <Button asChild className="gap-1">
          <Link href="/candidates/register">
            <Plus className="h-4 w-4" /> Register candidate
          </Link>
        </Button>
      </div>

      <div className="rounded-xl border bg-gradient-to-b from-card to-muted/20 p-3 shadow-sm">
        {isLoading ? (
          <ContentLoading text="Loading candidates…" />
        ) : (
          <DataTable
            table={table}
            enableGlobalFilter
            dense
            searchPlaceholder="Search across all fields…"
          />
        )}
      </div>

      <EditCandidateSheet
        candidate={editTarget}
        open={!!editTarget}
        onOpenChange={(open) => !open && setEditTarget(null)}
        onUpdated={() => mutate()}
      />

      <DeleteDialog
        open={!!deleteTarget}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete candidate"
        description={`Remove '${deleteTarget?.name}' from the pipeline?`}
        onConfirm={confirmDelete}
        isDeleting={isDeleting}
      />
    </div>
  );
}
