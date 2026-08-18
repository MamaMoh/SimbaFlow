"use client";

import { use, useState } from "react";
import Link from "next/link";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { Button } from "@/components/ui/button";
import { CommissionStatusBadge } from "@/components/finance/commission-status-badge";
import { FeeBreakdownEditor } from "@/components/finance/fee-breakdown-editor";
import { RecordPaymentSheet } from "@/components/finance/record-payment-sheet";
import { DisputePanel } from "@/components/finance/dispute-panel";
import { formatEtb, useCommission } from "@/lib/api/commissions";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { ArrowLeft, Loader2 } from "lucide-react";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

export default function CommissionDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("commission.read") || hasPermission("system.admin");
  const canUpdate = hasPermission("commission.update") || hasPermission("system.admin");
  const canPost = hasPermission("accounting.post") || hasPermission("system.admin");

  const { commission, isLoading, error, mutate } = useCommission(canView ? id : undefined);
  const [paymentOpen, setPaymentOpen] = useState(false);

  if (permsLoading || isLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin mr-2" />
        Loading…
      </div>
    );
  }

  if (!canView) return <AccessDenied resource="Commissions" />;

  if (error) {
    return (
      <div className="p-6">
        <LoadError
          message={error instanceof Error ? error.message : "Failed to load"}
          onRetry={() => mutate()}
        />
      </div>
    );
  }

  if (!commission) {
    return (
      <div className="p-6">
        <PageAlert variant="info" title="Not found" description="Commission record not found." />
      </div>
    );
  }

  const feesReadOnly = !canUpdate || commission.status === "Settled";
  const paymentBlock =
    commission.fees.length === 0
      ? "Add fees before recording a payment"
      : commission.status === "Settled" && commission.balanceAmount <= 0
        ? "Commission is already settled"
        : null;

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6 p-6">
      <div className="space-y-3">
        <Link
          href="/workflow/commissions"
          className="inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to commissions
        </Link>

        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="space-y-1">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-bold tracking-tight">{commission.candidateName}</h1>
              <CommissionStatusBadge status={commission.status} />
            </div>
            <p className="text-sm text-muted-foreground">
              Passport {commission.passportNumber}
              {commission.partnerName ? ` · ${commission.partnerName}` : ""}
              {commission.countryOfTravel ? ` · ${commission.countryOfTravel}` : ""}
            </p>
            <p className="text-sm">
              <Link
                href={`/candidates/${commission.candidateId}`}
                className="text-primary underline-offset-4 hover:underline"
              >
                Open candidate profile
              </Link>
            </p>
          </div>
          <div className="grid grid-cols-3 gap-4 rounded-md border bg-card px-4 py-3 text-sm">
            <div>
              <p className="text-muted-foreground">Fees</p>
              <p className="font-medium tabular-nums">{formatEtb(commission.totalFeesAmount)}</p>
            </div>
            <div>
              <p className="text-muted-foreground">Paid</p>
              <p className="font-medium tabular-nums">{formatEtb(commission.totalPaidAmount)}</p>
            </div>
            <div>
              <p className="text-muted-foreground">Balance</p>
              <p className="font-medium tabular-nums">{formatEtb(commission.balanceAmount)}</p>
            </div>
          </div>
        </div>

        {commission.status === "Disputed" ? (
          <PageAlert
            variant="error"
            title="Disputed"
            description="Resolve the open dispute before treating this commission as settled."
          />
        ) : null}
      </div>

      <section className="space-y-3 rounded-lg border bg-card p-4 shadow-sm">
        <h2 className="text-lg font-medium">Fee breakdown</h2>
        <FeeBreakdownEditor
          commissionId={commission.id}
          fees={commission.fees}
          readOnly={feesReadOnly}
          onSaved={() => mutate()}
        />
      </section>

      <section className="space-y-3 rounded-lg border bg-card p-4 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-lg font-medium">Payments</h2>
          {canPost ? (
            <Button size="sm" className="h-8" onClick={() => setPaymentOpen(true)}>
              Record payment
            </Button>
          ) : null}
        </div>
        {commission.payments.length === 0 ? (
          <p className="text-sm text-muted-foreground">No payments yet.</p>
        ) : (
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Date</TableHead>
                  <TableHead>Amount</TableHead>
                  <TableHead>ETB</TableHead>
                  <TableHead>Method</TableHead>
                  <TableHead>Reference</TableHead>
                  <TableHead>Journal</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {commission.payments.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell>{new Date(p.paidAt).toLocaleDateString()}</TableCell>
                    <TableCell className="tabular-nums">
                      {p.amount.toFixed(2)} {p.currency}
                      {p.currency !== "ETB" ? (
                        <span className="ml-1 text-xs text-muted-foreground">
                          @ {p.exchangeRateToEtb}
                        </span>
                      ) : null}
                    </TableCell>
                    <TableCell className="tabular-nums">{formatEtb(p.amountEtb)}</TableCell>
                    <TableCell>{p.method}</TableCell>
                    <TableCell>{p.reference || "—"}</TableCell>
                    <TableCell>
                      {p.journalEntryId ? (
                        <Link
                          href={`/finance/journals/${p.journalEntryId}`}
                          className="text-primary underline-offset-4 hover:underline"
                        >
                          View
                        </Link>
                      ) : (
                        "—"
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </section>

      <section className="space-y-3 rounded-lg border bg-card p-4 shadow-sm">
        <h2 className="text-lg font-medium">Disputes</h2>
        <DisputePanel
          commissionId={commission.id}
          disputes={commission.disputes}
          canUpdate={canUpdate}
          onChanged={() => mutate()}
        />
      </section>

      <RecordPaymentSheet
        open={paymentOpen}
        onOpenChange={setPaymentOpen}
        commissionId={commission.id}
        disabledReason={paymentBlock}
        onRecorded={() => mutate()}
      />
    </div>
  );
}
