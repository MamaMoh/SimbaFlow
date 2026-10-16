"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
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
import { Copy, Filter, Loader2, MoreHorizontal, UserRound, UserSearch } from "lucide-react";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { PageHeader } from "@/components/ui/page-header";
import { AgencyAccessLog } from "@/components/platform/agency-access-log";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import {
  useAuditTrail,
  type AuditTrailEntry,
  type AuditTrailKind,
} from "@/lib/api/audit";
import { cn } from "@/lib/utils";
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

const ANY = "all";

/** A row as the table reads it, with the words its funnels compare against. */
type AuditRow = AuditTrailEntry & { on: string; result: string };

function When({ iso }: { iso: string }) {
  const d = new Date(iso);
  return (
    <span className="whitespace-nowrap tabular-nums" title={d.toISOString()}>
      {d.toLocaleDateString()} {d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
    </span>
  );
}

function Subject({ e }: { e: AuditTrailEntry }) {
  if (!e.entityType && !e.entityId) return <span className="text-muted-foreground">—</span>;
  const shortId = e.entityId ? (
    <span className="font-mono text-xs text-muted-foreground">{e.entityId.slice(0, 8)}</span>
  ) : null;
  const text = e.entityLabel ? (
    <span className="font-medium">{e.entityLabel}</span>
  ) : (
    <span>
      {e.entityType} {shortId}
    </span>
  );
  if (e.entityType === "Candidate" && e.entityId) {
    return (
      <Link href={`/candidates/${e.entityId}`} className="hover:underline">
        {text}
      </Link>
    );
  }
  return text;
}

/** What was asked for, as the server recorded it. Passwords and codes arrive already blanked. */
function Details({ json }: { json?: string | null }) {
  if (!json) return null;
  let pretty = json;
  try {
    pretty = JSON.stringify(JSON.parse(json), null, 2);
  } catch {
    // Shown as stored.
  }
  return (
    <details className="mt-1 text-xs text-muted-foreground">
      <summary className="cursor-pointer select-none">Details</summary>
      <pre className="mt-1 max-h-64 max-w-md overflow-auto whitespace-pre-wrap break-words rounded bg-muted/50 p-2 font-mono">
        {pretty}
      </pre>
    </details>
  );
}

/**
 * The agency's audit trail. It is too long to send whole, so the API pages it: the tabs, person,
 * action, dates and failed-only switch travel with each request and narrow the whole trail, and
 * sit above the table for that reason. The search box and the funnels narrow the page in hand,
 * which is why the box says so.
 */
export default function AuditTrailPage() {
  const router = useRouter();
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canRead = hasPermission("audit.read") || hasPermission("system.admin");

  const [kind, setKind] = useState<AuditTrailKind>("changes");
  const [userId, setUserId] = useState(ANY);
  const [action, setAction] = useState(ANY);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [failedOnly, setFailedOnly] = useState(false);
  const [pagination, setPagination] = useState<PaginationState>({ pageIndex: 0, pageSize: 50 });
  const [sorting, setSorting] = useState<SortingState>([]);
  const { globalFilter, setGlobalFilter, filters, setFilters } = useBoardQuery();

  const { data, error, isLoading, isValidating, mutate } = useAuditTrail(
    {
      kind,
      page: pagination.pageIndex + 1,
      pageSize: pagination.pageSize,
      userId: userId === ANY ? undefined : userId,
      action: action === ANY ? undefined : action,
      from: from || undefined,
      to: to || undefined,
      failedOnly,
    },
    !permsLoading && canRead
  );

  const firstPage = () => setPagination((p) => ({ ...p, pageIndex: 0 }));

  // Any change of filter starts again from the first page.
  const refilter = <T,>(set: (v: T) => void) => (v: T) => {
    set(v);
    firstPage();
  };

  const rows = useMemo<AuditRow[]>(
    () =>
      applyColumnFilters(
        (data?.items ?? []).map((e) => ({
          ...e,
          on: e.entityLabel ?? [e.entityType, e.entityId?.slice(0, 8)].filter(Boolean).join(" "),
          result: e.success ? "OK" : "Failed",
        })),
        filters,
        { at: "date", userName: "text", action: "text", on: "text", result: "text", ipAddress: "text" },
      ),
    [data, filters],
  );

  const userIds = useMemo(() => new Set((data?.users ?? []).map((u) => u.value)), [data]);
  const actionIds = useMemo(() => new Set((data?.actions ?? []).map((a) => a.value)), [data]);

  const columns: ColumnDef<AuditRow>[] = useMemo(() => {
    const copy = async (e: AuditRow) => {
      const text = [new Date(e.at).toLocaleString(), e.userName, e.action, e.on, e.success ? "OK" : "Failed", e.details]
        .filter(Boolean)
        .join(" · ");
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
        cell: ({ row }) => <span className="text-muted-foreground"><When iso={row.original.at} /></span>,
      },
      {
        accessorKey: "userName",
        meta: { filter: { field: "userName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Who" />,
        cell: ({ row }) => (
          <div>
            <div className="font-medium">{row.original.userName}</div>
            {row.original.userEmail && (
              <div className="text-xs text-muted-foreground">{row.original.userEmail}</div>
            )}
          </div>
        ),
      },
      {
        accessorKey: "action",
        meta: { filter: { field: "action", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Action" />,
        cell: ({ row }) => (
          <div title={row.original.rawAction}>
            {row.original.action}
            <Details json={row.original.details} />
          </div>
        ),
      },
      {
        accessorKey: "on",
        meta: { filter: { field: "on", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="On" />,
        cell: ({ row }) => <Subject e={row.original} />,
      },
      {
        accessorKey: "result",
        meta: { filter: { field: "result", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Result" />,
        cell: ({ row }) => (
          <span
            className={cn(
              "rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide",
              row.original.success
                ? "bg-emerald-500/15 text-emerald-600 dark:text-emerald-400"
                : "bg-rose-500/15 text-rose-600 dark:text-rose-400"
            )}
          >
            {row.original.success ? "OK" : "Failed"}
          </span>
        ),
      },
      {
        accessorKey: "ipAddress",
        meta: { filter: { field: "ipAddress", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="From" />,
        cell: ({ row }) => (
          <span className="font-mono text-xs text-muted-foreground" title={row.original.userAgent ?? undefined}>
            {row.original.ipAddress ?? "—"}
          </span>
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
                {e.entityType === "Candidate" && e.entityId && (
                  <DropdownMenuItem onClick={() => router.push(`/candidates/${e.entityId}`)}>
                    <UserSearch className="mr-2 h-4 w-4" /> Open candidate
                  </DropdownMenuItem>
                )}
                <DropdownMenuItem onClick={() => copy(e)}>
                  <Copy className="mr-2 h-4 w-4" /> Copy entry
                </DropdownMenuItem>
                {e.userId && userIds.has(e.userId) && (
                  <DropdownMenuItem onClick={() => { setUserId(e.userId!); firstPage(); }}>
                    <UserRound className="mr-2 h-4 w-4" /> Only {e.userName}
                  </DropdownMenuItem>
                )}
                {actionIds.has(e.rawAction) && (
                  <DropdownMenuItem onClick={() => { setAction(e.rawAction); firstPage(); }}>
                    <Filter className="mr-2 h-4 w-4" /> Only &ldquo;{e.action}&rdquo;
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
  }, [router, userIds, actionIds]);

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "audit-trail", columnIds });

  const table = useReactTable({
    data: rows,
    columns,
    state: {
      sorting,
      globalFilter,
      pagination,
      // Views and prints are never refused, so they have no result to show.
      columnOrder: layout.columnOrder,
      columnVisibility: { ...layout.columnVisibility, ...(kind === "views" ? { result: false } : {}) },
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

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }

  if (!canRead) return <AccessDenied resource="the audit trail" />;

  const total = data?.total ?? 0;
  const filtered = userId !== ANY || action !== ANY || from || to || failedOnly;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Audit trail" count={data ? total : undefined} />

      <AgencyAccessLog />

      {error && <LoadError message={error.message} onRetry={() => mutate()} />}

      <div className="space-y-3 rounded-lg border bg-card p-4 shadow-sm">
        <Tabs
          value={kind}
          onValueChange={(v) => {
            setKind(v as AuditTrailKind);
            // The two logs record different actions, so an action picked in one means nothing in the other.
            setAction(ANY);
            firstPage();
          }}
        >
          <TabsList>
            <TabsTrigger value="changes" className="px-4">Changes</TabsTrigger>
            <TabsTrigger value="views" className="px-4">Views &amp; prints</TabsTrigger>
          </TabsList>
        </Tabs>

        {/* These narrow the whole trail on the server, so they sit above the table rather than
            in its toolbar, whose search and funnels only narrow the page in hand. */}
        <div className="flex flex-wrap items-center gap-2">
          <Select value={userId} onValueChange={refilter(setUserId)}>
            <SelectTrigger aria-label="Person" className="h-8 w-[180px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>Anyone</SelectItem>
              {(data?.users ?? []).map((u) => (
                <SelectItem key={u.value} value={u.value}>{u.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Select value={action} onValueChange={refilter(setAction)}>
            <SelectTrigger aria-label="Action" className="h-8 w-[200px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>Any action</SelectItem>
              {(data?.actions ?? []).map((a) => (
                <SelectItem key={a.value} value={a.value}>{a.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Input
            type="date"
            aria-label="From"
            title="From"
            className="h-8 w-[140px]"
            value={from}
            max={to || undefined}
            onChange={(e) => refilter(setFrom)(e.target.value)}
          />
          <Input
            type="date"
            aria-label="To"
            title="To"
            className="h-8 w-[140px]"
            value={to}
            min={from || undefined}
            onChange={(e) => refilter(setTo)(e.target.value)}
          />
          {kind === "changes" && (
            <div className="flex h-8 items-center gap-2 px-1">
              <Switch id="audit-failed" checked={failedOnly} onCheckedChange={refilter(setFailedOnly)} />
              <Label htmlFor="audit-failed" className="text-sm">Failed only</Label>
            </div>
          )}
          {filtered && (
            <Button
              variant="ghost"
              size="sm"
              className="h-8"
              onClick={() => {
                setUserId(ANY);
                setAction(ANY);
                setFrom("");
                setTo("");
                setFailedOnly(false);
                firstPage();
              }}
            >
              Clear
            </Button>
          )}
        </div>

        <div className={cn(isValidating && "opacity-70")}>
          <DataTable
            layout={layout}
            rowClickOpensActions
            table={table}
            enableGlobalFilter
            searchPlaceholder="Search this page…"
            paginated
            paginationProps={{ pageSizeOptions: [20, 50, 100] }}
            exportFileName={kind === "views" ? "audit-views" : "audit-changes"}
            filters={filters}
            onFiltersChange={setFilters}
            emptyState={
              isLoading && !data ? (
                <div className="flex items-center justify-center p-10 text-sm text-muted-foreground">
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Loading the audit trail…
                </div>
              ) : undefined
            }
            emptyMessage={
              filtered
                ? "Nothing matches these filters."
                : kind === "views"
                  ? "No views or prints recorded yet."
                  : "No changes recorded yet."
            }
          />
        </div>
      </div>
    </div>
  );
}
