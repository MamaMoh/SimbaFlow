"use client";

import { useMemo, useState } from "react";
import useSWR from "swr";
import { toast } from "sonner";
import {
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  type ColumnDef,
  type SortingState,
} from "@tanstack/react-table";
import { CheckCircle2, Copy, MoreHorizontal } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { PageHeader } from "@/components/ui/page-header";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { AgeCell } from "@/components/data-table/age-cell";
import { DataTable } from "@/components/data-table/data-table";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { indexColumn } from "@/components/data-table/index-column";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import { useBoardQuery } from "@/lib/data-table/use-board-query";
import { applyColumnFilters } from "@/lib/data-table/apply-filters";
import { useTableRows } from "@/lib/data/use-table-rows";

type ErrorGroup = {
  fingerprint: string;
  count: number;
  lastSeen: string;
  firstSeen: string;
  exceptionType: string;
  message: string;
  path: string | null;
  source: string;
  affectedUsers: number;
};

const fetcher = (url: string) => fetch(url).then((r) => r.json());

/** Days between a timestamp and now, for the age chip. */
function daysSince(iso: string) {
  return Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000));
}

const when = (iso: string) =>
  new Date(iso).toLocaleString([], { dateStyle: "medium", timeStyle: "short" });

export default function ErrorsPage() {
  const { hasPermission, isSuperAdmin, permissions } = usePermissions();
  // The support desk reads errors too; only administrators mark them resolved.
  const canResolve = isSuperAdmin || hasPermission("system.admin");
  const allowed = canResolve || permissions.includes("platform.support");

  const { data, error, mutate } = useSWR(
    allowed ? "/api/proxy/diagnostics/errors" : null,
    fetcher,
    { revalidateOnFocus: false, refreshInterval: 60_000 }
  );

  const [sorting, setSorting] = useState<SortingState>([]);
  // The unresolved errors arrive whole, so the funnels are answered here like on the other
  // boards that hold every row they have.
  const { globalFilter, setGlobalFilter, filters, setFilters } = useBoardQuery();

  const all = useTableRows<ErrorGroup>(data?.data);
  const groups = useMemo(
    () =>
      applyColumnFilters(all, filters, {
        source: "text",
        exceptionType: "text",
        message: "text",
        path: "text",
        count: "number",
        affectedUsers: "number",
        lastSeen: "date",
        firstSeen: "date",
      }),
    [all, filters]
  );

  const columns: ColumnDef<ErrorGroup>[] = useMemo(() => {
    const resolve = async (fingerprint: string) => {
      const res = await fetch(`/api/proxy/diagnostics/errors/${fingerprint}/resolve`, { method: "POST" });
      const body = await res.json().catch(() => null);
      if (res.ok && body?.isSuccess) {
        toast.success("Marked as resolved");
        mutate();
      } else {
        toast.error(body?.error || "Could not mark it resolved");
      }
    };

    const copy = async (g: ErrorGroup) => {
      const text = [`${g.source} ${g.exceptionType}`, g.path, g.message].filter(Boolean).join("\n");
      try {
        await navigator.clipboard.writeText(text);
        toast.success("Error copied");
      } catch {
        toast.error("Could not copy");
      }
    };

    return [
      indexColumn<ErrorGroup>(),
      {
        accessorKey: "source",
        meta: { filter: { field: "source", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Source" />,
        cell: ({ row }) => (
          <span className="rounded bg-muted px-1.5 py-0.5 text-xs font-medium uppercase tracking-wide">
            {row.original.source}
          </span>
        ),
      },
      {
        accessorKey: "exceptionType",
        meta: { filter: { field: "exceptionType", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Error" />,
        cell: ({ row }) => (
          <span className="font-mono text-sm font-semibold">{row.original.exceptionType}</span>
        ),
      },
      {
        accessorKey: "message",
        meta: { filter: { field: "message", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Message" />,
        cell: ({ row }) => (
          <p className="line-clamp-2 max-w-[300px] text-sm text-muted-foreground" title={row.original.message}>
            {row.original.message}
          </p>
        ),
      },
      {
        accessorKey: "path",
        meta: { filter: { field: "path", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Path" />,
        cell: ({ row }) =>
          row.original.path ? (
            <span className="block max-w-[180px] truncate font-mono text-xs text-muted-foreground" title={row.original.path}>
              {row.original.path}
            </span>
          ) : (
            <span className="text-muted-foreground">—</span>
          ),
      },
      {
        accessorKey: "count",
        meta: { filter: { field: "count", kind: "number" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Occurrences" />,
        cell: ({ row }) => <span className="tabular-nums">{row.original.count}</span>,
      },
      {
        accessorKey: "affectedUsers",
        meta: { filter: { field: "affectedUsers", kind: "number" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Users" />,
        cell: ({ row }) => <span className="tabular-nums">{row.original.affectedUsers}</span>,
      },
      {
        accessorKey: "lastSeen",
        meta: { filter: { field: "lastSeen", kind: "date" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Last seen" />,
        cell: ({ row }) => (
          <span className="whitespace-nowrap text-muted-foreground">{when(row.original.lastSeen)}</span>
        ),
      },
      {
        id: "firstSeen",
        accessorFn: (g) => daysSince(g.firstSeen),
        meta: { filter: { field: "firstSeen", kind: "date" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Age" />,
        cell: ({ row }) => (
          <AgeCell days={daysSince(row.original.firstSeen)} title="Days since first seen" />
        ),
      },
      {
        id: "actions",
        header: "Actions",
        cell: ({ row }) => (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon" className="h-8 w-8" aria-label="Row actions">
                <MoreHorizontal className="h-4 w-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              {canResolve && (
                <DropdownMenuItem onClick={() => resolve(row.original.fingerprint)}>
                  <CheckCircle2 className="mr-2 h-4 w-4" /> Resolve
                </DropdownMenuItem>
              )}
              <DropdownMenuItem onClick={() => copy(row.original)}>
                <Copy className="mr-2 h-4 w-4" /> Copy error
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        ),
        size: 60,
        enableSorting: false,
      },
    ];
  }, [canResolve, mutate]);

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "admin-errors", columnIds });

  const table = useReactTable({
    data: groups,
    columns,
    state: {
      sorting,
      globalFilter,
      columnOrder: layout.columnOrder,
      columnVisibility: layout.columnVisibility,
    },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    initialState: { pagination: { pageSize: 10 } },
    onColumnOrderChange: layout.setColumnOrder,
    onColumnVisibilityChange: layout.setColumnVisibility,
  });

  if (!allowed) return <AccessDenied resource="errors" />;
  if (error) return <LoadError message={error.message} onRetry={() => mutate()} />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Errors" count={all.length} />

      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
          layout={layout}
          rowClickOpensActions
          table={table}
          enableGlobalFilter
          searchPlaceholder="Search errors…"
          paginated
          exportFileName="errors"
          filters={filters}
          onFiltersChange={setFilters}
          emptyMessage="No unresolved errors."
        />
      </div>
    </div>
  );
}
