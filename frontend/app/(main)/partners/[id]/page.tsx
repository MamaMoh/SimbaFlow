"use client";

import { useMemo } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  useReactTable,
  getCoreRowModel,
  getPaginationRowModel,
  getSortedRowModel,
  type ColumnDef,
} from "@tanstack/react-table";
import { DataTable } from "@/components/data-table/data-table";
import { DataTableColumnHeader } from "@/components/data-table/data-table-column-header";
import { indexColumn } from "@/components/data-table/index-column";
import { NameCell } from "@/components/data-table/name-cell";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/components/ui/page-header";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { StatusBadge } from "@/components/ui/status-badge";
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

  const candidates = usePartnerCandidates(id, !permsLoading && canRead);
  const billing = usePartnerBilling(id, !permsLoading && canSeeBilling);

  const candidateColumns = useMemo<ColumnDef<PartnerCandidateRow>[]>(
    () => [
      indexColumn<PartnerCandidateRow>(),
      {
        accessorKey: "fullName",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Candidate" />,
        cell: ({ row }) => (
          <NameCell href={`/candidates/${row.original.id}`} name={row.original.fullName} />
        ),
      },
      {
        accessorKey: "passportNumber",
        header: ({ column }) => <DataTableColumnHeader column={column} title="Passport" />,
      },
      {
        accessorKey: "stage",
        header: "Stage",
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "countryOfTravel",
        header: "Destination",
        cell: ({ getValue }) => (getValue() as string) || "—",
      },
      {
        accessorKey: "registeredAt",
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
        header: ({ column }) => <DataTableColumnHeader column={column} title="Candidate" />,
        cell: ({ row }) => (
          <NameCell
            href={`/workflow/commissions/${row.original.id}`}
            name={row.original.candidateName}
          />
        ),
      },
      {
        accessorKey: "passportNumber",
        header: "Passport",
      },
      {
        accessorKey: "status",
        header: "Status",
        cell: ({ getValue }) => {
          const s = String(getValue() || "");
          return <StatusBadge tone={commissionTone(s)} value={s} />;
        },
      },
      {
        accessorKey: "totalFeesAmount",
        header: () => <div className="text-right">Fees (ETB)</div>,
        cell: ({ getValue }) => (
          <div className="text-right tabular-nums">{etb(Number(getValue() || 0))}</div>
        ),
      },
      {
        accessorKey: "totalPaidAmount",
        header: () => <div className="text-right">Paid</div>,
        cell: ({ getValue }) => (
          <div className="text-right tabular-nums">{etb(Number(getValue() || 0))}</div>
        ),
      },
      {
        accessorKey: "balanceAmount",
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

  const candidateTable = useReactTable({
    data: candidates.data?.items ?? [],
    columns: candidateColumns,
    getCoreRowModel: getCoreRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    getSortedRowModel: getSortedRowModel(),
    initialState: { pagination: { pageSize: 20 } },
  });

  const billingTable = useReactTable({
    data: billing.data?.items ?? [],
    columns: billingColumns,
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
              {candidates.data?.totalCandidates ?? 0} candidate(s) placed
            </>
          }
        />
      </div>

      {canSeeBilling && billing.data ? (
        <div className="grid gap-4 sm:grid-cols-3">
          <div className="rounded-xl border bg-card p-4 shadow-sm">
            <p className="text-xs text-muted-foreground">Total fees (ETB)</p>
            <p className="mt-1 text-2xl font-bold tabular-nums">
              {etb(billing.data.totalFees)}
            </p>
          </div>
          <div className="rounded-xl border bg-card p-4 shadow-sm">
            <p className="text-xs text-muted-foreground">Collected</p>
            <p className="mt-1 text-2xl font-bold tabular-nums text-emerald-700 dark:text-emerald-400">
              {etb(billing.data.totalPaid)}
            </p>
          </div>
          <div className="rounded-xl border bg-card p-4 shadow-sm">
            <p className="text-xs text-muted-foreground">Outstanding</p>
            <p className="mt-1 text-2xl font-bold tabular-nums text-amber-700 dark:text-amber-400">
              {etb(billing.data.outstanding)}
            </p>
          </div>
        </div>
      ) : null}

      <Tabs defaultValue="candidates">
        <TabsList>
          <TabsTrigger value="candidates" className="px-4">
            Candidates
          </TabsTrigger>
          {canSeeBilling && (
            <TabsTrigger value="billing" className="px-4">
              Billing
            </TabsTrigger>
          )}
        </TabsList>

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
                  paginated
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
                    paginated
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
