"use client";

import { useMemo } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  useReactTable,
  getCoreRowModel,
  getFilteredRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  type ColumnDef,
} from "@tanstack/react-table";
import { DataTable } from "@/components/data-table/data-table";
import { useTableLayout } from "@/lib/data-table/use-table-layout";
import { useBoardQuery } from "@/lib/data-table/use-board-query";
import { applyColumnFilters } from "@/lib/data-table/apply-filters";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { indexColumn } from "@/components/data-table/index-column";
import { identityColumns } from "@/components/data-table/identity-columns";
import { NameCell } from "@/components/data-table/name-cell";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import useSWR from "swr";
import { AgreementDocuments } from "@/components/partners/agreement-documents";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/components/ui/page-header";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { StatusBadge } from "@/components/ui/status-badge";
import { StatTile } from "@/components/ui/stat-tile";
import { commissionTone } from "@/lib/ui/status";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import {
  usePartnerCandidates,
  usePartnerBilling,
  type PartnerCandidateRow,
  type PartnerBilling,
} from "@/lib/api/partners";
import { ArrowLeft, Loader2 } from "lucide-react";

const etb = (n: number) =>
  n.toLocaleString("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export default function PartnerDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canRead = hasPermission("partner.read") || hasPermission("system.admin");
  const canSeeBilling =
    hasPermission("commission.read") || hasPermission("system.admin");

  const canEditDocs = hasPermission("partner.update") || hasPermission("partner.create");

  // Documents hang off the agreement, not the partner, so resolve this agency's link id.
  const { data: linkedResp } = useSWR(
    !permsLoading && canRead ? "/api/proxy/partners?linkedOnly=true" : null,
    (url: string) => fetch(url).then((r) => r.json()),
    { revalidateOnFocus: false }
  );
  const linkId: string | undefined = (linkedResp?.data || []).find(
    (p: { id: string; linkId?: string }) => p.id === id
  )?.linkId;

  const candidates = usePartnerCandidates(id, !permsLoading && canRead);
  const billing = usePartnerBilling(id, !permsLoading && canSeeBilling);

  // Two tables on this page, so two of everything: each tab keeps its own search box and its
  // own funnels, because they are lists of different things. Both arrive whole — there is no
  // page two — so the filters are answered here rather than asked of the API.
  const people = useBoardQuery();
  const money = useBoardQuery();

  const candidateRows = useMemo(
    () =>
      applyColumnFilters(candidates.data?.items ?? [], people.filters, {
        fullName: "text",
        passportNumber: "text",
        labourId: "text",
        stage: "text",
        countryOfTravel: "text",
        registeredAt: "date",
      }),
    [candidates.data, people.filters],
  );

  const billingRows = useMemo(
    () =>
      applyColumnFilters(billing.data?.items ?? [], money.filters, {
        candidateName: "text",
        passportNumber: "text",
        labourId: "text",
        status: "text",
        totalFeesAmount: "number",
        totalPaidAmount: "number",
        balanceAmount: "number",
      }),
    [billing.data, money.filters],
  );

  const candidateColumns = useMemo<ColumnDef<PartnerCandidateRow>[]>(
    () => [
      indexColumn<PartnerCandidateRow>(),
      {
        accessorKey: "fullName",
        meta: { filter: { field: "fullName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Candidate" />,
        cell: ({ row }) => (
          <NameCell href={`/candidates/${row.original.id}`} name={row.original.fullName} />
        ),
      },
      ...identityColumns<PartnerCandidateRow>(),
      {
        accessorKey: "stage",
        meta: { filter: { field: "stage", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Stage" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "countryOfTravel",
        meta: { filter: { field: "countryOfTravel", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Destination" />,
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "registeredAt",
        meta: { filter: { field: "registeredAt", kind: "date" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Registered" />,
        cell: ({ getValue }) => {
          const v = getValue() as string;
          return v ? new Date(v).toLocaleDateString() : "—";
        },
      },
    ],
    [],
  );

  const billingColumns = useMemo<ColumnDef<PartnerBilling["items"][number]>[]>(
    () => [
      indexColumn<PartnerBilling["items"][number]>(),
      {
        accessorKey: "candidateName",
        meta: { filter: { field: "candidateName", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Candidate" />,
        cell: ({ row }) => (
          <NameCell
            href={`/workflow/commissions/${row.original.id}`}
            name={row.original.candidateName}
          />
        ),
      },
      ...identityColumns<PartnerBilling["items"][number]>(),
      {
        accessorKey: "status",
        meta: { filter: { field: "status", kind: "text" } },
        header: ({ column }) => <DataTableColumnHeader column={column} title="Status" />,
        cell: ({ getValue }) => {
          const s = String(getValue() || "");
          return <StatusBadge tone={commissionTone(s)} value={s} />;
        },
      },
      {
        accessorKey: "totalFeesAmount",
        meta: { filter: { field: "totalFeesAmount", kind: "number" } },
        header: () => <div className="text-right">Fees (ETB)</div>,
        cell: ({ getValue }) => (
          <div className="text-right tabular-nums">{etb(Number(getValue() || 0))}</div>
        ),
      },
      {
        accessorKey: "totalPaidAmount",
        meta: { filter: { field: "totalPaidAmount", kind: "number" } },
        header: () => <div className="text-right">Paid</div>,
        cell: ({ getValue }) => (
          <div className="text-right tabular-nums">{etb(Number(getValue() || 0))}</div>
        ),
      },
      {
        accessorKey: "balanceAmount",
        meta: { filter: { field: "balanceAmount", kind: "number" } },
        header: () => <div className="text-right">Outstanding</div>,
        cell: ({ getValue }) => (
          <div className="text-right font-medium tabular-nums">
            {etb(Number(getValue() || 0))}
          </div>
        ),
      },
    ],
    [],
  );

  // Two tables on one page, so two arrangements: the candidates sent to this partner and the
  // money owed on them are different questions and are not read with the same columns.
  const candidateColumnIds = useMemo(
    () => candidateColumns.map((c: any) => c.id ?? c.accessorKey),
    [candidateColumns],
  );
  const candidateLayout = useTableLayout({
    table: "partner-candidates",
    columnIds: candidateColumnIds,
  });

  const billingColumnIds = useMemo(
    () => billingColumns.map((c: any) => c.id ?? c.accessorKey),
    [billingColumns],
  );
  const billingLayout = useTableLayout({
    table: "partner-billing",
    columnIds: billingColumnIds,
  });

  const candidateTable = useReactTable({
    data: candidateRows,
    columns: candidateColumns,
    state: {
      globalFilter: people.globalFilter,
      columnOrder: candidateLayout.columnOrder,
      columnVisibility: candidateLayout.columnVisibility,
    },
    onGlobalFilterChange: people.setGlobalFilter,
    onColumnOrderChange: candidateLayout.setColumnOrder,
    onColumnVisibilityChange: candidateLayout.setColumnVisibility,
    getCoreRowModel: getCoreRowModel(),
    getFilteredRowModel: getFilteredRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    initialState: { pagination: { pageSize: 20 } },
  });

  const billingTable = useReactTable({
    data: billingRows,
    columns: billingColumns,
    state: {
      globalFilter: money.globalFilter,
      columnOrder: billingLayout.columnOrder,
      columnVisibility: billingLayout.columnVisibility,
    },
    onGlobalFilterChange: money.setGlobalFilter,
    onColumnOrderChange: billingLayout.setColumnOrder,
    onColumnVisibilityChange: billingLayout.setColumnVisibility,
    getCoreRowModel: getCoreRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    initialState: { pagination: { pageSize: 20 } },
  });

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }

  if (!canRead) return <AccessDenied resource="partners" />;

  const name = candidates.data?.partnerName ?? billing.data?.partnerName ?? "Partner";
  const country = candidates.data?.country ?? billing.data?.country;

  return (
    <div className="flex flex-col gap-6">
      <div>
        <Button
          asChild
          variant="ghost"
          size="sm"
          className="mb-3 -ml-2 h-8 px-2 text-muted-foreground hover:text-foreground"
        >
          <Link href="/partners">
            <ArrowLeft className="mr-1 h-4 w-4" />
            Partners
          </Link>
        </Button>

        <PageHeader
          title={name}
          description={
            <>
              {country ? `${country} · ` : ""}
              {candidates.data?.totalCandidates ?? 0} placed
            </>
          }
        />
      </div>

      {canSeeBilling && billing.data ? (
        <div className="grid gap-4 sm:grid-cols-3">
          <StatTile label="Total fees (ETB)" value={etb(billing.data.totalFees)} />
          <StatTile label="Collected" value={etb(billing.data.totalPaid)} tone="good" />
          <StatTile label="Outstanding" value={etb(billing.data.outstanding)} tone="warn" />
        </div>
      ) : null}

      <Tabs defaultValue="candidates">
        <TabsList>
          <TabsTrigger value="candidates" className="px-4">
            Candidates
          </TabsTrigger>
          <TabsTrigger value="documents" className="px-4">
            Contract documents
          </TabsTrigger>
          {canSeeBilling && (
            <TabsTrigger value="billing" className="px-4">
              Billing
            </TabsTrigger>
          )}
        </TabsList>

        <TabsContent value="documents" className="mt-4">
          <AgreementDocuments linkId={linkId} canEdit={canEditDocs} />
        </TabsContent>

        <TabsContent value="candidates" className="mt-4">
          {candidates.error ? (
            <LoadError
              message={candidates.error.message}
              onRetry={() => candidates.mutate()}
            />
          ) : (
            <div className="rounded-lg border bg-card p-4 shadow-sm">
              {candidates.isLoading ? (
                <div className="flex items-center justify-center py-12 text-muted-foreground">
                  <Loader2 className="mr-2 h-5 w-5 animate-spin" />
                  Loading…
                </div>
              ) : (
                <DataTable
                  table={candidateTable}
                  layout={candidateLayout}
                  paginated
                  enableGlobalFilter
                  searchPlaceholder="Search name, passport or labour ID…"
                  exportFileName="partner-candidates"
                  filters={people.filters}
                  onFiltersChange={people.setFilters}
                  emptyMessage="No candidates placed through this partner yet."
                />
              )}
            </div>
          )}
        </TabsContent>

        {canSeeBilling && (
          <TabsContent value="billing" className="mt-4">
            {billing.error ? (
              <LoadError message={billing.error.message} onRetry={() => billing.mutate()} />
            ) : (
              <div className="rounded-lg border bg-card p-4 shadow-sm">
                {billing.isLoading ? (
                  <div className="flex items-center justify-center py-12 text-muted-foreground">
                    <Loader2 className="mr-2 h-5 w-5 animate-spin" />
                    Loading…
                  </div>
                ) : (
                  <DataTable
                    table={billingTable}
                    layout={billingLayout}
                    paginated
                    enableGlobalFilter
                    searchPlaceholder="Search name, passport or labour ID…"
                    exportFileName="partner-billing"
                    filters={money.filters}
                    onFiltersChange={money.setFilters}
                    emptyMessage="No commissions for this partner yet — they appear once an arrived candidate is added to Commission."
                  />
                )}
              </div>
            )}
          </TabsContent>
        )}
      </Tabs>
    </div>
  );
}
