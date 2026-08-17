"use client";

import { useState, useMemo } from "react";
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
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { MoreHorizontal, Pencil, Trash2, Users, Shield } from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { DeleteDialog } from "@/components/ui/delete-dialog";
import { CreateRoleSheet } from "@/components/roles/create-role-sheet";
import { toast } from "sonner";
import { PageHeader } from "@/components/ui/page-header";

interface RoleRow {
  id: string;
  name: string;
  code: string;
  description: string | null;
  isSystemRole: boolean;
  isActive: boolean;
  sortOrder: number;
  permissions: string[];
  userCount: number;
}

const fetcher = (url: string) => fetch(url).then(r => r.json());

export default function RolesPage() {
  const [sorting, setSorting] = useState<SortingState>([]);
  const [globalFilter, setGlobalFilter] = useState("");
  const [createOpen, setCreateOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const { data, mutate } = useSWR("/api/proxy/roles", fetcher, { revalidateOnFocus: false });
  const roles: RoleRow[] = data?.data || [];

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
      header: ({ column }) => <DataTableColumnHeader column={column} title="Role Name" />,
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
      accessorKey: "code",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Code" />,
      cell: ({ getValue }) => <code className="text-xs bg-muted px-1.5 py-0.5 rounded">{getValue() as string}</code>,
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
      header: ({ column }) => <DataTableColumnHeader column={column} title="Status" />,
      cell: ({ getValue }) => (
        <Badge variant={getValue() ? "default" : "outline"} className={getValue() ? "bg-green-100 text-green-800" : ""}>
          {getValue() ? "Active" : "Inactive"}
        </Badge>
      ),
    },
    {
      id: "actions",
      header: "Actions",
      cell: ({ row }) => (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon" className="h-8 w-8">
              <MoreHorizontal className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={() => toast.info("Edit permissions — coming soon")}>
              <Pencil className="h-4 w-4 mr-2" /> Edit Permissions
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => toast.info("View users — coming soon")}>
              <Users className="h-4 w-4 mr-2" /> View Users
            </DropdownMenuItem>
            {!row.original.isSystemRole && (
              <>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  className="text-destructive"
                  onClick={() => setDeleteTarget({ id: row.original.id, name: row.original.name })}
                >
                  <Trash2 className="h-4 w-4 mr-2" /> Delete
                </DropdownMenuItem>
              </>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      ),
      size: 60,
      enableSorting: false,
    },
  ], []);

  const table = useReactTable({
    data: roles,
    columns,
    state: { sorting, globalFilter },
    onSortingChange: setSorting,
    onGlobalFilterChange: setGlobalFilter,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    initialState: { pagination: { pageSize: 10 } },
  });

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Roles & Permissions"
        description="Define custom roles and assign permissions for your agency"
      />

      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
          table={table}
          enableGlobalFilter={true}
          searchPlaceholder="Search roles..."
          paginated={true}
          toolbarEndActions={
            <Button size="sm" className="h-8 bg-green-800 hover:bg-green-900 text-white" onClick={() => setCreateOpen(true)}>
              <span className="mr-1">+</span> Create Role
            </Button>
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
