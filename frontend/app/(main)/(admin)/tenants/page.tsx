"use client";

import { useState, useMemo } from "react";
import { useTableRows } from "@/lib/data/use-table-rows";
import useSWR from "swr";
import {
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  type ColumnDef,
  type SortingState,
} from "@tanstack/react-table";
import { DataTable } from "@/components/data-table/data-table";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import { useBoardQuery } from "@/lib/data-table/use-board-query";
import { applyColumnFilters } from "@/lib/data-table/apply-filters";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { MoreHorizontal, Building2, Pencil, Users, Power, Trash2, Download, RotateCcw } from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { CreateAgencySheet } from "@/components/tenants/create-agency-sheet";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { EditAgencySheet } from "@/components/tenants/edit-agency-sheet";
import { OffboardDialog, PurgeDialog, type OffboardTarget } from "@/components/tenants/offboarding-dialogs";
import { filenameFromResponse, saveFile } from "@/lib/files/download";
import { toast } from "sonner";
import { PageHeader } from "@/components/ui/page-header";

interface AgencyRow {
  id: string;
  name: string;
  slug: string;
  schemaName: string;
  contactEmail: string;
  status: number;
  provisionedAt: string;
  agencyLevel?: number;
  maxPartnersPerCountry?: number;
  maxCountries?: number | null;
  licenseNumber?: string | null;
  licensedCountryCount?: number;
  offboardedAt?: string | null;
  offboardingReason?: string | null;
  lastExportedAt?: string | null;
}

const fetcher = (url: string) => fetch(url).then(r => r.json());

const STATUS_MAP: Record<number, { label: string; variant: "default" | "outline" | "destructive" }> = {
  0: { label: "Active", variant: "default" },
  1: { label: "Suspended", variant: "outline" },
  2: { label: "Offboarded", variant: "destructive" },
};

export default function TenantsPage() {
  // Creating, suspending and offboarding an agency is platform work: /api/tenants is behind the
  // SuperAdmin policy end to end. Anyone else reaching this page gets a table of buttons that all
  // come back 403, so say so once instead.
  const { isSuperAdmin, isLoading: permsLoading } = usePermissions();

  const [sorting, setSorting] = useState<SortingState>([]);
  // The same search box and the same funnels as every other board. This list arrives whole —
  // there is no page two — so the rows in hand are all the rows there are, and the filters are
  // answered here rather than asked of the API.
  const { globalFilter, setGlobalFilter, filters, setFilters } = useBoardQuery();
  const [createOpen, setCreateOpen] = useState(false);
  const [editAgencyId, setEditAgencyId] = useState<string | null>(null);
  const [offboardTarget, setOffboardTarget] = useState<OffboardTarget | null>(null);
  const [purgeTarget, setPurgeTarget] = useState<OffboardTarget | null>(null);
  const [isPending, setIsPending] = useState(false);

  const { data, mutate } = useSWR("/api/proxy/tenants", fetcher, { revalidateOnFocus: false });
  const all = useTableRows<AgencyRow>(data?.data);
  const agencies = useMemo(
    () =>
      applyColumnFilters(
        all,
        filters,
        {
          name: "text",
          slug: "text",
          agencyLevel: "number",
          contactEmail: "text",
          status: "text",
          provisionedAt: "date",
        },
        // Status is held as a number and shown as a word, so it is matched against the word.
        (row, field) =>
          field === "status"
            ? (STATUS_MAP[row.status]?.label ?? "Unknown")
            : (row as unknown as Record<string, unknown>)[field],
      ),
    [all, filters],
  );

  const handleStatusChange = async (id: string, status: number) => {
    const res = await fetch(`/api/proxy/tenants/${id}/status`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ status }),
    });
    const result = await res.json();
    if (result.isSuccess) {
      mutate();
      toast.success("Agency status updated");
    } else {
      toast.error(result.error || "Failed to update status");
    }
  };

  const confirmOffboard = async (reason: string) => {
    if (!offboardTarget) return;
    setIsPending(true);
    const res = await fetch(`/api/proxy/tenants/${offboardTarget.id}/offboard`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ reason }),
    });
    const result = await res.json().catch(() => ({}));
    if (res.ok && result.isSuccess) {
      mutate();
      toast.success(`${offboardTarget.name} offboarded. Its data is kept until you purge it.`);
      setOffboardTarget(null);
    } else {
      toast.error(result.error || "Failed to offboard agency");
    }
    setIsPending(false);
  };

  // The whole agency as one zip: every table, its platform records, and every file. Built on the
  // server before it is sent, so a large agency takes a moment.
  const handleExport = async (row: AgencyRow) => {
    const id = toast.loading(`Preparing the export of ${row.name}…`);
    try {
      const res = await fetch(`/api/proxy/tenants/${row.id}/export`);
      if (!res.ok) {
        const body = await res.json().catch(() => ({}));
        throw new Error(body?.error || "Export failed");
      }
      const blob = await res.blob();
      saveFile(new File([blob], filenameFromResponse(res, `${row.slug}-export.zip`), { type: "application/zip" }));
      toast.success(`Exported ${row.name}`, { id });
      mutate();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Export failed", { id });
    }
  };

  const confirmPurge = async (confirmSlug: string) => {
    if (!purgeTarget) return;
    setIsPending(true);
    const res = await fetch(`/api/proxy/tenants/${purgeTarget.id}/purge`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ confirmSlug }),
    });
    const result = await res.json().catch(() => ({}));
    if (res.ok && result.isSuccess) {
      mutate();
      toast.success(`${purgeTarget.name} purged`);
      setPurgeTarget(null);
    } else {
      toast.error(result.error || "Failed to purge agency");
    }
    setIsPending(false);
  };

  // Purging needs an export taken after offboarding; the server enforces it, this says why the
  // item is not offered yet.
  const exportedSinceOffboarding = (row: AgencyRow) =>
    !!row.offboardedAt && !!row.lastExportedAt && new Date(row.lastExportedAt) >= new Date(row.offboardedAt);

  const columns: ColumnDef<AgencyRow>[] = useMemo(() => [
    {
      id: "index",
      header: "#",
      cell: ({ row }) => <span className="text-muted-foreground">{row.index + 1}</span>,
      size: 40,
      enableSorting: false,
    },
    {
      accessorKey: "name",
      meta: { filter: { field: "name", kind: "text" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Agency name" />,
      cell: ({ row }) => (
        <div className="flex items-center gap-2">
          <Building2 className="h-4 w-4 text-green-700" />
          <span className="font-medium">{row.original.name}</span>
        </div>
      ),
    },
    {
      accessorKey: "slug",
      meta: { filter: { field: "slug", kind: "text" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="URL identifier" />,
      cell: ({ getValue }) => <code className="text-xs bg-muted px-1.5 py-0.5 rounded">{getValue() as string}</code>,
    },
    {
      accessorKey: "agencyLevel",
      meta: { filter: { field: "agencyLevel", kind: "number" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Level" />,
      cell: ({ row }) => {
        const level = row.original.agencyLevel ?? "—";
        const per = row.original.maxPartnersPerCountry;
        return (
          <div className="text-sm">
            <span className="font-medium">L{level}</span>
            {per != null ? (
              <span className="text-muted-foreground text-xs block">≤{per}/country</span>
            ) : null}
          </div>
        );
      },
    },
    {
      accessorKey: "contactEmail",
      meta: { filter: { field: "contactEmail", kind: "text" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Contact email" />,
    },
    {
      accessorKey: "status",
      meta: { filter: { field: "status", kind: "text" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Status" />,
      cell: ({ row, getValue }) => {
        const status = getValue() as number;
        const s = STATUS_MAP[status] || { label: "Unknown", variant: "outline" as const };
        const badge = <Badge variant={s.variant} className={status === 0 ? "bg-green-100 text-green-800" : ""}>{s.label}</Badge>;
        if (status !== 2 || !row.original.offboardedAt) return badge;
        return (
          <div className="text-sm" title={row.original.offboardingReason ?? undefined}>
            {badge}
            <span className="text-muted-foreground text-xs block">
              {new Date(row.original.offboardedAt).toLocaleDateString()}
              {row.original.lastExportedAt ? " · exported" : ""}
            </span>
          </div>
        );
      },
    },
    {
      accessorKey: "provisionedAt",
      meta: { filter: { field: "provisionedAt", kind: "date" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Created" />,
      cell: ({ getValue }) => new Date(getValue() as string).toLocaleDateString(),
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
          {/* Edit and the destructive item together at the top, then a rule, then the rest — the
              shape every row menu in the app uses. An agency still in service is offboarded; one
              already offboarded can only then be purged. */}
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={() => setEditAgencyId(row.original.id)}>
              <Pencil className="h-4 w-4 mr-2" /> Edit
            </DropdownMenuItem>
            {row.original.status !== 2 ? (
              <DropdownMenuItem
                className="text-destructive"
                onClick={() => setOffboardTarget({ id: row.original.id, name: row.original.name, slug: row.original.slug })}
              >
                <Power className="h-4 w-4 mr-2" /> Offboard…
              </DropdownMenuItem>
            ) : (
              <DropdownMenuItem
                className="text-destructive"
                disabled={!exportedSinceOffboarding(row.original)}
                onClick={() => setPurgeTarget({ id: row.original.id, name: row.original.name, slug: row.original.slug })}
              >
                <Trash2 className="h-4 w-4 mr-2" />
                {exportedSinceOffboarding(row.original) ? "Purge data…" : "Purge data (export first)"}
              </DropdownMenuItem>
            )}
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => window.location.href = `/staff?tenant=${row.original.id}`}>
              <Users className="h-4 w-4 mr-2" /> Manage Users
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => handleExport(row.original)}>
              <Download className="h-4 w-4 mr-2" /> Export data
            </DropdownMenuItem>
            {row.original.status === 0 ? (
              <DropdownMenuItem onClick={() => handleStatusChange(row.original.id, 1)}>
                <Power className="h-4 w-4 mr-2" /> Suspend
              </DropdownMenuItem>
            ) : row.original.status === 1 ? (
              <DropdownMenuItem onClick={() => handleStatusChange(row.original.id, 0)}>
                <Power className="h-4 w-4 mr-2" /> Activate
              </DropdownMenuItem>
            ) : (
              <DropdownMenuItem onClick={() => handleStatusChange(row.original.id, 0)}>
                <RotateCcw className="h-4 w-4 mr-2" /> Reactivate
              </DropdownMenuItem>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      ),
      size: 60,
      enableSorting: false,
    },
  ], []);

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "tenants", columnIds });

  const table = useReactTable({
    data: agencies,
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

  if (!permsLoading && !isSuperAdmin) {
    return (
      <div className="flex flex-col gap-6">
        <PageHeader title="Agencies" />
        <div className="rounded-lg border bg-card p-8 text-center text-sm text-muted-foreground">
          Managing agencies is reserved for platform administrators.
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Agencies" />

      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
          layout={layout}
            rowClickOpensActions
          table={table}
          enableGlobalFilter={true}
          searchPlaceholder="Search agencies…"
          paginated={true}
          exportFileName="agencies"
          filters={filters}
          onFiltersChange={setFilters}
          createAction={
            <Button
              size="sm"
              className="h-8 bg-green-800 text-white hover:bg-green-900"
              onClick={() => setCreateOpen(true)}
            >
              <span className="mr-1">+</span> Create agency
            </Button>
          }
        />
      </div>

      <CreateAgencySheet open={createOpen} onOpenChange={setCreateOpen} onCreated={() => mutate()} />
      <EditAgencySheet
        agencyId={editAgencyId}
        open={!!editAgencyId}
        onOpenChange={(open) => { if (!open) setEditAgencyId(null); }}
        onUpdated={() => mutate()}
      />
      <OffboardDialog
        target={offboardTarget}
        onOpenChange={(open) => { if (!open) setOffboardTarget(null); }}
        onConfirm={confirmOffboard}
        isPending={isPending}
      />
      <PurgeDialog
        target={purgeTarget}
        onOpenChange={(open) => { if (!open) setPurgeTarget(null); }}
        onConfirm={confirmPurge}
        isPending={isPending}
      />
    </div>
  );
}
