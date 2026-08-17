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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { ExceptionStatusBadge } from "@/components/workflow/remaining-days-badge";
import {
  useExceptionCases,
  type ExceptionCaseListItem,
} from "@/lib/api/exceptions";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { Loader2 } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { NameCell } from "@/components/data-table/name-cell";
import { indexColumn } from "@/components/data-table/index-column";

export default function ExceptionsListPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView =
    hasPermission("arrival.read") ||
    hasPermission("arrival.exception") ||
    hasPermission("system.admin");
  const [status, setStatus] = useState<string>("");
  const [type, setType] = useState<string>("");

  const { cases, totalCount, isLoading, error, mutate } = useExceptionCases({
    status: status || undefined,
    type: type || undefined,
    pageSize: 50,
  });

  const columns = useMemo<ColumnDef<ExceptionCaseListItem>[]>(
    () => [
      indexColumn<ExceptionCaseListItem>(),
      {
        accessorKey: "candidateName",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Candidate" />,
        cell: ({ row }) => (
          <NameCell
            href={`/workflow/exceptions/${row.original.id}`}
            name={row.original.candidateName}
          />
        ),
      },
      {
        accessorKey: "passportNumber",
        header: "Passport",
      },
      {
        accessorKey: "type",
        header: "Type",
      },
      {
        accessorKey: "status",
        header: "Status",
        cell: ({ row }) => <ExceptionStatusBadge status={row.original.status} />,
      },
      {
        accessorKey: "openedAt",
        header: "Opened",
        cell: ({ getValue }) =>
          new Date(getValue() as string).toLocaleDateString(),
      },
      {
        id: "impact",
        header: "Impact",
        cell: ({ row }) =>
          row.original.financialImpactAmount != null
            ? `${row.original.financialImpactAmount} ${row.original.financialImpactCurrency ?? ""}`
            : "—",
      },
    ],
    []
  );

  const table = useReactTable({
    data: cases,
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

  if (!canView) return <AccessDenied resource="Exceptions" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Exceptions"
        description={<>Returned / Runaway cases · {totalCount}</>}
        actions={
          <div className="flex flex-wrap gap-2">
            <Select value={status || "all"} onValueChange={(v) => setStatus(v === "all" ? "" : v)}>
              <SelectTrigger className="w-[160px]">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All statuses</SelectItem>
                <SelectItem value="Open">Open</SelectItem>
                <SelectItem value="UnderInvestigation">Investigating</SelectItem>
                <SelectItem value="Resolved">Resolved</SelectItem>
                <SelectItem value="Closed">Closed</SelectItem>
              </SelectContent>
            </Select>
            <Select value={type || "all"} onValueChange={(v) => setType(v === "all" ? "" : v)}>
              <SelectTrigger className="w-[140px]">
                <SelectValue placeholder="Type" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All types</SelectItem>
                <SelectItem value="Returned">Returned</SelectItem>
                <SelectItem value="Runaway">Runaway</SelectItem>
              </SelectContent>
            </Select>
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
            <DataTable table={table} paginated emptyMessage="No exception cases — Returned/Runaway cases will appear here." />
          )}
        </div>
      )}
    </div>
  );
}
