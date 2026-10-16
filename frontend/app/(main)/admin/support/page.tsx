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
import { CheckCircle2, MessageSquare, MoreHorizontal } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { SupportThread, StatusPill, type TicketStatus } from "@/components/platform/support-thread";
import { DataTable } from "@/components/data-table/data-table";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { indexColumn } from "@/components/data-table/index-column";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import { useBoardQuery } from "@/lib/data-table/use-board-query";
import { applyColumnFilters } from "@/lib/data-table/apply-filters";
import { useTableRows } from "@/lib/data/use-table-rows";
import { usePlatformStaff } from "@/lib/platform/use-platform-staff";
import { ago } from "@/lib/platform/health";

type Row = {
  id: string;
  agency: string;
  subject: string;
  status: TicketStatus;
  raisedBy: string;
  lastActivityAt: string;
  messages: number;
};

const ALL = "all";

const FILTERS: { value: string; label: string }[] = [
  { value: "Open", label: "Needs a reply" },
  { value: "Answered", label: "Answered" },
  { value: "Closed", label: "Closed" },
  { value: ALL, label: "All requests" },
];

const fetcher = async (url: string): Promise<Row[]> => {
  const res = await fetch(url);
  const body = await res.json().catch(() => null);
  if (!res.ok || !body?.isSuccess) throw new Error(body?.error || "Could not load help requests");
  return body.data;
};

/**
 * Every agency's help requests in one inbox, oldest unanswered first.
 *
 * The same table as every other board, with the conversation in a panel over it: a request is
 * opened from its row and answered there, and closing the panel puts the list back where it was.
 */
export default function SupportInboxPage() {
  const { isStaff, isLoading } = usePlatformStaff();
  const [status, setStatus] = useState("Open");
  const [openId, setOpenId] = useState<string | null>(null);
  const { data, error, mutate } = useSWR(
    isStaff ? `/api/proxy/platform/support${status !== ALL ? `?status=${status}` : ""}` : null,
    fetcher,
    { refreshInterval: 60_000 },
  );

  const [sorting, setSorting] = useState<SortingState>([]);
  // The inbox arrives whole for the status chosen, so the funnels are answered here.
  const { globalFilter, setGlobalFilter, filters, setFilters } = useBoardQuery();
  const all = useTableRows<Row>(data);
  const rows = useMemo(
    () =>
      applyColumnFilters(all, filters, {
        subject: "text",
        agency: "text",
        raisedBy: "text",
        status: "text",
        messages: "number",
        lastActivityAt: "date",
      }),
    [all, filters],
  );

  const columns: ColumnDef<Row>[] = useMemo(() => {
    const close = async (r: Row) => {
      const res = await fetch(`/api/proxy/platform/support/${r.id}/close`, { method: "POST" });
      const body = await res.json().catch(() => ({}));
      if (res.ok && body?.isSuccess) {
        toast.success("Request closed");
        void mutate();
      } else {
        toast.error(body?.error || "Could not close the request");
      }
    };

    return [
      indexColumn<Row>(),
      {
        accessorKey: "subject",
        meta: { filter: { field: "subject", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Subject" />,
        cell: ({ row }) => (
          <span className="block max-w-[360px] truncate font-medium" title={row.original.subject}>
            {row.original.subject}
          </span>
        ),
      },
      {
        accessorKey: "agency",
        meta: { filter: { field: "agency", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Agency" />,
      },
      {
        accessorKey: "raisedBy",
        meta: { filter: { field: "raisedBy", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Raised by" />,
      },
      {
        accessorKey: "status",
        meta: { filter: { field: "status", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Status" />,
        cell: ({ row }) => <StatusPill status={row.original.status} platformView />,
      },
      {
        accessorKey: "messages",
        meta: { filter: { field: "messages", kind: "number" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Messages" />,
        cell: ({ row }) => <span className="tabular-nums">{row.original.messages}</span>,
      },
      {
        accessorKey: "lastActivityAt",
        meta: { filter: { field: "lastActivityAt", kind: "date" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Last activity" />,
        cell: ({ row }) => (
          <span
            className="whitespace-nowrap text-muted-foreground"
            title={new Date(row.original.lastActivityAt).toLocaleString()}
          >
            {ago(row.original.lastActivityAt)}
          </span>
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
              <DropdownMenuItem onClick={() => setOpenId(row.original.id)}>
                <MessageSquare className="mr-2 h-4 w-4" /> Open and reply
              </DropdownMenuItem>
              {row.original.status !== "Closed" && (
                <DropdownMenuItem onClick={() => close(row.original)}>
                  <CheckCircle2 className="mr-2 h-4 w-4" /> Close request
                </DropdownMenuItem>
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        ),
        size: 60,
        enableSorting: false,
      },
    ];
  }, [mutate]);

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "platform-support", columnIds });

  const table = useReactTable({
    data: rows,
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

  if (!isLoading && !isStaff) return <AccessDenied resource="help requests" />;
  if (error) return <LoadError message={error.message} onRetry={() => mutate()} />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Help requests" count={all.length} />

      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
          layout={layout}
          table={table}
          enableGlobalFilter
          searchPlaceholder="Search help requests…"
          paginated
          exportFileName="help-requests"
          filters={filters}
          onFiltersChange={setFilters}
          onRowClick={(r: Row) => setOpenId(r.id)}
          emptyMessage={status === "Open" ? "Nothing waiting for a reply." : "Nothing here."}
          toolbarEndActions={
            <Select value={status} onValueChange={(v) => { setStatus(v); setOpenId(null); }}>
              <SelectTrigger className="h-8 w-[150px]" aria-label="Status">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {FILTERS.map((f) => (
                  <SelectItem key={f.value} value={f.value}>
                    {f.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          }
        />
      </div>

      <Sheet open={!!openId} onOpenChange={(open) => { if (!open) setOpenId(null); }}>
        <SheetContent aria-describedby={undefined} className="flex w-[560px] flex-col overflow-y-auto sm:max-w-[560px]">
          <SheetHeader>
            <SheetTitle>Help request</SheetTitle>
          </SheetHeader>
          {openId ? (
            <div className="px-4 pb-4">
              <SupportThread
                key={openId}
                base={`/api/proxy/platform/support/${openId}`}
                platformView
                onChanged={() => void mutate()}
              />
            </div>
          ) : null}
        </SheetContent>
      </Sheet>
    </div>
  );
}
