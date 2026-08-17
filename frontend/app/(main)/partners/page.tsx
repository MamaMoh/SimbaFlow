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
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
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
  status: string;
  capacityTier: string;
  maxEthiopianAgencies: number;
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
  const { hasPermission, isSuperAdmin } = usePermissions();
  const canRead =
    hasPermission("partner.read") || hasPermission("system.admin");
  const canLink =
    hasPermission("partner.create") ||
    hasPermission("partner.update") ||
    hasPermission("system.admin");
  /** Catalog create lives on /admin/partners — SuperAdmin only here as shortcut */
  const canCreateCatalog = isSuperAdmin;

  const [sorting, setSorting] = useState<SortingState>([]);
  const [globalFilter, setGlobalFilter] = useState("");
  const [createOpen, setCreateOpen] = useState(false);
  const [linkOpen, setLinkOpen] = useState(false);
  const [tab, setTab] = useState<"catalog" | "linked">("linked");
  const [unlinkingId, setUnlinkingId] = useState<string | null>(null);

  const catalogUrl =
    tab === "linked" ? "/api/proxy/partners?linkedOnly=true" : "/api/proxy/partners";
  const { data, error, mutate } = useSWR(canRead ? catalogUrl : null, fetcher, {
    revalidateOnFocus: false,
  });
  const partners: PartnerRow[] = data?.data || [];

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
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Country" />
        ),
      },
      {
        accessorKey: "capacityTier",
        header: ({ column }) => (
          <DataTableColumnHeader column={column} title="Art. 40 tier" />
        ),
        cell: ({ row }) => (
          <span className="text-sm">
            {row.original.capacityTier}
            <span className="text-muted-foreground">
              {" "}
              (≤{row.original.maxEthiopianAgencies} ET)
            </span>
          </span>
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
        accessorKey: "status",
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

  if (!canRead) {
    return <AccessDenied resource="partners" />;
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Partners"
        description="Link overseas partners to your agency (ትስስር). Platform catalog management is under
Partner catalog."
        actions={
          <div className="flex flex-wrap gap-2">
            {canCreateCatalog ? (
              <Button
                size="sm"
                className="h-8 gap-1.5 bg-green-800 hover:bg-green-900 text-white"
                onClick={() => setCreateOpen(true)}
              >
                <Plus className="h-3.5 w-3.5" />
                Add to catalog
              </Button>
            ) : null}
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
          </div>
        }
      />

      {tab === "linked" ? <CapacityStrip enabled={canRead} /> : null}

      <div className="flex gap-2">
        <Button
          size="sm"
          variant={tab === "linked" ? "default" : "outline"}
          onClick={() => setTab("linked")}
        >
          Linked to my agency
        </Button>
        <Button
          size="sm"
          variant={tab === "catalog" ? "default" : "outline"}
          onClick={() => setTab("catalog")}
        >
          Browse catalog
        </Button>
      </div>

      {error ? (
        <LoadError message="Could not load partners" onRetry={() => mutate()} />
      ) : null}


      <div className="rounded-lg border bg-card p-4 shadow-sm">
        <DataTable
          table={table}
          emptyMessage="No partners linked yet — link a foreign agency from the catalog so staff can select it when registering candidates."
          enableGlobalFilter
          searchPlaceholder="Search partners…"
          paginated
        />
      </div>

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
