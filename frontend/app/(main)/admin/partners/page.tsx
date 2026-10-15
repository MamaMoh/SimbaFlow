"use client";

import { useMemo, useState } from "react";
import { useTableRows } from "@/lib/data/use-table-rows";
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
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { CreatePartnerSheet } from "@/components/partners/create-partner-sheet";
import { usePartners, type PartnerRow } from "@/lib/api/partners";
import { Plus } from "lucide-react";
import { LogoUpload } from "@/components/branding/logo-upload";
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
  // The same search box and the same funnels as every other board. The catalogue arrives whole
  // — there is no page two — so the rows in hand are all the rows there are, and the filters
  // are answered here rather than asked of the API.
  const { globalFilter, setGlobalFilter, filters, setFilters } = useBoardQuery();
  const [createOpen, setCreateOpen] = useState(false);

  const { data: partnerData, error, mutate } = usePartners({
    enabled: canManage,
  });
  const all = useTableRows<PartnerRow>(partnerData);
  const partners = useMemo(
    () =>
      applyColumnFilters(all, filters, {
        name: "text",
        country: "text",
        contactPhone: "text",
        foreignLicenseId: "text",
        contactEmail: "text",
        status: "text",
      }),
    [all, filters],
  );

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
        meta: { filter: { field: "name", kind: "text" } },
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Partner agency" />
        ),
      },
      {
        accessorKey: "country",
        meta: { filter: { field: "country", kind: "text" } },
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Country" />
        ),
      },
      {
        accessorKey: "contactPhone",
        meta: { filter: { field: "contactPhone", kind: "text" } },
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Phone" />
        ),
        cell: ({ row }) => (
          <span className="text-sm">{row.original.contactPhone || "—"}</span>
        ),
      },
      {
        accessorKey: "foreignLicenseId",
        meta: { filter: { field: "foreignLicenseId", kind: "text" } },
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Foreign license" />
        ),
        cell: ({ row }) => row.original.foreignLicenseId || "—",
      },
      {
        accessorKey: "contactEmail",
        meta: { filter: { field: "contactEmail", kind: "text" } },
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Contact" />
        ),
        cell: ({ row }) => row.original.contactEmail || "—",
      },
      {
        id: "letterhead",
        header: "Letterhead",
        enableSorting: false,
        cell: ({ row }) => (
          <LogoUpload
            endpoint={`/api/proxy/branding/partners/${row.original.id}/letterhead`}
            logoPath={row.original.letterheadPath ?? null}
            onChange={() => mutate()}
            label=""
            hint=""
            compact
          />
        ),
      },
      {
        id: "logo",
        header: "Logo",
        enableSorting: false,
        cell: ({ row }) => (
          <LogoUpload
            endpoint={`/api/proxy/branding/partners/${row.original.id}/logo`}
            logoPath={row.original.logoPath ?? null}
            onChange={() => mutate()}
            label=""
            hint=""
            compact
          />
        ),
      },
      {
        accessorKey: "status",
        meta: { filter: { field: "status", kind: "text" } },
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
    [mutate]
  );

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "admin-partners", columnIds });

  const table = useReactTable({
    data: partners,
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

  if (!canManage) {
    return <AccessDenied resource="the partner catalog" />;
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Partner catalog"
        description="Shared by every agency on the platform"
      />

      {error ? (
        <LoadError
          message={error.message || "Could not load catalog"}
          onRetry={() => mutate()}
        />
      ) : null}

      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
          layout={layout}
          table={table}
          enableGlobalFilter
          searchPlaceholder="Search catalog…"
          paginated
          exportFileName="partner-catalog"
          filters={filters}
          onFiltersChange={setFilters}
          createAction={
            <Button
              size="sm"
              className="h-8 gap-1.5 bg-green-800 text-white hover:bg-green-900"
              onClick={() => setCreateOpen(true)}
            >
              <Plus className="h-3.5 w-3.5" />
              Add partner
            </Button>
          }
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
