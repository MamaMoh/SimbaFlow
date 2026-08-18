"use client";

import { useMemo, useState } from "react";
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
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { CreatePartnerSheet } from "@/components/partners/create-partner-sheet";
import { usePartners, type PartnerRow } from "@/lib/api/partners";
import { Plus } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";

/**
 * SuperAdmin partner catalog (platform master data).
 * Tenant agency links live on /partners.
 */
export default function AdminPartnersPage() {
  const { isSuperAdmin } = usePermissions();
  // Catalog create/manage is SuperAdmin / platform only (not AgencyOwner via claim bypass)
  const canManage = isSuperAdmin;

  const [sorting, setSorting] = useState<SortingState>([]);
  const [globalFilter, setGlobalFilter] = useState("");
  const [createOpen, setCreateOpen] = useState(false);

  const { data: partners = [], error, mutate } = usePartners({
    enabled: canManage,
  });

  const columns: ColumnDef<PartnerRow>[] = useMemo(
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
          <DataTableColumnHeader column={column} title="Partner agency" />
        ),
      },
      {
        accessorKey: "country",
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Country" />
        ),
      },
      {
        accessorKey: "contactPhone",
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Phone" />
        ),
        cell: ({ row }) => (
          <span className="text-sm">{row.original.contactPhone || "—"}</span>
        ),
      },
      {
        accessorKey: "foreignLicenseId",
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Foreign license" />
        ),
        cell: ({ row }) => row.original.foreignLicenseId || "—",
      },
      {
        accessorKey: "contactEmail",
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Contact" />
        ),
        cell: ({ row }) => row.original.contactEmail || "—",
      },
      {
        accessorKey: "status",
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Status" />
        ),
        cell: ({ row }) => (
          <Badge
            variant={row.original.status === "Active" ? "default" : "outline"}
          >
            {row.original.status}
          </Badge>
        ),
      },
    ],
    []
  );

  const table = useReactTable({
    data: partners,
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

  if (!canManage) {
    return <AccessDenied resource="the partner catalog" />;
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Partner catalog"
        description={<>Shared catalog of foreign partner agencies. Agencies link from{" "}
<a href="/partners" className="underline underline-offset-2">
Partners
</a>
.</>}
        actions={
          <Button
            size="sm"
            className="h-8 gap-1.5 bg-green-800 hover:bg-green-900 text-white"
            onClick={() => setCreateOpen(true)}
          >
            <Plus className="h-3.5 w-3.5" />
            Add partner
          </Button>
        }
      />

      <PageAlert
        variant="info"
        title="Platform catalog"
        description="Partners added here are available to every agency. Check before adding a duplicate."
      />

      {error ? (
        <LoadError
          message={error.message || "Could not load catalog"}
          onRetry={() => mutate()}
        />
      ) : null}

      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
          table={table}
          enableGlobalFilter
          searchPlaceholder="Search catalog…"
          paginated
        />
      </div>

      <CreatePartnerSheet
        open={createOpen}
        onOpenChange={setCreateOpen}
        onCreated={() => mutate()}
      />
    </div>
  );
}
