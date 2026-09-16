"use client";

import { use, useMemo, useState } from "react";
import Link from "next/link";
import { toast } from "sonner";
import {
  ArrowLeft,
  Ban,
  CheckCircle2,
  Loader2,
  Receipt,
} from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import {
  subscriptionApi,
  useInvoices,
  useSubscriptions,
  type InvoiceRow,
} from "@/lib/api/subscriptions";
import { money, onDate, statusTone } from "@/lib/billing/format";

export default function AgencyBillingPage({
  params,
}: {
  params: Promise<{ tenantId: string }>;
}) {
  const { tenantId } = use(params);
  const { isSuperAdmin } = usePermissions();
  const { rows, isLoading: rowsLoading, mutate: mutateRows } = useSubscriptions(isSuperAdmin);
  const { invoices, error, mutate } = useInvoices(isSuperAdmin ? tenantId : null);

  const [paying, setPaying] = useState<InvoiceRow | null>(null);
  const [voiding, setVoiding] = useState<InvoiceRow | null>(null);

  const agency = rows.find((r) => r.id === tenantId) ?? null;

  // Totals are worked out from the invoices themselves rather than carried on the row, so the
  // figures on this page always agree with the table under them.
  const summary = useMemo(() => {
    const currency = agency?.subscriptionCurrency || invoices[0]?.currency || "ETB";
    const unpaid = invoices.filter((i) => i.status === "Issued");
    const paid = invoices.filter((i) => i.status === "Paid");
    return {
      currency,
      outstanding: unpaid.reduce((sum, i) => sum + i.amount, 0),
      outstandingCount: unpaid.length,
      collected: paid.reduce((sum, i) => sum + i.amount, 0),
      collectedCount: paid.length,
    };
  }, [invoices, agency]);

  if (!isSuperAdmin) return <AccessDenied resource="subscriptions" />;

  const refresh = () => {
    void mutate();
    void mutateRows();
  };

  return (
    <div className="flex flex-col gap-6">
      <div>
        <Link
          href="/subscriptions"
          className="mb-2 inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-3.5 w-3.5" />
          All subscriptions
        </Link>
        <PageHeader
          title={agency?.name ?? "Billing history"}
          description="Every invoice raised for this agency, and what happened to it."
          actions={
            agency ? (
              <Badge
                variant="outline"
                className={
                  agency.hasAccess
                    ? "border-green-300 bg-green-50 text-green-800"
                    : "border-red-300 bg-red-50 text-red-800"
                }
              >
                {agency.hasAccess ? "Active" : agency.status}
              </Badge>
            ) : null
          }
        />
      </div>

      {error ? <LoadError message="Could not load invoices" onRetry={() => mutate()} /> : null}

      <div className="grid gap-4 sm:grid-cols-3">
        <SummaryCard
          label="Outstanding"
          value={money(summary.outstanding, summary.currency)}
          detail={
            summary.outstandingCount === 0
              ? "Nothing unpaid"
              : `${summary.outstandingCount} ${summary.outstandingCount === 1 ? "invoice" : "invoices"} awaiting payment`
          }
          tone={summary.outstanding > 0 ? "warn" : "plain"}
        />
        <SummaryCard
          label="Collected"
          value={money(summary.collected, summary.currency)}
          detail={`${summary.collectedCount} ${summary.collectedCount === 1 ? "invoice" : "invoices"} settled`}
        />
        <SummaryCard
          label="Plan"
          value={
            agency && agency.subscriptionAmount > 0
              ? money(agency.subscriptionAmount, agency.subscriptionCurrency)
              : "Not charged"
          }
          detail={
            agency && agency.subscriptionAmount > 0
              ? `Billed ${agency.cycle.toLowerCase()} · next ${onDate(agency.nextPaymentDue)}`
              : "No subscription set"
          }
        />
      </div>

      <div className="overflow-hidden rounded-lg border bg-card shadow-sm">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[900px] text-sm">
            <thead>
              <tr className="border-b bg-muted/40 text-left text-xs uppercase tracking-wide text-muted-foreground">
                <th className="px-4 py-3 font-medium">Invoice</th>
                <th className="px-4 py-3 font-medium">Period covered</th>
                <th className="px-4 py-3 font-medium">Issued</th>
                <th className="px-4 py-3 font-medium">Due</th>
                <th className="px-4 py-3 text-right font-medium">Amount</th>
                <th className="px-4 py-3 font-medium">Status</th>
                <th className="px-4 py-3 text-right font-medium">Actions</th>
              </tr>
            </thead>
            <tbody>
              {rowsLoading && invoices.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-4 py-12 text-center text-muted-foreground">
                    <Loader2 className="mx-auto h-4 w-4 animate-spin" />
                  </td>
                </tr>
              ) : invoices.length === 0 ? (
                <tr>
                  <td colSpan={7} className="px-4 py-12 text-center">
                    <Receipt className="mx-auto mb-2 h-6 w-6 text-muted-foreground/60" />
                    <p className="text-sm font-medium">No invoices yet</p>
                    <p className="mt-0.5 text-sm text-muted-foreground">
                      Raise one from the subscriptions list.
                    </p>
                  </td>
                </tr>
              ) : (
                invoices.map((inv) => {
                  const tone = statusTone(inv.status);
                  return (
                    <tr key={inv.id} className="border-b last:border-0 hover:bg-muted/30">
                      <td className="px-4 py-3 font-medium whitespace-nowrap tabular-nums">
                        {inv.number}
                      </td>
                      <td className="px-4 py-3 whitespace-nowrap text-muted-foreground">
                        {onDate(inv.periodStart)} – {onDate(inv.periodEnd)}
                        <span className="ml-2 text-xs">({inv.cycle.toLowerCase()})</span>
                      </td>
                      <td className="px-4 py-3 whitespace-nowrap text-muted-foreground">
                        {onDate(inv.issuedOn)}
                      </td>
                      <td className="px-4 py-3 whitespace-nowrap text-muted-foreground">
                        {onDate(inv.dueOn)}
                      </td>
                      <td className="px-4 py-3 text-right font-medium whitespace-nowrap tabular-nums">
                        {money(inv.amount, inv.currency)}
                      </td>
                      <td className="px-4 py-3 whitespace-nowrap">
                        <Badge variant="outline" className={tone.className}>
                          {inv.status}
                        </Badge>
                        {inv.status === "Paid" && inv.paidOn ? (
                          <span className="mt-0.5 block text-xs text-muted-foreground">
                            {onDate(inv.paidOn)}
                            {inv.paymentReference ? ` · ${inv.paymentReference}` : ""}
                          </span>
                        ) : null}
                      </td>
                      <td className="px-4 py-3 text-right whitespace-nowrap">
                        {inv.status === "Issued" ? (
                          <div className="flex justify-end gap-1.5">
                            <Button
                              size="sm"
                              variant="outline"
                              className="h-8 gap-1.5"
                              onClick={() => setPaying(inv)}
                            >
                              <CheckCircle2 className="h-3.5 w-3.5" />
                              Record payment
                            </Button>
                            <Button
                              size="sm"
                              variant="ghost"
                              className="h-8 gap-1.5 text-muted-foreground hover:text-red-700"
                              onClick={() => setVoiding(inv)}
                            >
                              <Ban className="h-3.5 w-3.5" />
                              Cancel
                            </Button>
                          </div>
                        ) : (
                          <span className="text-xs text-muted-foreground">
                            {inv.status === "Void" ? "Cancelled" : "—"}
                          </span>
                        )}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      <RecordPaymentDialog invoice={paying} onClose={() => setPaying(null)} onSaved={refresh} />
      <VoidInvoiceDialog invoice={voiding} onClose={() => setVoiding(null)} onSaved={refresh} />
    </div>
  );
}

function SummaryCard({
  label,
  value,
  detail,
  tone = "plain",
}: {
  label: string;
  value: string;
  detail: string;
  tone?: "plain" | "warn";
}) {
  return (
    <div className="rounded-lg border bg-card p-4 shadow-sm">
      <p className="text-xs uppercase tracking-wide text-muted-foreground">{label}</p>
      <p
        className={`mt-1 text-2xl font-semibold tabular-nums ${
          tone === "warn" ? "text-amber-700" : ""
        }`}
      >
        {value}
      </p>
      <p className="mt-0.5 text-sm text-muted-foreground">{detail}</p>
    </div>
  );
}

/**
 * Recording a payment, with the detail that makes it a record rather than a flag.
 *
 * The date and reference have always been stored; nothing ever collected them, so every settled
 * invoice said only "Paid" and nobody could match it to a bank transfer afterwards.
 */
function RecordPaymentDialog({
  invoice,
  onClose,
  onSaved,
}: {
  invoice: InvoiceRow | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [paidOn, setPaidOn] = useState("");
  const [reference, setReference] = useState("");
  const [saving, setSaving] = useState(false);
  const [loadedFor, setLoadedFor] = useState<string | null>(null);

  if (invoice && loadedFor !== invoice.id) {
    setLoadedFor(invoice.id);
    setPaidOn(new Date().toISOString().slice(0, 10));
    setReference("");
  }

  const save = async () => {
    if (!invoice) return;
    setSaving(true);
    try {
      await subscriptionApi.markPaid(invoice.id, {
        paidOn: paidOn || null,
        paymentReference: reference.trim() || null,
      });
      toast.success(`${invoice.number} recorded as paid`);
      onSaved();
      onClose();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not record the payment.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={!!invoice} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-[460px]">
        <DialogHeader>
          <DialogTitle>Record payment</DialogTitle>
          <DialogDescription>
            {invoice ? (
              <>
                {invoice.number} · {money(invoice.amount, invoice.currency)}. This moves the
                agency&rsquo;s next payment date on by one {invoice.cycle.toLowerCase()} period.
              </>
            ) : null}
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 py-2">
          <div className="space-y-1.5">
            <Label htmlFor="paid-on">Date received</Label>
            <Input
              id="paid-on"
              type="date"
              value={paidOn}
              onChange={(e) => setPaidOn(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="paid-ref">Reference</Label>
            <Input
              id="paid-ref"
              value={reference}
              onChange={(e) => setReference(e.target.value)}
              placeholder="Transfer or receipt number"
            />
            <p className="text-xs text-muted-foreground">
              Optional, but it is what lets someone match this to a bank statement later.
            </p>
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={save} disabled={saving} className="bg-green-800 hover:bg-green-900">
            {saving ? "Saving…" : "Record payment"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Cancelling an invoice, and saying plainly what that does and does not mean. */
function VoidInvoiceDialog({
  invoice,
  onClose,
  onSaved,
}: {
  invoice: InvoiceRow | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [saving, setSaving] = useState(false);

  const confirm = async () => {
    if (!invoice) return;
    setSaving(true);
    try {
      await subscriptionApi.voidInvoice(invoice.id);
      toast.success(`${invoice.number} cancelled`);
      onSaved();
      onClose();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not cancel the invoice.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={!!invoice} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-[460px]">
        <DialogHeader>
          <DialogTitle>Cancel this invoice?</DialogTitle>
          <DialogDescription>
            {invoice ? (
              <>
                {invoice.number} · {money(invoice.amount, invoice.currency)} for{" "}
                {onDate(invoice.periodStart)} – {onDate(invoice.periodEnd)}.
              </>
            ) : null}
          </DialogDescription>
        </DialogHeader>
        <div className="rounded-lg border bg-muted/40 px-4 py-3 text-sm text-muted-foreground">
          <p className="font-medium text-foreground">What this does</p>
          <p className="mt-1">
            Use this when an invoice should never have been raised — the wrong amount, the wrong
            period, or a duplicate. The agency stops owing it.
          </p>
          <p className="mt-2">
            It is not deleted. The invoice stays in this history marked{" "}
            <span className="font-medium text-foreground">Void</span>, and its number is not reused,
            so the record stays complete and auditable. You can raise a corrected invoice for the
            same period afterwards.
          </p>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Keep it
          </Button>
          <Button variant="destructive" onClick={confirm} disabled={saving}>
            {saving ? "Cancelling…" : "Cancel invoice"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
