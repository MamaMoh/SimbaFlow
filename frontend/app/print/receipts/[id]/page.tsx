"use client";

import { use } from "react";
import { Loader2, Printer } from "lucide-react";
import { Button } from "@/components/ui/button";
import { formatEtb, usePaymentReceipt } from "@/lib/api/commissions";

const METHOD_LABELS: Record<string, string> = {
  Cash: "Cash",
  BankTransfer: "Bank transfer",
  MobileMoney: "Mobile money",
  Other: "Other",
};

/**
 * A payment receipt on its own page, outside the app's navigation, so the browser's print (or
 * save as PDF) gives just the receipt. A voided payment still prints, stamped void.
 */
export default function PaymentReceiptPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { receipt, isLoading, error } = usePaymentReceipt(id);

  if (isLoading) {
    return (
      <div className="flex h-screen items-center justify-center text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading receipt…
      </div>
    );
  }

  if (error || !receipt) {
    return (
      <div className="flex h-screen items-center justify-center text-sm text-muted-foreground">
        {error instanceof Error ? error.message : "Receipt not found."}
      </div>
    );
  }

  const r = receipt;
  const rows: [string, string][] = [
    ["Received from", r.candidateName],
    ["Passport", r.passportNumber],
    ...(r.partnerName ? ([["Partner", r.partnerName]] as [string, string][]) : []),
    ["Payment method", METHOD_LABELS[r.method] ?? r.method],
    ...(r.reference ? ([["Reference", r.reference]] as [string, string][]) : []),
    ...(r.notes ? ([["Notes", r.notes]] as [string, string][]) : []),
  ];

  return (
    <div className="h-screen overflow-auto bg-muted/40 print:h-auto print:overflow-visible print:bg-white">
      <div className="mx-auto max-w-2xl p-6 print:max-w-none print:p-0">
        <div className="mb-4 flex justify-end print:hidden">
          <Button size="sm" className="gap-1.5" onClick={() => window.print()}>
            <Printer className="h-4 w-4" />
            Print
          </Button>
        </div>

        <article className="relative overflow-hidden rounded-lg border bg-white p-8 text-black shadow-sm print:border-0 print:shadow-none">
          {r.voidedAt ? (
            <div className="pointer-events-none absolute inset-0 flex items-center justify-center">
              <span className="rotate-[-20deg] rounded border-4 border-red-600 px-6 py-2 text-5xl font-bold tracking-widest text-red-600 opacity-60">
                VOID
              </span>
            </div>
          ) : null}

          <header className="flex items-start justify-between gap-6 border-b pb-4">
            <div>
              <h1 className="text-lg font-semibold">{r.agencyName}</h1>
              {r.agencyAddress ? <p className="text-sm text-gray-600">{r.agencyAddress}</p> : null}
              {r.agencyPhone ? <p className="text-sm text-gray-600">{r.agencyPhone}</p> : null}
              {r.agencyLicenseNumber ? (
                <p className="text-sm text-gray-600">Licence {r.agencyLicenseNumber}</p>
              ) : null}
            </div>
            <div className="text-right">
              <p className="text-xl font-semibold tracking-tight">Receipt</p>
              <p className="font-mono text-sm">{r.receiptNumber}</p>
              <p className="text-sm text-gray-600">{new Date(r.paidAt).toLocaleDateString()}</p>
            </div>
          </header>

          <dl className="grid grid-cols-[10rem_1fr] gap-x-4 gap-y-2 py-5 text-sm">
            {rows.map(([label, value]) => (
              <div key={label} className="contents">
                <dt className="text-gray-600">{label}</dt>
                <dd>{value}</dd>
              </div>
            ))}
          </dl>

          <div className="rounded-md bg-gray-50 p-4">
            <div className="flex items-baseline justify-between">
              <span className="text-sm text-gray-600">Amount received</span>
              <span className="text-2xl font-semibold tabular-nums">
                {r.amount.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}{" "}
                {r.currency}
              </span>
            </div>
            {r.currency !== "ETB" ? (
              <p className="mt-1 text-right text-sm text-gray-600 tabular-nums">
                {formatEtb(r.amountEtb)} at {r.exchangeRateToEtb} ETB per {r.currency}
              </p>
            ) : null}
          </div>

          <dl className="mt-4 grid grid-cols-3 gap-4 text-sm">
            <div>
              <dt className="text-gray-600">Total fees</dt>
              <dd className="tabular-nums">{formatEtb(r.totalFeesEtb)}</dd>
            </div>
            <div>
              <dt className="text-gray-600">Paid to date</dt>
              <dd className="tabular-nums">{formatEtb(r.totalPaidEtb)}</dd>
            </div>
            <div>
              <dt className="text-gray-600">Balance</dt>
              <dd className="tabular-nums">{formatEtb(r.balanceEtb)}</dd>
            </div>
          </dl>

          {r.voidedAt ? (
            <p className="mt-4 text-sm text-red-700">
              Voided on {new Date(r.voidedAt).toLocaleDateString()}
              {r.voidReason ? `: ${r.voidReason}` : ""}
            </p>
          ) : null}

          <footer className="mt-10 flex items-end justify-between gap-6 text-sm">
            <div>
              <p className="text-gray-600">Received by</p>
              <p>{r.recordedBy || "—"}</p>
            </div>
            <div className="w-48 border-t pt-1 text-center text-gray-600">Signature and stamp</div>
          </footer>
        </article>
      </div>
    </div>
  );
}
