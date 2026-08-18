"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import {
  useReactTable,
  getCoreRowModel,
  getPaginationRowModel,
  type ColumnDef,
} from "@tanstack/react-table";
import { AgeCell } from "@/components/data-table/age-cell";
import { DataTable } from "@/components/data-table/data-table";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { Input } from "@/components/ui/input";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { TrackChip } from "@/components/workflow/status-update-sheet";
import { EmbassyRowActions } from "@/components/workflow/embassy-row-actions";
import { useCaseExecutiveBoard, type EmbassyBoardRow } from "@/lib/api/embassy";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";
import { indexColumn } from "@/components/data-table/index-column";

export default function CaseExecutiveBoardPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView =
    hasPermission("embassy.case_view") ||
    hasPermission("embassy.read") ||
    hasPermission("system.admin");
  const [search, setSearch] = useState("");

  const { candidates, totalCount, isLoading, error, mutate } = useCaseExecutiveBoard({
    search: search || undefined,
    pageSize: 50,
  });

  const columns = useMemo<ColumnDef<EmbassyBoardRow>[]>(
    () => [
      indexColumn<EmbassyBoardRow>(),
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
        id: "visa",
        header: "Visa",
        cell: ({ row }) => (
          <TrackChip label="visa" value={row.original.statusValues?.visa} />
        ),
      },
      {
        id: "context",
        header: "Clearances",
        cell: ({ row }) => (
          <div className="flex flex-wrap gap-1">
            <TrackChip label="medical" value={row.original.statusValues?.medical} />
            <TrackChip label="tasheer" value={row.original.statusValues?.tasheer} />
          </div>
        ),
      },
      {
        accessorKey: "daysInStage",
        header: "Waiting",
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
          <EmbassyRowActions
            candidate={row.original}
            onMutate={() => mutate()}
            variant="case-executive"
          />
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

  if (!canView) return <AccessDenied resource="Case Executive board" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Case Executive"
        description={<>Mirror board (visa Ready / Submitted) · {totalCount} case
{totalCount === 1 ? "" : "s"}</>}
        actions={
          <Input
            className="max-w-xs"
            placeholder="Search…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        }
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
        exportFileName="case-executive"
            table={table}
            enableGlobalFilter={false}
            paginated
            emptyMessage="No cases waiting — candidates appear here when Embassy sets visa to Ready."
          />
        )}
      </div>
    </div>
  );
}
