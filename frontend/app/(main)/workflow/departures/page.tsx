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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { TrackChip } from "@/components/workflow/status-update-sheet";
import { RemainingDaysBadge } from "@/components/workflow/remaining-days-badge";
import { TravelRowActions } from "@/components/workflow/travel-row-actions";
import { useDepartureBoard, type TravelBoardRow } from "@/lib/api/travel";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";
import { indexColumn } from "@/components/data-table/index-column";

export default function DepartureBoardPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("travel.read") || hasPermission("system.admin");
  const [search, setSearch] = useState("");
  const [includeCanceled, setIncludeCanceled] = useState(false);

  const { candidates, totalCount, isLoading, error, mutate } = useDepartureBoard({
    search: search || undefined,
    pageSize: 50,
    includeCanceled,
  });

  const columns = useMemo<ColumnDef<TravelBoardRow>[]>(
    () => [
      indexColumn<TravelBoardRow>(),
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
        id: "flightDate",
        header: "Flight",
        cell: ({ row }) => row.original.statusValues?.flight_date || "—",
      },
      {
        id: "remaining",
        header: "Remaining",
        cell: ({ row }) => <RemainingDaysBadge days={row.original.remainingDays} />,
      },
      {
        id: "notification",
        header: "Notify",
        cell: ({ row }) => (
          <TrackChip label="notify" value={row.original.statusValues?.notification_status} />
        ),
      },
      {
        id: "departure",
        header: "Status",
        cell: ({ row }) => (
          <TrackChip
            label="dep"
            value={
              row.original.isCanceled
                ? "Canceled"
                : row.original.statusValues?.departure_status
            }
          />
        ),
      },
      {
        id: "actions",
        header: () => <div className="text-center">Actions</div>,
        cell: ({ row }) => (
          <TravelRowActions
            candidate={row.original}
            onMutate={() => mutate()}
            board="departure"
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

  if (!canView) return <AccessDenied resource="Departure board" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Departures"
        description={<>Countdown by flight date · {totalCount} candidate{totalCount === 1 ? "" : "s"}</>}
        actions={
          <div className="flex flex-wrap items-center gap-4">
            <div className="flex items-center gap-2">
              <Checkbox
                id="show-canceled"
                checked={includeCanceled}
                onCheckedChange={(v) => setIncludeCanceled(v === true)}
              />
              <Label htmlFor="show-canceled" className="text-sm font-normal">
                Show canceled
              </Label>
            </div>
            <Input
              className="max-w-xs"
              placeholder="Search name or passport…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
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
        exportFileName="departures" table={table} paginated emptyMessage="No departures scheduled yet — candidates appear here after “To Departure” from Tickets." />
          )}
        </div>
      )}
    </div>
  );
}
