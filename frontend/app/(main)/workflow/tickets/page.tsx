"use client";

import { PendingCell } from "@/components/data-table/pending-cell";

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
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { TrackChip } from "@/components/workflow/status-update-sheet";
import { TravelRowActions } from "@/components/workflow/travel-row-actions";
import { useTicketBoard, type TravelBoardRow } from "@/lib/api/travel";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";
import { indexColumn } from "@/components/data-table/index-column";

export default function TicketBoardPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("travel.read") || hasPermission("system.admin");
  const [search, setSearch] = useState("");

  const { candidates, totalCount, isLoading, error, mutate } = useTicketBoard({
    search: search || undefined,
    pageSize: 50,
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
        header: "Flight date",
        cell: ({ row }) => (
          <PendingCell
            value={row.original.statusValues?.flight_date}
            pendingLabel="Not booked"
          />
        ),
      },
      {
        id: "ticket",
        header: "Ticket",
        cell: ({ row }) => (
          <TrackChip label="ticket" value={row.original.statusValues?.ticket_status} />
        ),
      },
      {
        accessorKey: "partnerName",
        header: "Partner",
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        id: "actions",
        header: () => <div className="text-center">Actions</div>,
        cell: ({ row }) => (
          <TravelRowActions
            candidate={row.original}
            onMutate={() => mutate()}
            board="ticket"
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

  if (!canView) return <AccessDenied resource="Ticket board" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Tickets"
        description={<>Book flights · {totalCount} candidate{totalCount === 1 ? "" : "s"}</>}
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
        exportFileName="tickets" table={table} paginated emptyMessage="No candidates awaiting tickets — they appear here after “To Ticket” from LMIS." />
          )}
        </div>
      )}
    </div>
  );
}
