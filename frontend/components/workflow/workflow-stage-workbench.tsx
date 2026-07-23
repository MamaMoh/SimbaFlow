"use client";

import { useCallback, useMemo, useState } from "react";
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
import {
  FlagBadge,
  StatusTrackGroup,
  TimingChip,
} from "@/components/workflow/status-pill";
import { ContentLoading } from "@/components/loading/loading-components";
import { Button } from "@/components/ui/button";
import type { WorkflowViewRow } from "@/types/workflow";
import { AlertTriangle, Eye } from "lucide-react";
import { cn } from "@/lib/utils";
import { useNavigationLoadingStore } from "@/lib/stores/navigation-loading-store";

function candidateHref(id: string) {
  return `/candidates/${id.replace(/-preview$/, "")}`;
}

export function WorkflowStageWorkbench({
  stageName,
  stageSlug,
  rows,
  isLoading,
  onAction,
}: {
  stageName: string;
  stageSlug: string;
  rows: WorkflowViewRow[];
  isLoading?: boolean;
  onAction?: (candidateId: string, actionId: string) => void | Promise<void>;
}) {
  const router = useRouter();
  const setLoading = useNavigationLoadingStore((s) => s.setLoading);
  const [sorting, setSorting] = useState<SortingState>([{ id: "daysInStage", desc: true }]);
  const [globalFilter, setGlobalFilter] = useState("");

  const overdueCount = rows.filter((r) => r.isOverdue).length;
  const previewCount = rows.filter((r) => r.isPreview).length;

  const openCandidate = useCallback(
    (id: string) => {
      setLoading(true);
      router.push(candidateHref(id));
    },
    [router, setLoading],
  );

  const columns: ColumnDef<WorkflowViewRow>[] = useMemo(
    () => [
      {
        accessorKey: "fullName",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Candidate" />,
        cell: ({ row }) => (
          <div className="min-w-[160px]">
            <button
              type="button"
              onClick={() => openCandidate(row.original.id)}
              className="text-left font-semibold text-foreground hover:underline"
            >
              {row.original.fullName}
            </button>
            {(row.original.isPreview || row.original.isOverdue) && (
              <div className="mt-1 flex flex-wrap gap-1">
                {row.original.isPreview && <FlagBadge tone="info">Mirror</FlagBadge>}
                {row.original.isOverdue && (
                  <FlagBadge tone="danger">
                    <AlertTriangle className="h-3 w-3" /> Overdue
                  </FlagBadge>
                )}
              </div>
            )}
          </div>
        ),
        filterFn: "includesString",
      },
      {
        accessorKey: "applicationNo",
        header: ({ column }) => <DataTableColumnHeader column={column} title="App #" />,
        cell: ({ getValue }) => <span className="font-mono text-xs">{getValue() as string}</span>,
      },
      {
        accessorKey: "passportNumber",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Passport" />,
        cell: ({ getValue }) => <span className="font-mono text-xs">{getValue() as string}</span>,
      },
      {
        accessorKey: "labourId",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Labour ID" />,
        cell: ({ getValue }) => getValue() || "—",
      },
      {
        accessorKey: "countryOfTravel",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Destination" />,
      },
      {
        accessorKey: "sponsorName",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Sponsor / Office" />,
        cell: ({ row }) => (
          <div className="min-w-[120px]">
            <div>{row.original.sponsorName || "—"}</div>
            <div className="text-[11px] text-muted-foreground">{row.original.officeName}</div>
          </div>
        ),
      },
      {
        id: "statuses",
        accessorFn: (row) =>
          row.tracks.map((t) => `${t.trackKey}:${t.status ?? ""}`).join(" ") ||
          Object.entries(row.currentStatusValues || {})
            .map(([k, v]) => `${k}:${v}`)
            .join(" "),
        header: ({ column }) => <DataTableColumnHeader column={column} title="Status tracks" />,
        cell: ({ row }) => {
          const fromTracks = row.original.tracks.map((t) => ({
            key: t.trackKey,
            label: t.trackKey,
            value: t.status,
            sinceDays: t.daysOnStep,
          }));
          const fromValues = Object.entries(row.original.currentStatusValues || {}).map(([k, v]) => ({
            key: k,
            label: k,
            value: v,
          }));
          return <StatusTrackGroup items={fromTracks.length ? fromTracks : fromValues} max={2} />;
        },
        enableSorting: false,
      },
      {
        accessorKey: "enteredAt",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Entered" />,
        cell: ({ getValue }) => {
          const v = getValue() as string | undefined;
          return v ? new Date(v).toLocaleDateString() : "—";
        },
      },
      {
        accessorKey: "daysInStage",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Days" />,
        cell: ({ row }) => (
          <TimingChip days={row.original.daysInStage} overdue={row.original.isOverdue} />
        ),
      },
      {
        accessorKey: "lastActionLabel",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Last action" />,
        cell: ({ row }) => (
          <div className="min-w-[140px]">
            <div className="text-xs font-medium">{row.original.lastActionLabel || "—"}</div>
            {row.original.remainingDays != null && (
              <div
                className={cn(
                  "text-[11px]",
                  row.original.remainingDays <= 2 ? "text-rose-600 font-semibold" : "text-muted-foreground",
                )}
              >
                Flight in {row.original.remainingDays}d
              </div>
            )}
          </div>
        ),
      },
      {
        id: "actions",
        header: "Actions",
        cell: ({ row }) => (
          <div className="flex flex-wrap items-center gap-1 min-w-[140px]">
            <Button
              variant="ghost"
              size="icon"
              className="h-7 w-7"
              title="View candidate"
              onClick={(e) => {
                e.stopPropagation();
                openCandidate(row.original.id);
              }}
            >
              <Eye className="h-3.5 w-3.5" />
            </Button>
            {row.original.availableActions.map((a) => (
              <Button
                key={a.transitionRuleId}
                size="sm"
                className="h-7 text-xs"
                variant={a.isEnabled ? "default" : "outline"}
                disabled={!a.isEnabled || !onAction}
                title={a.disabledReason}
                onClick={(e) => {
                  e.stopPropagation();
                  onAction?.(row.original.id, a.transitionRuleId);
                }}
              >
                {a.buttonLabel}
              </Button>
            ))}
          </div>
        ),
        enableSorting: false,
      },
    ],
    [onAction, openCandidate],
  );

  const table = useReactTable({
    data: rows,
    columns,
    state: { sorting, globalFilter },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    initialState: { pagination: { pageSize: 10 } },
  });

  return (
    <div className="flex flex-col gap-5">
      <PipelineTracker activeSlug={stageSlug} />

      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">{stageName}</h1>
          <p className="text-sm text-muted-foreground">
            {rows.length} candidates
            {previewCount > 0 ? ` · ${previewCount} mirror previews` : ""}
            {overdueCount > 0 ? ` · ${overdueCount} overdue` : ""}
            {" · "}live timing
          </p>
        </div>
        <div className="flex gap-2">
          <div className="rounded-lg border bg-card px-3 py-2 text-center shadow-sm">
            <div className="text-[10px] uppercase text-muted-foreground">In view</div>
            <div className="text-lg font-bold tabular-nums">{rows.length}</div>
          </div>
          <div className="rounded-lg border bg-card px-3 py-2 text-center shadow-sm">
            <div className="text-[10px] uppercase text-muted-foreground">Overdue</div>
            <div className={cn("text-lg font-bold tabular-nums", overdueCount ? "text-rose-600" : "")}>
              {overdueCount}
            </div>
          </div>
        </div>
      </div>

      <div className="rounded-xl border bg-gradient-to-b from-card to-muted/20 p-3 shadow-sm">
        {isLoading ? (
          <ContentLoading text="Loading stage…" />
        ) : (
          <DataTable
            table={table}
            enableGlobalFilter
            dense
            searchPlaceholder="Search name, passport, labour ID, status…"
            emptyMessage="No candidates in this stage yet."
          />
        )}
      </div>
    </div>
  );
}
