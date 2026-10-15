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
import { MoreHorizontal, Trash2, Users, Shield } from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { DeleteDialog } from "@/components/ui/delete-dialog";
import { CreateRoleSheet } from "@/components/roles/create-role-sheet";
import { toast } from "sonner";
import { AccessDenied } from "@/components/ui/page-alert";
import { PageHeader } from "@/components/ui/page-header";
import { usePermissions } from "@/lib/tenant/tenant-provider";

interface RoleRow {
  id: string;
  name: string;
  description: string | null;
  isSystemRole: boolean;
  isActive: boolean;
  permissions: string[];
  userCount: number;
}

const fetcher = (url: string) => fetch(url).then(r => r.json());

export default function RolesPage() {
  // The platform team's page, not an agency's. The role catalogue defines what every permission
  // means and who may hold it — including the permission to edit roles — so an agency owner who
  // could open this could grant their own account anything. The API refuses them outright now;
  // this is so they meet a sentence rather than an empty table.
  const { isSuperAdmin } = usePermissions();
  const canManage = isSuperAdmin;

  const [sorting, setSorting] = useState<SortingState>([]);
  // The same search box and the same funnels as every other board. This list arrives whole —
  // there is no page two — so the rows in hand are all the rows there are, and the filters are
  // answered here rather than asked of the API.
  const { globalFilter, setGlobalFilter, filters, setFilters } = useBoardQuery();
  const [createOpen, setCreateOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const { data, mutate } = useSWR(isSuperAdmin ? "/api/proxy/roles" : null, fetcher, {
    revalidateOnFocus: false,
  });
  const all = useTableRows<RoleRow>(data?.data);
  const roles = useMemo(
    () => applyColumnFilters(all, filters, { name: "text", description: "text",
      userCount: "number", isActive: "text" },
      // Status is shown as a word and held as a yes or no, so it is matched against the word.
      (row, field) => (field === "isActive" ? (row.isActive ? "Active" : "Inactive") : (row as any)[field])),
    [all, filters],
  );

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setIsDeleting(true);
    const res = await fetch(`/api/proxy/roles/${deleteTarget.id}`, { method: "DELETE" });
    if (res.ok) {
      mutate();
      toast.success("Role deleted");
    } else {
      const err = await res.json().catch(() => null);
      toast.error(err?.error || "Failed to delete role");
    }
    setIsDeleting(false);
    setDeleteTarget(null);
  };

  const columns: ColumnDef<RoleRow>[] = useMemo(() => [
    {
      id: "index",
      header: "#",
      cell: ({ row }) => <span className="text-muted-foreground">{row.index + 1}</span>,
      size: 40,
      enableSorting: false,
    },
    {
      accessorKey: "name",
      // Permissions has no funnel: the cell counts a list rather than showing a column, and
      // "contains 4" is not a question anybody is asking of it.
      meta: { filter: { field: "name", kind: "text" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Role name" />,
      cell: ({ row }) => (
        <div className="flex items-center gap-2">
          <Shield className="h-4 w-4 text-green-700" />
          <span className="font-medium">{row.original.name}</span>
          {row.original.isSystemRole && (
            <Badge variant="secondary" className="text-[10px]">System</Badge>
          )}
        </div>
      ),
    },
    {
      accessorKey: "description",
      meta: { filter: { field: "description", kind: "text" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Description" />,
      cell: ({ getValue }) => (
        <span className="text-sm text-muted-foreground">{(getValue() as string) || "—"}</span>
      ),
    },
    {
      accessorKey: "permissions",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Permissions" />,
      cell: ({ getValue }) => {
        const perms = getValue() as string[];
        return <span className="text-sm text-muted-foreground">{perms.length} permissions</span>;
      },
      enableSorting: false,
    },
    {
      accessorKey: "userCount",
      meta: { filter: { field: "userCount", kind: "number" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Users" />,
      cell: ({ getValue }) => (
        <div className="flex items-center gap-1">
          <Users className="h-3.5 w-3.5 text-muted-foreground" />
          <span>{getValue() as number}</span>
        </div>
      ),
    },
    {
      accessorKey: "isActive",
      meta: { filter: { field: "isActive", kind: "text" } },
      header: ({ column }) => <DataTableColumnHeader column={column} title="Status" />,
      cell: ({ getValue }) => (
        <Badge variant={getValue() ? "default" : "outline"} className={getValue() ? "bg-green-100 text-green-800" : ""}>
          {getValue() ? "Active" : "Inactive"}
        </Badge>
      ),
    },
    {
      // Delete is the only thing this menu ever did. "Edit Permissions" and "View Users" were
      // toasts saying "coming soon" — a menu that answers a click with an apology is worse than a
      // shorter menu, and the counts those two promised are already columns in this table.
      id: "actions",
      header: "Actions",
      cell: ({ row }) =>
        !canManage || row.original.isSystemRole ? null : (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon" className="h-8 w-8" aria-label="Row actions">
                <MoreHorizontal className="h-4 w-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuItem
                className="text-destructive"
                onClick={() => setDeleteTarget({ id: row.original.id, name: row.original.name })}
              >
                <Trash2 className="h-4 w-4 mr-2" /> Delete
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        ),
      size: 60,
      enableSorting: false,
    },
  ], [canManage]);

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "roles", columnIds });

  const table = useReactTable({
    data: roles,
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

  if (!isSuperAdmin) {
    return <AccessDenied resource="roles and permissions" />;
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Roles & permissions" />

      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
          layout={layout}
            rowClickOpensActions
          table={table}
          enableGlobalFilter={true}
          searchPlaceholder="Search roles…"
          paginated={true}
          exportFileName="roles"
          filters={filters}
          onFiltersChange={setFilters}
          createAction={
            canManage ? (
              <Button
                size="sm"
                className="h-8 bg-green-800 text-white hover:bg-green-900"
                onClick={() => setCreateOpen(true)}
              >
                <span className="mr-1">+</span> Create role
              </Button>
            ) : null
          }
        />
      </div>

      <CreateRoleSheet open={createOpen} onOpenChange={setCreateOpen} onCreated={() => mutate()} />
      <DeleteDialog
        open={!!deleteTarget}
        onOpenChange={(open) => { if (!open) setDeleteTarget(null); }}
        title="Delete role"
        description={`Are you sure you want to delete role '${deleteTarget?.name}'? Users assigned to this role will lose these permissions.`}
        onConfirm={confirmDelete}
        isDeleting={isDeleting}
      />
    </div>
  );
}
