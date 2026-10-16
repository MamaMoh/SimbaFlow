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
import { Megaphone, MoreHorizontal, Pencil, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { PageHeader } from "@/components/ui/page-header";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { usePlatformStaff } from "@/lib/platform/use-platform-staff";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { DataTable } from "@/components/data-table/data-table";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { indexColumn } from "@/components/data-table/index-column";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import { useBoardQuery } from "@/lib/data-table/use-board-query";
import { applyColumnFilters } from "@/lib/data-table/apply-filters";
import { useTableRows } from "@/lib/data/use-table-rows";

type Level = "Info" | "Warning" | "Critical";

type AnnouncementRow = {
  id: string;
  title: string;
  body: string;
  level: Level;
  tenantId: string | null;
  agency: string | null;
  startsAt: string;
  endsAt: string | null;
  isLive: boolean;
};

/** A row as the table reads it, with the words its funnels compare against. */
type TableRow = AnnouncementRow & { audience: string; showing: string };

type Draft = {
  id?: string;
  title: string;
  body: string;
  level: Level;
  tenantId: string;
  startsAt: string;
  endsAt: string;
};

const ALL = "all";

/**
 * The agencies for the "Who sees it" list, whichever shape the cache holds them in.
 *
 * The agency switcher in the header reads the same address and caches the whole response,
 * `{ isSuccess, data }`, and SWR keeps whichever answer arrived first — so opening this form
 * after the switcher had loaded crashed the page with "map is not a function".
 */
function agencyList(value: unknown): { id: string; name: string }[] {
  if (Array.isArray(value)) return value;
  const data = (value as { data?: unknown } | null | undefined)?.data;
  return Array.isArray(data) ? data : [];
}

const fetcher = async (url: string) => {
  const res = await fetch(url);
  const body = await res.json().catch(() => null);
  if (!res.ok || !body?.isSuccess) throw new Error(body?.error || "Could not load");
  return body.data;
};

/** A UTC timestamp as the value a datetime-local input expects, in the viewer's own time. */
function toLocalInput(iso: string | null): string {
  if (!iso) return "";
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

const when = (iso: string) =>
  new Date(iso).toLocaleString([], { dateStyle: "medium", timeStyle: "short" });

const EMPTY: Draft = { title: "", body: "", level: "Info", tenantId: ALL, startsAt: "", endsAt: "" };

/**
 * Messages from the platform team to agencies. Each shows as a banner above every page for the
 * agencies it is addressed to, between its start and end.
 */
export default function AnnouncementsPage() {
  const { isStaff, isLoading } = usePlatformStaff();
  const { data, error, mutate } = useSWR<AnnouncementRow[]>(
    isStaff ? "/api/proxy/platform/announcements" : null,
    fetcher,
  );
  const { data: agencies } = useSWR<unknown>(
    isStaff ? "/api/proxy/tenants" : null,
    fetcher,
    { revalidateOnFocus: false },
  );
  const [draft, setDraft] = useState<Draft | null>(null);
  const [saving, setSaving] = useState(false);

  const [sorting, setSorting] = useState<SortingState>([]);
  // Every announcement arrives at once, so the funnels are answered here.
  const { globalFilter, setGlobalFilter, filters, setFilters } = useBoardQuery();
  const all = useTableRows<AnnouncementRow>(data);
  const rows = useMemo<TableRow[]>(
    () =>
      applyColumnFilters(
        all.map((a) => ({
          ...a,
          audience: a.agency ?? "All agencies",
          showing: a.isLive ? "Showing now" : "Not showing",
        })),
        filters,
        { title: "text", level: "text", showing: "text", audience: "text", startsAt: "date", endsAt: "date" },
      ),
    [all, filters],
  );

  const columns: ColumnDef<TableRow>[] = useMemo(() => {
    const edit = (a: AnnouncementRow) =>
      setDraft({
        id: a.id,
        title: a.title,
        body: a.body,
        level: a.level,
        tenantId: a.tenantId ?? ALL,
        startsAt: toLocalInput(a.startsAt),
        endsAt: toLocalInput(a.endsAt),
      });

    const remove = async (row: AnnouncementRow) => {
      const res = await fetch(`/api/proxy/platform/announcements/${row.id}`, { method: "DELETE" });
      const body = await res.json().catch(() => ({}));
      if (res.ok && body?.isSuccess) {
        toast.success("Announcement taken down");
        void mutate();
      } else {
        toast.error(body?.error || "Could not take it down");
      }
    };

    return [
      indexColumn<TableRow>(),
      {
        accessorKey: "title",
        meta: { filter: { field: "title", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Announcement" />,
        cell: ({ row }) => (
          <div className="max-w-[460px]">
            <span className="font-medium">{row.original.title}</span>
            <p className="line-clamp-2 whitespace-pre-line text-xs text-muted-foreground" title={row.original.body}>
              {row.original.body}
            </p>
          </div>
        ),
      },
      {
        accessorKey: "level",
        meta: { filter: { field: "level", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Level" />,
        cell: ({ row }) => (
          <Badge variant={row.original.level === "Critical" ? "destructive" : "outline"}>{row.original.level}</Badge>
        ),
      },
      {
        accessorKey: "showing",
        meta: { filter: { field: "showing", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Status" />,
        cell: ({ row }) =>
          row.original.isLive ? (
            <Badge className="bg-green-100 text-green-800 hover:bg-green-100">Showing now</Badge>
          ) : (
            <Badge variant="outline" className="text-muted-foreground">Not showing</Badge>
          ),
      },
      {
        accessorKey: "audience",
        meta: { filter: { field: "audience", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Who sees it" />,
      },
      {
        accessorKey: "startsAt",
        meta: { filter: { field: "startsAt", kind: "date" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Starts" />,
        cell: ({ row }) => <span className="whitespace-nowrap text-muted-foreground">{when(row.original.startsAt)}</span>,
      },
      {
        accessorKey: "endsAt",
        meta: { filter: { field: "endsAt", kind: "date" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Ends" />,
        cell: ({ row }) => (
          <span className="whitespace-nowrap text-muted-foreground">
            {row.original.endsAt ? when(row.original.endsAt) : "Until taken down"}
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
            {/* Edit and the destructive item together, the shape every row menu in the app uses. */}
            <DropdownMenuContent align="end">
              <DropdownMenuItem onClick={() => edit(row.original)}>
                <Pencil className="mr-2 h-4 w-4" /> Edit
              </DropdownMenuItem>
              <DropdownMenuItem className="text-destructive" onClick={() => remove(row.original)}>
                <Trash2 className="mr-2 h-4 w-4" /> Take down
              </DropdownMenuItem>
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
  const layout = useTableLayout({ table: "platform-announcements", columnIds });

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

  if (!isLoading && !isStaff) return <AccessDenied resource="announcements" />;
  if (error) return <LoadError message={error.message} onRetry={() => mutate()} />;

  const save = async () => {
    if (!draft) return;
    setSaving(true);
    try {
      const res = await fetch(
        draft.id ? `/api/proxy/platform/announcements/${draft.id}` : "/api/proxy/platform/announcements",
        {
          method: draft.id ? "PUT" : "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            title: draft.title,
            body: draft.body,
            level: draft.level,
            tenantId: draft.tenantId === ALL ? null : draft.tenantId,
            startsAt: draft.startsAt ? new Date(draft.startsAt).toISOString() : null,
            endsAt: draft.endsAt ? new Date(draft.endsAt).toISOString() : null,
          }),
        },
      );
      const body = await res.json().catch(() => ({}));
      if (!res.ok || !body?.isSuccess) {
        toast.error(body?.error || "Could not save the announcement");
        return;
      }
      toast.success(draft.id ? "Announcement updated" : "Announcement posted");
      setDraft(null);
      void mutate();
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Announcements" count={all.length} />

      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
          layout={layout}
          rowClickOpensActions
          table={table}
          enableGlobalFilter
          searchPlaceholder="Search announcements…"
          paginated
          exportFileName="announcements"
          filters={filters}
          onFiltersChange={setFilters}
          emptyMessage="Nothing posted. An announcement shows as a banner to the agencies you choose."
          createAction={
            <Button
              size="sm"
              className="h-8 gap-1.5 bg-green-800 text-white hover:bg-green-900"
              onClick={() => setDraft({ ...EMPTY })}
            >
              <Megaphone className="h-3.5 w-3.5" /> New announcement
            </Button>
          }
        />
      </div>

      <Dialog open={!!draft} onOpenChange={(open) => { if (!open) setDraft(null); }}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>{draft?.id ? "Edit announcement" : "New announcement"}</DialogTitle>
          </DialogHeader>
          {draft && (
            <div className="space-y-4">
              <div className="space-y-1.5">
                <Label htmlFor="ann-title">Title</Label>
                <Input
                  id="ann-title"
                  value={draft.title}
                  onChange={(e) => setDraft({ ...draft, title: e.target.value })}
                  placeholder="e.g. Planned maintenance on Saturday night"
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="ann-body">Message</Label>
                <Textarea
                  id="ann-body"
                  rows={4}
                  value={draft.body}
                  onChange={(e) => setDraft({ ...draft, body: e.target.value })}
                />
              </div>
              <div className="grid gap-4 sm:grid-cols-2">
                <div className="space-y-1.5">
                  <Label>Who sees it</Label>
                  <Select value={draft.tenantId} onValueChange={(v) => setDraft({ ...draft, tenantId: v })}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value={ALL}>All agencies</SelectItem>
                      {agencyList(agencies).map((t) => (
                        <SelectItem key={t.id} value={t.id}>
                          {t.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  <Label>How loud</Label>
                  <Select value={draft.level} onValueChange={(v) => setDraft({ ...draft, level: v as Level })}>
                    <SelectTrigger>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="Info">Info</SelectItem>
                      <SelectItem value="Warning">Warning</SelectItem>
                      <SelectItem value="Critical">Critical (cannot be dismissed)</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="ann-start">Starts</Label>
                  <Input
                    id="ann-start"
                    type="datetime-local"
                    value={draft.startsAt}
                    onChange={(e) => setDraft({ ...draft, startsAt: e.target.value })}
                  />
                  <p className="text-xs text-muted-foreground">Empty means now.</p>
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="ann-end">Ends</Label>
                  <Input
                    id="ann-end"
                    type="datetime-local"
                    value={draft.endsAt}
                    onChange={(e) => setDraft({ ...draft, endsAt: e.target.value })}
                  />
                  <p className="text-xs text-muted-foreground">Empty keeps it up until taken down.</p>
                </div>
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setDraft(null)}>
              Cancel
            </Button>
            <Button onClick={save} disabled={saving} className="bg-green-800 hover:bg-green-900">
              {draft?.id ? "Save" : "Post"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
