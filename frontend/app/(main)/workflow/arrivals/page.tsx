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
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { TrackChip } from "@/components/workflow/status-update-sheet";
import { ArrivalRowActions } from "@/components/workflow/arrival-row-actions";
import { useArrivalBoard, type ArrivalBoardRow } from "@/lib/api/arrival";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";
import { indexColumn } from "@/components/data-table/index-column";

export default function ArrivalBoardPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("arrival.read") || hasPermission("system.admin");
  const [search, setSearch] = useState("");

  const { candidates, totalCount, isLoading, error, mutate } = useArrivalBoard({
    search: search || undefined,
    pageSize: 50,
  });

  const columns = useMemo<ColumnDef<ArrivalBoardRow>[]>(
    () => [
      indexColumn<ArrivalBoardRow>(),
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
        id: "destination",
        header: "Destination",
        cell: ({ row }) =>
          row.original.statusValues?.destination || row.original.countryOfTravel || "—",
      },
      {
        id: "arrival",
        header: "Status",
        cell: ({ row }) => (
          <TrackChip label="arrival" value={row.original.statusValues?.arrival} />
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
          <ArrivalRowActions candidate={row.original} onMutate={() => mutate()} />
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

  if (!canView) return <AccessDenied resource="Arrival board" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Arrivals"
        description={<>Permanent ledger · {totalCount} candidate{totalCount === 1 ? "" : "s"}</>}
        actions={
          <Input
            className="max-w-xs"
            placeholder="Search name or passport…"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        }
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
        exportFileName="arrivals" table={table} paginated emptyMessage="No arrivals yet — candidates appear here after “To Arrival” from Departures." />
          )}
        </div>
      )}
    </div>
  );
}
