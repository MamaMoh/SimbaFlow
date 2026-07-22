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
import { StatusPill } from "@/components/workflow/status-pill";
import { ContentLoading } from "@/components/loading/loading-components";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { candidatesApi, USE_MOCKS } from "@/lib/api/candidates-api";
import type { CandidateListItem } from "@/types/candidate";
import { DEMO_STAGES } from "@/lib/demo/demo-data";
import { AlertTriangle, Clock3, Eye, Plus } from "lucide-react";
import { cn } from "@/lib/utils";

function stageHref(stageName?: string | null) {
  const match = DEMO_STAGES.find((s) => s.name === stageName);
  return match ? `/workflow/${match.slug}` : "/candidates";
}

export default function CandidatesPage() {
  const router = useRouter();
  const [sorting, setSorting] = useState<SortingState>([{ id: "registeredAt", desc: true }]);
  const [globalFilter, setGlobalFilter] = useState("");

  const { data, isLoading } = useSWR(
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
          <div>
            <Link href={`/candidates/${row.original.id}`} className="font-semibold hover:underline">
              {row.original.fullName}
            </Link>
            {row.original.isOverdue && (
              <Badge variant="destructive" className="ml-2 text-[10px] gap-0.5">
                <AlertTriangle className="h-3 w-3" /> Stuck
              </Badge>
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
          <Link href={stageHref(row.original.currentStageName)}>
            <StatusPill value={row.original.currentStageName || "Intake"} />
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
          <span
            className={cn(
              "inline-flex items-center gap-1 rounded-md px-2 py-0.5 text-xs font-bold tabular-nums",
              row.original.isOverdue ? "bg-rose-50 text-rose-700" : "bg-slate-100 text-slate-700",
            )}
          >
            <Clock3 className="h-3 w-3" />
            {row.original.daysInStage ?? 0}d
          </span>
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
        header: "",
        cell: ({ row }) => (
          <Button variant="ghost" size="icon" className="h-8 w-8" onClick={() => router.push(`/candidates/${row.original.id}`)}>
            <Eye className="h-4 w-4" />
          </Button>
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
    </div>
  );
}
