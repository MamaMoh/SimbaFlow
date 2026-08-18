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
import { PendingCell } from "@/components/data-table/pending-cell";
import { DataTable } from "@/components/data-table/data-table";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { TrackChip } from "@/components/workflow/status-update-sheet";
import { EmbassyRowActions } from "@/components/workflow/embassy-row-actions";
import { useEmbassyBoard, type EmbassyBoardRow } from "@/lib/api/embassy";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";
import { indexColumn } from "@/components/data-table/index-column";

export default function EmbassyBoardPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("embassy.read") || hasPermission("system.admin");
  const [search, setSearch] = useState("");

  const { candidates, totalCount, isLoading, error, mutate } = useEmbassyBoard({
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
        accessorKey: "partnerName",
        header: "Partner",
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        id: "medical",
        header: "Medical",
        cell: ({ row }) => {
          const v = row.original.statusValues?.medical;
          return <TrackChip label="medical" value={v} warn={v === "Unfit"} />;
        },
      },
      {
        id: "tasheer",
        header: "Tasheer",
        cell: ({ row }) => {
          const v = row.original.statusValues?.tasheer;
          return <TrackChip label="tasheer" value={v} warn={v === "Expired"} />;
        },
      },
      {
        id: "visa",
        header: "Visa",
        cell: ({ row }) => (
          <TrackChip label="visa" value={row.original.statusValues?.visa} />
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
        id: "tasheerAppointment",
        header: "Tasheer appt.",
        cell: ({ row }) => (
          <PendingCell
            value={row.original.statusValues?.tasheer_appointment_date}
            pendingLabel="No appointment"
          />
        ),
      },
      {
        id: "badges",
        header: "",
        cell: ({ row }) => {
          const s = row.original.statusValues ?? {};
          const mirrorLmis = s.medical === "Fit" && s.tasheer === "Book Done";
          return (
            <div className="flex flex-wrap gap-1">
              {s.medical === "Unfit" && (
                <Badge variant="destructive" className="text-xs">
                  Unfit
                </Badge>
              )}
              {s.tasheer === "Expired" && (
                <Badge variant="outline" className="text-xs text-amber-700">
                  Expired
                </Badge>
              )}
              {mirrorLmis && (
                <Badge variant="outline" className="text-xs">
                  Mirror→LMIS
                </Badge>
              )}
            </div>
          );
        },
      },
      {
        id: "actions",
        header: () => <div className="text-center">Actions</div>,
        cell: ({ row }) => (
          <EmbassyRowActions candidate={row.original} onMutate={() => mutate()} />
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

  if (!canView) return <AccessDenied resource="Embassy board" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Embassy"
        description={<>Medical, Tasheer &amp; Visa · {totalCount} candidate{totalCount === 1 ? "" : "s"}</>}
        actions={
          <Input
            className="max-w-xs"
            placeholder="Search name or passport…"
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
        exportFileName="embassy"
            table={table}
            enableGlobalFilter={false}
            paginated
            emptyMessage="No candidates in Embassy yet — they appear here after “To Embassy” from New Contracts."
          />
        )}
      </div>
    </div>
  );
}
