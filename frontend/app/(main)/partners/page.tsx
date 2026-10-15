"use client";

import { useRouter } from "next/navigation";
import { useTableRows } from "@/lib/data/use-table-rows";

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
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import { useBoardQuery } from "@/lib/data-table/use-board-query";
import { applyColumnFilters } from "@/lib/data-table/apply-filters";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { useActingTenantId } from "@/lib/tenant/acting-tenant";
import { CreatePartnerSheet } from "@/components/partners/create-partner-sheet";
import { LinkPartnerSheet } from "@/components/partners/link-partner-sheet";
import { Link2, Plus, Unlink } from "lucide-react";
import { toast } from "sonner";
import { PageHeader } from "@/components/ui/page-header";
import { StatusBadge } from "@/components/ui/status-badge";
import { NameCell } from "@/components/data-table/name-cell";
import { CapacityStrip } from "@/components/partners/capacity-strip";
import { agreementTone } from "@/lib/api/partners";

type PartnerRow = {
  id: string;
  name: string;
  country: string;
  countryCode: string;
  contactEmail: string | null;
  contactPhone?: string | null;
  status: string;
  foreignLicenseId: string | null;
  linkId?: string;
  agreementStart?: string;
  agreementEnd?: string;
  agreementState?: string;
  daysRemaining?: number;
  agreementLabel?: string;
  isUsable?: boolean;
};

const fetcher = (url: string) => fetch(url).then((r) => r.json());

export default function PartnersPage() {
  const router = useRouter();
  const { hasPermission, isSuperAdmin } = usePermissions();
  const canRead =
    hasPermission("partner.read") || hasPermission("system.admin");
  const canLink =
    hasPermission("partner.create") ||
    hasPermission("partner.update") ||
    hasPermission("system.admin");
  /** Catalog create lives on /admin/partners — SuperAdmin only here as shortcut */
  // Agencies register their own foreign partners; partner.create is what the API checks.
  // Agencies register the partners they work with; this is not a platform-admin job.
  const canCreateCatalog = hasPermission("partner.create") || isSuperAdmin;

  const [sorting, setSorting] = useState<SortingState>([]);
  // The same search box and the same funnels as every other board. This list arrives whole —
  // there is no page two — so the rows in hand are all the rows there are, and the filters are
  // answered here rather than asked of the API.
  const { globalFilter, setGlobalFilter, filters, setFilters } = useBoardQuery();
  const [createOpen, setCreateOpen] = useState(false);
  const [linkOpen, setLinkOpen] = useState(false);
  const [tab, setTab] = useState<"catalog" | "linked">("linked");
  const actingTenantId = useActingTenantId();
  const noAgencyChosen = isSuperAdmin && actingTenantId === null;
  const [unlinkingId, setUnlinkingId] = useState<string | null>(null);

  const catalogUrl =
    tab === "linked" ? "/api/proxy/partners?linkedOnly=true" : "/api/proxy/partners";
  const { data, error, mutate } = useSWR(canRead ? catalogUrl : null, fetcher, {
    revalidateOnFocus: false,
  });
  const all = useTableRows<PartnerRow>(data?.data);
  const partners = useMemo(
    () =>
      applyColumnFilters(all, filters, {
        name: "text",
        country: "text",
        contactPhone: "text",
        foreignLicenseId: "text",
        status: "text",
        agreementStart: "date",
      }),
    [all, filters],
  );

  const unlinkPartner = async (linkId: string, name: string) => {
    setUnlinkingId(linkId);
    try {
      const res = await fetch(`/api/proxy/partners/links/${linkId}/status`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ status: "Suspended" }),
      });
      const body = await res.json();
      if (!body.isSuccess) {
        toast.error(body.error || "Failed to unlink partner");
        return;
      }
      toast.success(`Unlinked ${name}`);
      mutate();
    } catch {
      toast.error("Failed to unlink partner");
    } finally {
      setUnlinkingId(null);
    }
  };

  const columns: ColumnDef<PartnerRow>[] = useMemo(() => {
    const base: ColumnDef<PartnerRow>[] = [
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
          <DataTableColumnHeader column={column} title="Partner (Arab agency)" />
        ),
        cell: ({ row }) => (
          <NameCell
            href={`/partners/${row.original.id}`}
            name={row.original.name}
            subtitle={row.original.country}
          />
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
        accessorKey: "status",
        meta: { filter: { field: "status", kind: "text" } },
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Status" />
        ),
        cell: ({ row }) => (
          <Badge variant={row.original.status === "Active" ? "default" : "outline"}>
            {row.original.status}
          </Badge>
        ),
      },
    ];

    if (tab === "linked") {
      base.push(
        {
          accessorKey: "agreementStart",
          meta: { filter: { field: "agreementStart", kind: "date" } },
          header: ({ column }) => (
            <DataTableColumnHeader column={column} title="Agreement" />
          ),
          cell: ({ row }) => {
            const { agreementStart: start, agreementEnd: end, agreementState, agreementLabel } =
              row.original;
            if (!start && !end) return "—";
            return (
              <div className="space-y-1">
                <StatusBadge
                  tone={agreementTone(agreementState)}
                  label={agreementLabel || agreementState || "—"}
                />
                <span className="block text-xs text-muted-foreground">
                  {start || "?"} → {end || "?"}
                </span>
              </div>
            );
          },
        },
        {
          id: "actions",
          header: "Actions",
          cell: ({ row }) => {
            if (!canLink || !row.original.linkId) return null;
            return (
              <Button
                size="sm"
                variant="outline"
                className="h-8 gap-1.5"
                disabled={unlinkingId === row.original.linkId}
                onClick={() =>
                  unlinkPartner(row.original.linkId!, row.original.name)
                }
              >
                <Unlink className="h-3.5 w-3.5" />
                Unlink
              </Button>
            );
          },
          enableSorting: false,
        }
      );
    }

    return base;
  }, [tab, canLink, unlinkingId]);

  // Memoised: a fresh array each render would rebuild the order every render.
  const columnIds = useMemo(() => columns.map((c: any) => c.id ?? c.accessorKey), [columns]);

  // How this person arranged this board, remembered on this computer.
  const layout = useTableLayout({ table: "partners", columnIds });

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

  if (!canRead) {
    return <AccessDenied resource="partners" />;
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Partners" />

      {tab === "linked" && !noAgencyChosen ? <CapacityStrip enabled={canRead} /> : null}

      {error ? (
        <LoadError message="Could not load partners" onRetry={() => mutate()} />
      ) : null}

      {noAgencyChosen && tab === "linked" ? (
        <div className="rounded-lg border border-dashed p-10 text-center text-sm text-muted-foreground">
          Choose an agency in the switcher above to see the partners it works with.
        </div>
      ) : (
      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
          layout={layout}
            onRowClick={(row: { id: string }) => router.push(`/partners/${row.id}`)}
        exportFileName="partners"
          table={table}
          emptyMessage={
            tab === "linked"
              ? "No partners linked yet — register a foreign agency, then link it so staff can select it when registering candidates."
              : "No partners registered yet. Register the foreign agencies your agency works with."
          }
          enableGlobalFilter
          searchPlaceholder="Search partners…"
          paginated
          filters={filters}
          onFiltersChange={setFilters}
          toolbarEndActions={
            /* Which half of the catalogue is on screen. It was a pair of buttons under the
               header; it is this board's own switch, so it sits where every other board's
               does. */
            <div className="flex rounded-md border p-0.5" role="group" aria-label="Which partners">
              {(["linked", "catalog"] as const).map((which) => (
                <button
                  key={which}
                  type="button"
                  onClick={() => setTab(which)}
                  className={
                    "rounded px-2.5 py-1 text-xs font-medium transition " +
                    (tab === which
                      ? "bg-green-800 text-white"
                      : "text-muted-foreground hover:text-foreground")
                  }
                >
                  {which === "linked" ? "Linked to my agency" : "All my partners"}
                </button>
              ))}
            </div>
          }
          createAction={
            <div className="flex items-center gap-2">
              {canLink ? (
                <Button
                  size="sm"
                  variant="outline"
                  className="h-8 gap-1.5"
                  onClick={() => setLinkOpen(true)}
                >
                  <Link2 className="h-3.5 w-3.5" />
                  Link to my agency
                </Button>
              ) : null}
              {canCreateCatalog ? (
                <Button
                  size="sm"
                  className="h-8 gap-1.5 bg-green-800 text-white hover:bg-green-900"
                  onClick={() => setCreateOpen(true)}
                >
                  <Plus className="h-3.5 w-3.5" />
                  Register partner
                </Button>
              ) : null}
            </div>
          }
        />
      </div>
      )}

      <CreatePartnerSheet
        open={createOpen}
        onOpenChange={setCreateOpen}
        onCreated={() => mutate()}
      />
      <LinkPartnerSheet
        open={linkOpen}
        onOpenChange={setLinkOpen}
        onLinked={() => {
          setTab("linked");
          mutate();
        }}
      />
    </div>
  );
}
