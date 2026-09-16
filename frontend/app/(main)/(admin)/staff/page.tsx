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
import { MoreHorizontal, Shield, ShieldOff, Pencil, KeyRound, Trash2 } from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { CreateUserSheet } from "@/components/users/create-user-sheet";
import { EditUserSheet, type EditableUser } from "@/components/users/edit-user-sheet";
import {
  ResetPasswordDialog,
  type ResetPasswordTarget,
} from "@/components/users/reset-password-dialog";
import { DeleteDialog } from "@/components/ui/delete-dialog";
import { PageHeader } from "@/components/ui/page-header";
import { usePermissions } from "@/lib/tenant/tenant-provider";

interface UserRow {
  id: string;
  username: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string | null;
  isActive: boolean;
  isSuperAdmin: boolean;
  twoFactorEnabled: boolean;
  departmentName: string | null;
  tenantId: string | null;
  tenantName: string | null;
  tenantRemoved: boolean;
  lastLoginAt: string | null;
  roles: string[];
}

const fetcher = (url: string) => fetch(url).then(res => res.json());

export default function StaffPage() {
  // This page is the user list, and every button on it writes to an account: activate, edit,
  // reset a password, delete. The sidebar shows it to anyone with staff.read — which the office
  // manager holds and users.read does not follow from — so the page has to ask for what it uses.
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("users.read");
  const canManage = hasPermission("users.write");

  const [sorting, setSorting] = useState<SortingState>([]);
  const [globalFilter, setGlobalFilter] = useState("");
  const [createOpen, setCreateOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<EditableUser | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [resetTarget, setResetTarget] = useState<ResetPasswordTarget | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const { data, isLoading, mutate } = useSWR(
    canView ? `/api/proxy/users?page=1&pageSize=100` : null,
    fetcher,
    { revalidateOnFocus: false }
  );

  const users: UserRow[] = data?.data?.items || [];

  const handleToggleStatus = async (id: string) => {
    await fetch(`/api/proxy/users/${id}/toggle-status`, { method: "PUT" });
    mutate();
  };

  const handleDelete = async (id: string, name: string) => {
    setDeleteTarget({ id, name });
  };

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setIsDeleting(true);
    await fetch(`/api/proxy/users/${deleteTarget.id}`, { method: "DELETE" });
    mutate();
    setIsDeleting(false);
    setDeleteTarget(null);
  };

  const columns: ColumnDef<UserRow>[] = useMemo(() => [
    {
      id: "index",
      header: "#",
      cell: ({ row }) => <span className="text-muted-foreground">{row.index + 1}</span>,
      size: 40,
      enableSorting: false,
    },
    {
      accessorKey: "username",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Username" />,
      cell: ({ row }) => (
        <div>
          <span className="font-medium">{row.original.username}</span>
          {row.original.isSuperAdmin && (
            <Badge variant="destructive" className="ml-2 text-[10px] px-1 py-0">Admin</Badge>
          )}
        </div>
      ),
    },
    {
      id: "fullName",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Full Name" />,
      accessorFn: (row) => `${row.firstName} ${row.lastName}`,
    },
    {
      accessorKey: "email",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Email" />,
    },
    {
      accessorKey: "tenantName",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Agency" />,
      cell: ({ row }) => {
        const { tenantName: name, tenantId, tenantRemoved } = row.original;
        // An agency that has been removed still has people pointing at it, which is why this list
        // can show more agencies than the Agencies page does. Saying so here is the difference
        // between a puzzle and a job to do.
        if (tenantRemoved) {
          return (
            <span className="flex items-center gap-1.5">
              <span className="text-sm text-muted-foreground line-through">{name ?? "Unknown"}</span>
              <Badge variant="outline" className="border-amber-300 bg-amber-50 text-[10px] text-amber-800">
                Removed
              </Badge>
            </span>
          );
        }
        return name ? (
          <span className="text-sm">{name}</span>
        ) : tenantId ? (
          <span className="text-sm text-muted-foreground">—</span>
        ) : (
          <Badge variant="outline" className="text-xs">Platform</Badge>
        );
      },
    },
    {
      accessorKey: "roles",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Roles" />,
      cell: ({ getValue }) => {
        const roles = getValue() as string[];
        return (
          <div className="flex flex-wrap gap-1">
            {roles.map(role => (
              <Badge key={role} variant="secondary" className="text-xs">{role}</Badge>
            ))}
          </div>
        );
      },
      enableSorting: false,
    },
    {
      accessorKey: "isActive",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Status" />,
      cell: ({ getValue }) => {
        const active = getValue() as boolean;
        return (
          <Badge variant={active ? "default" : "outline"} className={active ? "bg-green-100 text-green-800" : "text-muted-foreground"}>
            {active ? "Active" : "Inactive"}
          </Badge>
        );
      },
    },
    {
      accessorKey: "lastLoginAt",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Last Login" />,
      cell: ({ getValue }) => {
        const date = getValue() as string | null;
        return date ? new Date(date).toLocaleDateString() : "Never";
      },
    },
    {
      id: "actions",
      header: "Actions",
      cell: ({ row }) =>
        !canManage ? null : (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon" className="h-8 w-8" aria-label="Row actions">
              <MoreHorizontal className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={() => handleToggleStatus(row.original.id)}>
              {row.original.isActive ? (
                <><ShieldOff className="h-4 w-4 mr-2" /> Deactivate</>
              ) : (
                <><Shield className="h-4 w-4 mr-2" /> Activate</>
              )}
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => setEditTarget(row.original)}>
              <Pencil className="h-4 w-4 mr-2" /> Edit
            </DropdownMenuItem>
            <DropdownMenuItem
              onClick={() =>
                setResetTarget({
                  id: row.original.id,
                  username: row.original.username,
                  fullName: `${row.original.firstName} ${row.original.lastName}`,
                })
              }
            >
              <KeyRound className="h-4 w-4 mr-2" /> Reset Password
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => handleDelete(row.original.id, row.original.username)} className="text-destructive">
              <Trash2 className="h-4 w-4 mr-2" /> Delete User
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      ),
      size: 60,
      enableSorting: false,
    },
  ], [canManage]);

  const table = useReactTable({
    data: users,
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

  if (!permsLoading && !canView) {
    return (
      <div className="flex flex-col gap-6">
        <PageHeader title="Users & Staff" description="Manage system users, roles, and access" />
        <div className="rounded-lg border bg-card p-8 text-center text-sm text-muted-foreground">
          You do not have permission to view user accounts.
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Users & Staff"
        description="Manage system users, roles, and access"
      />

      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
            rowClickOpensActions
          table={table}
          enableGlobalFilter={true}
          searchPlaceholder="Search users..."
          paginated={true}
          toolbarEndActions={
            canManage ? (
              <Button size="sm" className="h-8 bg-green-800 hover:bg-green-900 text-white" onClick={() => setCreateOpen(true)}>
                <span className="mr-1">+</span> Create
              </Button>
            ) : null
          }
        />
      </div>

      <EditUserSheet
        user={editTarget}
        onOpenChange={(open) => { if (!open) setEditTarget(null); }}
        onSaved={() => mutate()}
      />
      <ResetPasswordDialog
        user={resetTarget}
        onOpenChange={(open) => { if (!open) setResetTarget(null); }}
        onReset={() => mutate()}
      />
      <CreateUserSheet open={createOpen} onOpenChange={setCreateOpen} onCreated={() => mutate()} />
      <DeleteDialog
        open={!!deleteTarget}
        onOpenChange={(open) => { if (!open) setDeleteTarget(null); }}
        title="Delete user"
        description={`Are you sure you want to delete '${deleteTarget?.name}'? This action cannot be undone.`}
        onConfirm={confirmDelete}
        isDeleting={isDeleting}
      />
    </div>
  );
}
