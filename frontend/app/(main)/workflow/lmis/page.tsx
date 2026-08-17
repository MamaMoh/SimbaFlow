"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import {
  useReactTable,
  getCoreRowModel,
  getPaginationRowModel,
  type ColumnDef,
} from "@tanstack/react-table";
import { DataTable } from "@/components/data-table/data-table";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { TrackChip } from "@/components/workflow/status-update-sheet";
import { LmisRowActions } from "@/components/workflow/lmis-row-actions";
import { useLmisBoard, type LmisBoardRow } from "@/lib/api/lmis";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";
import { indexColumn } from "@/components/data-table/index-column";

export default function LmisBoardPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("lmis.read") || hasPermission("system.admin");
  const [search, setSearch] = useState("");
  const [insurance, setInsurance] = useState<string>("all");
  const [milestone, setMilestone] = useState<string>("all");
  const [mirrorOnly, setMirrorOnly] = useState<string>("all");

  const { candidates, totalCount, isLoading, error, mutate } = useLmisBoard({
    search: search || undefined,
    insurance: insurance === "all" ? undefined : insurance,
    milestone: milestone === "all" ? undefined : milestone,
    mirrorOnly: mirrorOnly === "mirror" ? true : undefined,
    pageSize: 50,
  });

  const columns = useMemo<ColumnDef<LmisBoardRow>[]>(
    () => [
      indexColumn<LmisBoardRow>(),
      {
        accessorKey: "fullName",
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
      {
        accessorKey: "passportNumber",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Passport" />,
      },
      {
        id: "insurance",
        header: "Insurance",
        cell: ({ row }) => (
          <TrackChip
            label="insurance"
            value={row.original.insurance ?? row.original.statusValues?.insurance}
          />
        ),
      },
      {
        id: "milestone",
        header: "Milestone",
        cell: ({ row }) => (
          <TrackChip
            label="milestone"
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
        header: "Days",
        cell: ({ getValue }) => getValue() as number,
      },
      {
        id: "actions",
        header: () => <div className="text-center">Actions</div>,
        cell: ({ row }) => (
          <LmisRowActions candidate={row.original} onMutate={() => mutate()} />
        ),
      },
    ],
    [mutate]
  );

  const table = useReactTable({
    data: candidates,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    initialState: { pagination: { pageSize: 20 } },
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
        description={<>Insurance &amp; milestones · {totalCount} candidate{totalCount === 1 ? "" : "s"}</>}
        actions={
          <Input
            className="max-w-xs"
            placeholder="Search…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        }
      />

      <div className="flex flex-wrap gap-4 items-end">
        <div className="space-y-1">
          <Label className="text-xs">Insurance</Label>
          <Select value={insurance} onValueChange={setInsurance}>
            <SelectTrigger className="w-[180px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All</SelectItem>
              <SelectItem value="Insurance Unpaid">Unpaid</SelectItem>
              <SelectItem value="Insurance Paid">Paid</SelectItem>
              <SelectItem value="Available">Available</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Milestone</Label>
          <Select value={milestone} onValueChange={setMilestone}>
            <SelectTrigger className="w-[180px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All</SelectItem>
              <SelectItem value="Uploaded">Uploaded</SelectItem>
              <SelectItem value="Check Verified">Check Verified</SelectItem>
              <SelectItem value="Issued">Issued</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Source</Label>
          <Select value={mirrorOnly} onValueChange={setMirrorOnly}>
            <SelectTrigger className="w-[160px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Primary + Mirror</SelectItem>
              <SelectItem value="mirror">Mirror only</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>

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
            table={table}
            enableGlobalFilter={false}
            paginated
            emptyMessage="No candidates on the LMIS board yet — mirror rows appear when Medical=Fit and Tasheer=Book Done; primary rows after “To LMIS”."
          />
        )}
      </div>
    </div>
  );
}
