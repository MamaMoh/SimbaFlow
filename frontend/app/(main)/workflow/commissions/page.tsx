"use client";

import { useMemo, useState } from "react";
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
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { CommissionStatusBadge } from "@/components/finance/commission-status-badge";
import {
  formatEtb,
  useCommissionBoard,
  type CommissionBoardRow,
} from "@/lib/api/commissions";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";
import { indexColumn } from "@/components/data-table/index-column";

export default function CommissionsPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("commission.read") || hasPermission("system.admin");
  const [sorting, setSorting] = useState<SortingState>([]);
  const [statusFilter, setStatusFilter] = useState("");
  const [search, setSearch] = useState("");

  const { items, totalCount, isLoading, error, mutate } = useCommissionBoard({
    status: statusFilter || undefined,
    search: search || undefined,
    pageSize: 100,
  });

  const columns = useMemo<ColumnDef<CommissionBoardRow>[]>(
    () => [
      indexColumn<CommissionBoardRow>(),
      {
        accessorKey: "candidateName",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Candidate" />,
        cell: ({ row }) => (
          <NameCell
            href={`/workflow/commissions/${row.original.id}`}
            name={row.original.candidateName}
          />
        ),
      },
      {
        accessorKey: "passportNumber",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Passport" />,
      },
      {
        accessorKey: "countryOfTravel",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Country" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "partnerName",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Partner" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "status",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Status" />,
        cell: ({ getValue }) => <CommissionStatusBadge status={String(getValue() || "")} />,
      },
      {
        accessorKey: "totalFeesAmount",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Fees (ETB)" />,
        cell: ({ getValue }) => formatEtb(getValue() as number),
      },
      {
        accessorKey: "totalPaidAmount",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Paid" />,
        cell: ({ getValue }) => formatEtb(getValue() as number),
      },
      {
        accessorKey: "balanceAmount",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Balance" />,
        cell: ({ getValue }) => formatEtb(getValue() as number),
      },
      {
        accessorKey: "openedAt",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Opened" />,
        cell: ({ getValue }) => {
          const v = getValue() as string;
          return v ? new Date(v).toLocaleDateString() : "—";
        },
      },
    ],
    []
  );

  const table = useReactTable({
    data: items,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
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

  if (!canView) return <AccessDenied resource="Commissions" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Commissions"
        description={<>Queue · {totalCount} record{totalCount === 1 ? "" : "s"}</>}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <Input
              className="h-8 w-48"
              placeholder="Search name / passport"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
            {["", "Open", "Partial", "Settled", "Disputed"].map((s) => (
              <Button
                key={s || "all"}
                size="sm"
                variant={statusFilter === s ? "default" : "outline"}
                onClick={() => setStatusFilter(s)}
              >
                {s || "All"}
              </Button>
            ))}
          </div>
        }
      />

      {error ? (
        <LoadError
          message={error instanceof Error ? error.message : "Failed to load"}
          onRetry={() => mutate()}
        />
      ) : null}


      {!error ? (
        <div className="rounded-lg border bg-card p-4 shadow-sm">
          <DataTable
            table={table}
            paginated
            emptyMessage="No commissions yet — open one from the Arrivals board (Add to Commission)."
          />
        </div>
      ) : null}

      <p className="text-sm text-muted-foreground">
        <Link href="/workflow/arrivals" className="text-primary underline-offset-4 hover:underline">
          Go to Arrivals board
        </Link>
        {" · "}
        <Link href="/finance/rates" className="text-primary underline-offset-4 hover:underline">
          Exchange rates
        </Link>
      </p>
    </div>
  );
}
