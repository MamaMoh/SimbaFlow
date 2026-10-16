"use client";

import { useMemo, useState } from "react";
import useSWR from "swr";
import { toast } from "sonner";
import {
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getSortedRowModel,
  type ColumnDef,
  type PaginationState,
  type SortingState,
} from "@tanstack/react-table";
import { Building2, Copy, MoreHorizontal, UserRound } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
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
import { usePlatformStaff } from "@/lib/platform/use-platform-staff";

type Option = { value: string; label: string };

type Entry = {
  id: string;
  at: string;
  userId: string | null;
  userName: string;
  isPlatformTeam: boolean;
  action: string;
  agencyId: string | null;
  agency: string | null;
  details: string | null;
  success: boolean;
};

type AuditPage = {
  items: Entry[];
  total: number;
  page: number;
  pageSize: number;
  users: Option[];
  agencies: Option[];
};

/** A row as the table reads it: the stored detail already turned into a line. */
type AuditRow = Entry & { detailText: string | null; result: string };

const ANY = "any";

const fetcher = async (url: string): Promise<AuditPage> => {
  const res = await fetch(url);
  const body = await res.json().catch(() => null);
  if (!res.ok || !body?.isSuccess) throw new Error(body?.error || "Could not load the audit trail");
  return body.data;
};

/** A stored JSON detail as a short readable line: "reason: …, hours: 4". */
function describeDetails(raw: string | null): string | null {
  if (!raw) return null;
  try {
    const obj = JSON.parse(raw);
    if (!obj || typeof obj !== "object") return String(obj);
    return Object.entries(obj)
      .filter(([, v]) => v !== null && v !== "" && v !== undefined)
      .map(([k, v]) => `${k}: ${typeof v === "object" ? JSON.stringify(v) : v}`)
      .join(", ");
  } catch {
    return raw;
  }
}

const when = (iso: string) =>
  new Date(iso).toLocaleString([], { dateStyle: "medium", timeStyle: "short" });

/**
 * The audit trail across every agency. "Platform team only" narrows it to what the platform team
 * and the system did: entering agencies, suspensions, billing, announcements and help requests.
 *
 * The trail is too long to send whole, so it is paged by the API — the one board that is. The
 * agency, person, dates and platform switch travel with each request and narrow the whole trail;
 * the search box and the funnels narrow the page in hand, which is why the box says so.
 */
export default function PlatformAuditPage() {
  const { isStaff, isLoading } = usePlatformStaff();
  const [agency, setAgency] = useState(ANY);
  const [user, setUser] = useState(ANY);
  const [platformOnly, setPlatformOnly] = useState(true);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [pagination, setPagination] = useState<PaginationState>({ pageIndex: 0, pageSize: 50 });
  const [sorting, setSorting] = useState<SortingState>([]);
  const { globalFilter, setGlobalFilter, filters, setFilters } = useBoardQuery();

  const params = new URLSearchParams({
    page: String(pagination.pageIndex + 1),
    pageSize: String(pagination.pageSize),
  });
  if (agency !== ANY) params.set("tenantId", agency);
  if (user !== ANY) params.set("userId", user);
  if (platformOnly) params.set("scope", "platform");
  if (from) params.set("from", from);
  if (to) params.set("to", to);

  const { data, error, mutate } = useSWR(isStaff ? `/api/proxy/platform/audit?${params}` : null, fetcher, {
    keepPreviousData: true,
  });

  const reset = () => setPagination((p) => ({ ...p, pageIndex: 0 }));

  const rows = useMemo<AuditRow[]>(
    () =>
      applyColumnFilters(
        (data?.items ?? []).map((e) => ({
          ...e,
          detailText: describeDetails(e.details),
          result: e.success ? "Succeeded" : "Failed",
        })),
        filters,
        { at: "date", userName: "text", action: "text", agency: "text", result: "text" },
      ),
    [data, filters],
  );

  const staffIds = useMemo(() => new Set((data?.users ?? []).map((u) => u.value)), [data]);

  const columns: ColumnDef<AuditRow>[] = useMemo(() => {
    const copy = async (e: AuditRow) => {
      const text = [when(e.at), e.userName, e.action, e.detailText, e.agency].filter(Boolean).join(" · ");
      try {
        await navigator.clipboard.writeText(text);
        toast.success("Entry copied");
      } catch {
        toast.error("Could not copy");
      }
    };

    return [
      indexColumn<AuditRow>(),
      {
        accessorKey: "at",
        meta: { filter: { field: "at", kind: "date" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="When" />,
        cell: ({ row }) => (
          <span className="whitespace-nowrap text-muted-foreground">{when(row.original.at)}</span>
        ),
      },
      {
        accessorKey: "userName",
        meta: { filter: { field: "userName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Who" />,
        cell: ({ row }) => (
          <span className="whitespace-nowrap">
            {row.original.userName}
            {row.original.isPlatformTeam ? (
              <Badge variant="outline" className="ml-1.5 text-[10px]">Platform</Badge>
            ) : null}
          </span>
        ),
      },
      {
        accessorKey: "action",
        meta: { filter: { field: "action", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="What" />,
        cell: ({ row }) => (
          <div className="max-w-[560px]">
            <span className={row.original.success ? "" : "text-red-700"}>{row.original.action}</span>
            {row.original.detailText ? (
              <p className="line-clamp-2 break-all text-xs text-muted-foreground" title={row.original.detailText}>
                {row.original.detailText}
              </p>
            ) : null}
          </div>
        ),
      },
      {
        accessorKey: "agency",
        meta: { filter: { field: "agency", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Agency" />,
        cell: ({ row }) => <span className="text-muted-foreground">{row.original.agency ?? "—"}</span>,
      },
      {
        accessorKey: "result",
        meta: { filter: { field: "result", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Result" />,
        cell: ({ row }) =>
          row.original.success ? (
            <Badge variant="outline" className="text-muted-foreground">Succeeded</Badge>
          ) : (
            <Badge variant="destructive">Failed</Badge>
          ),
      },
      {
        id: "actions",
        header: "Actions",
        cell: ({ row }) => {
          const e = row.original;
          return (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" className="h-8 w-8" aria-label="Row actions">
                  <MoreHorizontal className="h-4 w-4" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem onClick={() => copy(e)}>
                  <Copy className="mr-2 h-4 w-4" /> Copy entry
                </DropdownMenuItem>
                {e.agencyId && (
                  <DropdownMenuItem onClick={() => { setAgency(e.agencyId!); reset(); }}>
                    <Building2 className="mr-2 h-4 w-4" /> Only {e.agency ?? "this agency"}
                  </DropdownMenuItem>
                )}
                {/* The person filter lists the platform team, so it is only offered for them. */}
                {e.userId && staffIds.has(e.userId) && (
                  <DropdownMenuItem onClick={() => { setUser(e.userId!); reset(); }}>
                    <UserRound className="mr-2 h-4 w-4" /> Only {e.userName}
                  </DropdownMenuItem>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          );
        },
        size: 60,
        enableSorting: false,
      },
    ];
  }, [staffIds]);

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "platform-audit", columnIds });

  const table = useReactTable({
    data: rows,
    columns,
    state: {
      sorting,
      globalFilter,
      pagination,
      columnOrder: layout.columnOrder,
      columnVisibility: layout.columnVisibility,
    },
    // The API pages this one, so the table is told how many pages there are rather than
    // counting the rows it holds.
    manualPagination: true,
    pageCount: data ? Math.max(1, Math.ceil(data.total / pagination.pageSize)) : 1,
    onPaginationChange: setPagination,
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getSortedRowModel: getSortedRowModel(),
    onColumnOrderChange: layout.setColumnOrder,
    onColumnVisibilityChange: layout.setColumnVisibility,
  });

  if (!isLoading && !isStaff) return <AccessDenied resource="the platform audit trail" />;
  if (error) return <LoadError message={error.message} onRetry={() => mutate()} />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Platform audit" count={data?.total} />

      <div className="space-y-3 rounded-lg border bg-card p-4 shadow-sm">
        {/* These narrow the whole trail on the server, so they sit above the table rather than
            in its toolbar, whose search and funnels only narrow the page in hand. */}
        <div className="flex flex-wrap items-center gap-2">
          <Select value={agency} onValueChange={(v) => { setAgency(v); reset(); }}>
            <SelectTrigger className="h-8 w-[160px]" aria-label="Agency">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>All agencies</SelectItem>
              {(data?.agencies ?? []).map((o) => (
                <SelectItem key={o.value} value={o.value}>
                  {o.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select value={user} onValueChange={(v) => { setUser(v); reset(); }}>
            <SelectTrigger className="h-8 w-[160px]" aria-label="Platform team member">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>Anyone</SelectItem>
              {(data?.users ?? []).map((o) => (
                <SelectItem key={o.value} value={o.value}>
                  {o.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Input
            type="date"
            aria-label="From"
            title="From"
            className="h-8 w-[140px]"
            value={from}
            onChange={(e) => { setFrom(e.target.value); reset(); }}
          />
          <Input
            type="date"
            aria-label="To"
            title="To"
            className="h-8 w-[140px]"
            value={to}
            onChange={(e) => { setTo(e.target.value); reset(); }}
          />
          <Button
            size="sm"
            variant={platformOnly ? "default" : "outline"}
            className={platformOnly ? "h-8 bg-green-800 text-white hover:bg-green-900" : "h-8"}
            onClick={() => { setPlatformOnly(!platformOnly); reset(); }}
          >
            {platformOnly ? "Platform team only" : "Everyone"}
          </Button>
        </div>
        <DataTable
          layout={layout}
          rowClickOpensActions
          table={table}
          enableGlobalFilter
          searchPlaceholder="Search this page…"
          paginated
          paginationProps={{ pageSizeOptions: [20, 50, 100] }}
          exportFileName="platform-audit"
          filters={filters}
          onFiltersChange={setFilters}
          emptyMessage="Nothing recorded for these filters."
        />
      </div>
    </div>
  );
}
