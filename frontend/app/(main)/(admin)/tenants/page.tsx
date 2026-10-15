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
import { MoreHorizontal, Building2, Pencil, Users, Power, Trash2 } from "lucide-react";
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
import { DeleteDialog } from "@/components/ui/delete-dialog";
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
}

const fetcher = (url: string) => fetch(url).then(r => r.json());

const STATUS_MAP: Record<number, { label: string; variant: "default" | "outline" | "destructive" }> = {
  0: { label: "Active", variant: "default" },
  1: { label: "Suspended", variant: "outline" },
  2: { label: "Deactivated", variant: "destructive" },
};

export default function TenantsPage() {
  // Creating, suspending and deleting an agency is platform work: /api/tenants is behind the
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
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

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

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setIsDeleting(true);
    const res = await fetch(`/api/proxy/tenants/${deleteTarget.id}`, { method: "DELETE" });
    if (res.ok) {
      mutate();
      toast.success("Agency deleted");
    } else {
      toast.error("Failed to delete agency");
    }
    setIsDeleting(false);
    setDeleteTarget(null);
  };

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
      cell: ({ getValue }) => {
        const status = getValue() as number;
        const s = STATUS_MAP[status] || { label: "Unknown", variant: "outline" as const };
        return <Badge variant={s.variant} className={status === 0 ? "bg-green-100 text-green-800" : ""}>{s.label}</Badge>;
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
          {/* Edit and Delete together at the top, then a rule, then the rest — the shape every
              row menu in the app uses, so the destructive item is always in the same place. */}
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={() => setEditAgencyId(row.original.id)}>
              <Pencil className="h-4 w-4 mr-2" /> Edit
            </DropdownMenuItem>
            <DropdownMenuItem className="text-destructive" onClick={() => setDeleteTarget({ id: row.original.id, name: row.original.name })}>
              <Trash2 className="h-4 w-4 mr-2" /> Delete
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => window.location.href = `/staff?tenant=${row.original.id}`}>
              <Users className="h-4 w-4 mr-2" /> Manage Users
            </DropdownMenuItem>
            {row.original.status === 0 ? (
              <DropdownMenuItem onClick={() => handleStatusChange(row.original.id, 1)}>
                <Power className="h-4 w-4 mr-2" /> Suspend
              </DropdownMenuItem>
            ) : (
              <DropdownMenuItem onClick={() => handleStatusChange(row.original.id, 0)}>
                <Power className="h-4 w-4 mr-2" /> Activate
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
      <DeleteDialog
        open={!!deleteTarget}
        onOpenChange={(open) => { if (!open) setDeleteTarget(null); }}
        title="Delete agency"
        description={`Are you sure you want to delete '${deleteTarget?.name}'? All data for this agency will be deactivated. This action cannot be undone.`}
        onConfirm={confirmDelete}
        isDeleting={isDeleting}
      />
    </div>
  );
}
