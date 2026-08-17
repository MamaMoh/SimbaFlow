"use client";

import { useMemo, useState } from "react";
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
import { MoreHorizontal, Pencil, Trash2 } from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { DeleteDialog } from "@/components/ui/delete-dialog";
import { CreateOfficeSheet } from "@/components/offices/create-office-sheet";
import { EditOfficeSheet } from "@/components/offices/edit-office-sheet";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import type { DepartmentListItem } from "@/lib/schemas/department";
import { toast } from "sonner";
import { PageHeader } from "@/components/ui/page-header";

const fetcher = (url: string) => fetch(url).then((r) => r.json());

export default function OfficesPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canRead =
    hasPermission("office.read") || hasPermission("system.admin");
  const canWrite =
    hasPermission("office.write") || hasPermission("system.admin");

  const [sorting, setSorting] = useState<SortingState>([]);
  const [globalFilter, setGlobalFilter] = useState("");
  const [createOpen, setCreateOpen] = useState(false);
  const [editOffice, setEditOffice] = useState<DepartmentListItem | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const { data, error, isLoading, mutate } = useSWR(
    !permsLoading && canRead ? "/api/proxy/departments" : null,
    fetcher,
    { revalidateOnFocus: false }
  );

  const offices: DepartmentListItem[] = data?.data || [];
  const loadFailed = !!error || (data && data.isSuccess === false);

  const confirmDelete = async () => {
    if (!deleteTarget) return;
    setIsDeleting(true);
    try {
      const res = await fetch(`/api/proxy/departments/${deleteTarget.id}`, {
        method: "DELETE",
      });
      const body = await res.json().catch(() => ({}));
      if (res.ok) {
        toast.success("Office deleted successfully");
        mutate();
      } else {
        toast.error(body?.error || "Failed to delete office");
      }
    } catch {
      toast.error("Failed to delete office. Please try again.");
    }
    setIsDeleting(false);
    setDeleteTarget(null);
  };

  const columns: ColumnDef<DepartmentListItem>[] = useMemo(
    () => [
      {
        id: "index",
        header: "#",
        cell: ({ row }) => (
          <span className="text-muted-foreground">{row.index + 1}</span>
        ),
        size: 40,
        enableSorting: false,
      },
      {
        accessorKey: "name",
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Office" />
        ),
      },
      {
        accessorKey: "code",
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Code" />
        ),
      },
      {
        accessorKey: "parentDepartmentName",
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Parent" />
        ),
        cell: ({ getValue }) => (getValue() as string | null) || "—",
      },
      {
        accessorKey: "userCount",
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Users" />
        ),
      },
      {
        accessorKey: "isActive",
        header: "Status",
        cell: ({ getValue }) =>
          getValue() ? (
            <Badge className="bg-green-100 text-green-800 hover:bg-green-100">
              Active
            </Badge>
          ) : (
            <Badge variant="secondary">Inactive</Badge>
          ),
      },
      {
        id: "actions",
        header: "Actions",
        cell: ({ row }) =>
          canWrite ? (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" className="h-8 w-8">
                  <MoreHorizontal className="h-4 w-4" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem onClick={() => setEditOffice(row.original)}>
                  <Pencil className="h-4 w-4 mr-2" /> Edit
                </DropdownMenuItem>
                <DropdownMenuItem
                  className="text-destructive"
                  onClick={() =>
                    setDeleteTarget({
                      id: row.original.id,
                      name: row.original.name,
                    })
                  }
                >
                  <Trash2 className="h-4 w-4 mr-2" /> Delete
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          ) : null,
        size: 60,
        enableSorting: false,
      },
    ],
    [canWrite]
  );

  const table = useReactTable({
    data: offices,
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

  if (permsLoading) {
    return null;
  }

  if (!canRead) {
    return <AccessDenied resource="offices" />;
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Offices"
        description="Manage agency branches and office locations"
      />

      {loadFailed && (
        <LoadError
          message={data?.error || error?.message}
          onRetry={() => mutate()}
        />
      )}


      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
          table={table}
          emptyMessage="No offices yet — create your first office to assign staff and candidates."
          enableGlobalFilter
          searchPlaceholder="Search offices…"
          paginated
          toolbarEndActions={
            canWrite ? (
              <Button
                size="sm"
                className="h-8 bg-green-800 hover:bg-green-900 text-white"
                onClick={() => setCreateOpen(true)}
              >
                + Create
              </Button>
            ) : undefined
          }
        />
      </div>

      <CreateOfficeSheet
        open={createOpen}
        onOpenChange={setCreateOpen}
        onCreated={() => mutate()}
      />
      <EditOfficeSheet
        office={editOffice}
        open={!!editOffice}
        onOpenChange={(o) => {
          if (!o) setEditOffice(null);
        }}
        onUpdated={() => mutate()}
      />
      <DeleteDialog
        open={!!deleteTarget}
        onOpenChange={(o) => {
          if (!o) setDeleteTarget(null);
        }}
        title="Delete office"
        description={`Are you sure you want to delete '${deleteTarget?.name}'?`}
        onConfirm={confirmDelete}
        isDeleting={isDeleting}
      />
    </div>
  );
}
