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
import { MoreHorizontal, Building2, Pencil, Users, Power, Trash2 } from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { CreateAgencySheet } from "@/components/tenants/create-agency-sheet";
import { EditAgencySheet } from "@/components/tenants/edit-agency-sheet";
import { DeleteDialog } from "@/components/ui/delete-dialog";
import { toast } from "sonner";
import { USE_MOCKS } from "@/lib/api/candidates-api";
import { mockApi } from "@/lib/api/mock-api";

interface AgencyRow {
  id: string;
  name: string;
  slug: string;
  schemaName: string;
  contactEmail: string;
  status: number;
  provisionedAt: string;
}

const fetcher = (url: string) => fetch(url).then(r => r.json());

const STATUS_MAP: Record<number, { label: string; variant: "default" | "outline" | "destructive" }> = {
  0: { label: "Active", variant: "default" },
  1: { label: "Suspended", variant: "outline" },
  2: { label: "Deactivated", variant: "destructive" },
};

export default function TenantsPage() {
  const [sorting, setSorting] = useState<SortingState>([]);
  const [globalFilter, setGlobalFilter] = useState("");
  const [createOpen, setCreateOpen] = useState(false);
  const [editAgencyId, setEditAgencyId] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const { data, mutate } = useSWR(
    USE_MOCKS ? "mock-tenants" : "/api/proxy/tenants",
    USE_MOCKS ? () => mockApi.getTenants() : fetcher,
    { revalidateOnFocus: false },
  );
  const agencies: AgencyRow[] = data?.data || [];

  const handleStatusChange = async (id: string, status: number) => {
    if (USE_MOCKS) {
      await mockApi.setTenantStatus(id, status);
      mutate();
      toast.success("Agency status updated");
      return;
    }
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
    if (USE_MOCKS) {
      await mockApi.deleteTenant(deleteTarget.id);
      mutate();
      toast.success("Agency deleted");
    } else {
      const res = await fetch(`/api/proxy/tenants/${deleteTarget.id}`, { method: "DELETE" });
      if (res.ok) {
        mutate();
        toast.success("Agency deleted");
      } else {
        toast.error("Failed to delete agency");
      }
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
      header: ({ column }) => <DataTableColumnHeader column={column} title="Agency Name" />,
      cell: ({ row }) => (
        <div className="flex items-center gap-2">
          <Building2 className="h-4 w-4 text-green-700" />
          <span className="font-medium">{row.original.name}</span>
        </div>
      ),
    },
    {
      accessorKey: "slug",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Slug" />,
      cell: ({ getValue }) => <code className="text-xs bg-muted px-1.5 py-0.5 rounded">{getValue() as string}</code>,
    },
    {
      accessorKey: "contactEmail",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Contact Email" />,
    },
    {
      accessorKey: "status",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Status" />,
      cell: ({ getValue }) => {
        const status = getValue() as number;
        const s = STATUS_MAP[status] || { label: "Unknown", variant: "outline" as const };
        return <Badge variant={s.variant} className={status === 0 ? "bg-green-100 text-green-800" : ""}>{s.label}</Badge>;
      },
    },
    {
      accessorKey: "provisionedAt",
      header: ({ column }) => <DataTableColumnHeader column={column} title="Created" />,
      cell: ({ getValue }) => new Date(getValue() as string).toLocaleDateString(),
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
            <DropdownMenuItem onClick={() => setEditAgencyId(row.original.id)}>
              <Pencil className="h-4 w-4 mr-2" /> Edit
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => window.location.href = `/staff?tenant=${row.original.id}`}>
              <Users className="h-4 w-4 mr-2" /> Manage Users
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            {row.original.status === 0 ? (
              <DropdownMenuItem onClick={() => handleStatusChange(row.original.id, 1)}>
                <Power className="h-4 w-4 mr-2" /> Suspend
              </DropdownMenuItem>
            ) : (
              <DropdownMenuItem onClick={() => handleStatusChange(row.original.id, 0)}>
                <Power className="h-4 w-4 mr-2" /> Activate
              </DropdownMenuItem>
            )}
            <DropdownMenuSeparator />
            <DropdownMenuItem className="text-destructive" onClick={() => setDeleteTarget({ id: row.original.id, name: row.original.name })}>
              <Trash2 className="h-4 w-4 mr-2" /> Delete
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      ),
      size: 60,
      enableSorting: false,
    },
  ], []);

  const table = useReactTable({
    data: agencies,
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
    <div className="flex flex-col gap-6 p-6">
      <div>
        <h1 className="text-2xl font-bold">Agencies</h1>
        <p className="text-sm text-muted-foreground">Manage all labour export agencies on the platform</p>
      </div>

      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
          table={table}
          enableGlobalFilter={true}
          searchPlaceholder="Search agencies..."
          paginated={true}
          toolbarEndActions={
            <Button size="sm" className="h-8 bg-green-800 hover:bg-green-900 text-white" onClick={() => setCreateOpen(true)}>
              <span className="mr-1">+</span> Create Agency
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
