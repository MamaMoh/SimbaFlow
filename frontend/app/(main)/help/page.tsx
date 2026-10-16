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
import { CheckCircle2, LifeBuoy, MessageSquare, MoreHorizontal } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { LoadError } from "@/components/ui/page-alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { SupportThread, StatusPill, STATUS_LABEL, type TicketStatus } from "@/components/platform/support-thread";
import { DataTable } from "@/components/data-table/data-table";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { indexColumn } from "@/components/data-table/index-column";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import { useBoardQuery } from "@/lib/data-table/use-board-query";
import { applyColumnFilters } from "@/lib/data-table/apply-filters";
import { useTableRows } from "@/lib/data/use-table-rows";
import { ago } from "@/lib/platform/health";

type Row = {
  id: string;
  subject: string;
  status: TicketStatus;
  lastActivityAt: string;
  messages: number;
};

/** A row as the table reads it, with the status in the words the pill shows. */
type TableRow = Row & { statusLabel: string };

const fetcher = async (url: string): Promise<Row[]> => {
  const res = await fetch(url);
  const body = await res.json().catch(() => null);
  if (!res.ok || !body?.isSuccess) throw new Error(body?.error || "Could not load your help requests");
  return body.data;
};

/**
 * Ask the SimbaFlow team for help. Everyone in the agency sees the agency's requests, so a
 * colleague can pick up an answer.
 *
 * The same table as every other board, with the conversation — or a new request — in a panel
 * over it.
 */
export default function HelpPage() {
  const { data, error, mutate } = useSWR("/api/proxy/support", fetcher, { refreshInterval: 60_000 });
  const [openId, setOpenId] = useState<string | null>(null);
  const [writing, setWriting] = useState(false);
  const [subject, setSubject] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);

  const [sorting, setSorting] = useState<SortingState>([]);
  // The agency's requests arrive whole, so the funnels are answered here.
  const { globalFilter, setGlobalFilter, filters, setFilters } = useBoardQuery();
  const all = useTableRows<Row>(data);
  const rows = useMemo<TableRow[]>(
    () =>
      applyColumnFilters(
        all.map((r) => ({ ...r, statusLabel: STATUS_LABEL[r.status] ?? r.status })),
        filters,
        { subject: "text", statusLabel: "text", messages: "number", lastActivityAt: "date" },
      ),
    [all, filters],
  );

  const columns: ColumnDef<TableRow>[] = useMemo(() => {
    const close = async (r: Row) => {
      const res = await fetch(`/api/proxy/support/${r.id}/close`, { method: "POST" });
      const body = await res.json().catch(() => ({}));
      if (res.ok && body?.isSuccess) {
        toast.success("Request closed");
        void mutate();
      } else {
        toast.error(body?.error || "Could not close the request");
      }
    };

    return [
      indexColumn<TableRow>(),
      {
        accessorKey: "subject",
        meta: { filter: { field: "subject", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Subject" />,
        cell: ({ row }) => (
          <span className="block max-w-[420px] truncate font-medium" title={row.original.subject}>
            {row.original.subject}
          </span>
        ),
      },
      {
        accessorKey: "statusLabel",
        meta: { filter: { field: "statusLabel", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Status" />,
        cell: ({ row }) => <StatusPill status={row.original.status} />,
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
              <DropdownMenuItem onClick={() => { setWriting(false); setOpenId(row.original.id); }}>
                <MessageSquare className="mr-2 h-4 w-4" /> Open
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
  const layout = useTableLayout({ table: "agency-help", columnIds });

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

  if (error) return <LoadError message={error.message} onRetry={() => mutate()} />;

  const raise = async () => {
    setBusy(true);
    try {
      const res = await fetch("/api/proxy/support", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ subject, message }),
      });
      const body = await res.json().catch(() => ({}));
      if (!res.ok || !body?.isSuccess) {
        toast.error(body?.error || "Could not send your request");
        return;
      }
      toast.success("Sent. The SimbaFlow team will reply here.");
      setSubject("");
      setMessage("");
      setWriting(false);
      setOpenId(body.data.id);
      void mutate();
    } finally {
      setBusy(false);
    }
  };

  const panelOpen = writing || !!openId;
  const closePanel = () => {
    setWriting(false);
    setOpenId(null);
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Help" count={all.length} />

      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
          layout={layout}
          table={table}
          enableGlobalFilter
          searchPlaceholder="Search your requests…"
          paginated
          exportFileName="help-requests"
          filters={filters}
          onFiltersChange={setFilters}
          onRowClick={(r: TableRow) => { setWriting(false); setOpenId(r.id); }}
          emptyMessage="No requests yet. Ask the SimbaFlow team anything about using the system."
          createAction={
            <Button
              size="sm"
              className="h-8 gap-1.5 bg-green-800 text-white hover:bg-green-900"
              onClick={() => { setOpenId(null); setWriting(true); }}
            >
              <LifeBuoy className="h-3.5 w-3.5" /> Ask for help
            </Button>
          }
        />
      </div>

      <Sheet open={panelOpen} onOpenChange={(open) => { if (!open) closePanel(); }}>
        <SheetContent className="flex w-[560px] flex-col overflow-y-auto sm:max-w-[560px]">
          <SheetHeader>
            <SheetTitle>{writing ? "Ask for help" : "Help request"}</SheetTitle>
            <SheetDescription>
              {writing
                ? "The SimbaFlow team answers here, and everyone in your agency can see the conversation."
                : "Your conversation with the SimbaFlow team."}
            </SheetDescription>
          </SheetHeader>
          <div className="px-4 pb-4">
            {writing ? (
              <div className="space-y-4">
                <div className="space-y-1.5">
                  <Label htmlFor="help-subject">What is it about?</Label>
                  <Input
                    id="help-subject"
                    value={subject}
                    onChange={(e) => setSubject(e.target.value)}
                    placeholder="e.g. Cannot print the visa form for one candidate"
                  />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="help-message">Tell us more</Label>
                  <Textarea
                    id="help-message"
                    rows={6}
                    value={message}
                    onChange={(e) => setMessage(e.target.value)}
                    placeholder="What were you doing, what did you expect, and what happened instead?"
                  />
                </div>
                <div className="flex justify-end gap-2">
                  <Button variant="outline" size="sm" onClick={closePanel}>
                    Cancel
                  </Button>
                  <Button
                    size="sm"
                    onClick={raise}
                    disabled={busy || !subject.trim() || !message.trim()}
                    className="bg-green-800 hover:bg-green-900"
                  >
                    Send
                  </Button>
                </div>
              </div>
            ) : openId ? (
              <SupportThread key={openId} base={`/api/proxy/support/${openId}`} onChanged={() => void mutate()} />
            ) : null}
          </div>
        </SheetContent>
      </Sheet>
    </div>
  );
}
