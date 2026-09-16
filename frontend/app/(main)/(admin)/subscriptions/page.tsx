"use client";

import { useState } from "react";
import { toast } from "sonner";
import { Ban, CheckCircle2, FileText, Loader2, Receipt } from "lucide-react";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import {
  subscriptionApi,
  useInvoices,
  useSubscriptions,
  type SubscriptionRow,
} from "@/lib/api/subscriptions";

function money(amount: number, currency: string) {
  return `${currency} ${amount.toLocaleString(undefined, { minimumFractionDigits: 0 })}`;
}

function onDate(iso: string | null) {
  if (!iso) return "—";
  return new Date(iso).toLocaleDateString(undefined, {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}

/** When the next payment lands, said the way someone would say it. */
function dueLabel(row: SubscriptionRow) {
  if (!row.nextPaymentDue) return "Not scheduled";
  const d = row.daysUntilDue;
  if (d === null) return onDate(row.nextPaymentDue);
  if (d < 0) return `${Math.abs(d)} ${Math.abs(d) === 1 ? "day" : "days"} overdue`;
  if (d === 0) return "Due today";
  return `in ${d} ${d === 1 ? "day" : "days"}`;
}

export default function SubscriptionsPage() {
  const { isSuperAdmin } = usePermissions();
  const { rows, error, isLoading, mutate } = useSubscriptions(isSuperAdmin);

  const [editing, setEditing] = useState<SubscriptionRow | null>(null);
  const [invoicing, setInvoicing] = useState<SubscriptionRow | null>(null);
  const [viewing, setViewing] = useState<SubscriptionRow | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);

  if (!isSuperAdmin) return <AccessDenied resource="subscriptions" />;

  const setStatus = async (row: SubscriptionRow, status: "Active" | "Suspended") => {
    setBusyId(row.id);
    try {
      await subscriptionApi.update(row.id, { status });
      toast.success(
        status === "Active"
          ? `${row.name} can use the system again`
          : `${row.name} is suspended — their staff can no longer sign in to their data`,
      );
      void mutate();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not change the status.");
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Subscriptions"
        description="What each agency pays, when it is next due, and whether they can use the system."
      />

      {error ? (
        <LoadError message="Could not load subscriptions" onRetry={() => mutate()} />
      ) : null}

      <div className="overflow-x-auto rounded-lg border bg-card shadow-sm">
        <table className="w-full min-w-[860px] text-sm">
          <thead>
            <tr className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
              <th className="px-4 py-3 font-medium">Agency</th>
              <th className="px-4 py-3 font-medium">Access</th>
              <th className="px-4 py-3 font-medium">Plan</th>
              <th className="px-4 py-3 font-medium">Next payment</th>
              <th className="px-4 py-3 font-medium">Unpaid</th>
              <th className="px-4 py-3 text-right font-medium">Actions</th>
            </tr>
          </thead>
          <tbody>
            {isLoading ? (
              <tr>
                <td colSpan={6} className="px-4 py-10 text-center text-muted-foreground">
                  <Loader2 className="mx-auto h-4 w-4 animate-spin" />
                </td>
              </tr>
            ) : rows.length === 0 ? (
              <tr>
                <td colSpan={6} className="px-4 py-10 text-center text-muted-foreground">
                  No agencies yet.
                </td>
              </tr>
            ) : (
              rows.map((row) => (
                <tr key={row.id} className="border-b last:border-0">
                  <td className="px-4 py-3 font-medium">{row.name}</td>
                  <td className="px-4 py-3">
                    <Badge
                      variant={row.hasAccess ? "default" : "destructive"}
                      className={row.hasAccess ? "bg-green-700 hover:bg-green-700" : ""}
                    >
                      {row.status}
                    </Badge>
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">
                    {row.subscriptionAmount > 0
                      ? `${money(row.subscriptionAmount, row.subscriptionCurrency)} · ${row.cycle.toLowerCase()}`
                      : "Not charged"}
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className={
                        row.notice === "Overdue"
                          ? "font-medium text-red-700"
                          : row.notice === "DueSoon" || row.notice === "DueToday"
                            ? "font-medium text-amber-700"
                            : "text-muted-foreground"
                      }
                    >
                      {dueLabel(row)}
                    </span>
                    {row.nextPaymentDue ? (
                      <span className="block text-xs text-muted-foreground">
                        {onDate(row.nextPaymentDue)}
                      </span>
                    ) : null}
                  </td>
                  <td className="px-4 py-3">
                    {row.outstanding > 0 ? (
                      <span className="font-medium text-amber-700">{row.outstanding}</span>
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap justify-end gap-1.5">
                      <Button size="sm" variant="outline" className="h-8" onClick={() => setEditing(row)}>
                        Plan
                      </Button>
                      <Button size="sm" variant="outline" className="h-8 gap-1.5" onClick={() => setInvoicing(row)}>
                        <Receipt className="h-3.5 w-3.5" />
                        Invoice
                      </Button>
                      <Button size="sm" variant="ghost" className="h-8 gap-1.5" onClick={() => setViewing(row)}>
                        <FileText className="h-3.5 w-3.5" />
                        History
                      </Button>
                      {row.hasAccess ? (
                        <Button
                          size="sm"
                          variant="ghost"
                          className="h-8 gap-1.5 text-red-700 hover:bg-red-50 hover:text-red-800"
                          disabled={busyId === row.id}
                          onClick={() => setStatus(row, "Suspended")}
                        >
                          <Ban className="h-3.5 w-3.5" />
                          Suspend
                        </Button>
                      ) : (
                        <Button
                          size="sm"
                          variant="ghost"
                          className="h-8 gap-1.5 text-green-700 hover:bg-green-50 hover:text-green-800"
                          disabled={busyId === row.id}
                          onClick={() => setStatus(row, "Active")}
                        >
                          <CheckCircle2 className="h-3.5 w-3.5" />
                          Reactivate
                        </Button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      <PlanDialog row={editing} onClose={() => setEditing(null)} onSaved={() => void mutate()} />
      <InvoiceDialog row={invoicing} onClose={() => setInvoicing(null)} onSaved={() => void mutate()} />
      <HistoryDialog row={viewing} onClose={() => setViewing(null)} onChanged={() => void mutate()} />
    </div>
  );
}

function PlanDialog({
  row,
  onClose,
  onSaved,
}: {
  row: SubscriptionRow | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [amount, setAmount] = useState("");
  const [currency, setCurrency] = useState("ETB");
  const [cycle, setCycle] = useState("Monthly");
  const [due, setDue] = useState("");
  const [saving, setSaving] = useState(false);
  const [loadedFor, setLoadedFor] = useState<string | null>(null);

  if (row && loadedFor !== row.id) {
    setLoadedFor(row.id);
    setAmount(row.subscriptionAmount ? String(row.subscriptionAmount) : "");
    setCurrency(row.subscriptionCurrency || "ETB");
    setCycle(row.cycle);
    setDue(row.nextPaymentDue ? row.nextPaymentDue.slice(0, 10) : "");
  }

  const save = async () => {
    if (!row) return;
    setSaving(true);
    try {
      await subscriptionApi.update(row.id, {
        amount: amount ? Number(amount) : 0,
        currency,
        cycle,
        nextPaymentDue: due || null,
      });
      toast.success(`${row.name}'s plan saved`);
      onSaved();
      onClose();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not save the plan.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={!!row} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-[420px]">
        <DialogHeader>
          <DialogTitle>{row?.name} plan</DialogTitle>
          <DialogDescription>
            What this agency pays and when. Leaving the date empty means nothing is scheduled and
            they see no renewal notice.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 py-2">
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="sub-amount">Amount per period</Label>
              <Input
                id="sub-amount"
                inputMode="decimal"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                placeholder="0"
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="sub-currency">Currency</Label>
              <Input
                id="sub-currency"
                value={currency}
                onChange={(e) => setCurrency(e.target.value.toUpperCase())}
                maxLength={8}
              />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label>Billed</Label>
            <Select value={cycle} onValueChange={setCycle}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Monthly">Monthly</SelectItem>
                <SelectItem value="Yearly">Yearly</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="sub-due">Next payment due</Label>
            <Input id="sub-due" type="date" value={due} onChange={(e) => setDue(e.target.value)} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={save} disabled={saving} className="bg-green-800 hover:bg-green-900">
            {saving ? "Saving…" : "Save plan"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function InvoiceDialog({
  row,
  onClose,
  onSaved,
}: {
  row: SubscriptionRow | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [start, setStart] = useState("");
  const [cycle, setCycle] = useState("Monthly");
  const [amount, setAmount] = useState("");
  const [dueInDays, setDueInDays] = useState("14");
  const [saving, setSaving] = useState(false);
  const [loadedFor, setLoadedFor] = useState<string | null>(null);

  if (row && loadedFor !== row.id) {
    setLoadedFor(row.id);
    setStart(row.nextPaymentDue ? row.nextPaymentDue.slice(0, 10) : "");
    setCycle(row.cycle);
    setAmount(row.subscriptionAmount ? String(row.subscriptionAmount) : "");
  }

  const generate = async () => {
    if (!row) return;
    setSaving(true);
    try {
      const res = await subscriptionApi.generateInvoice(row.id, {
        periodStart: start || null,
        cycle,
        amount: amount ? Number(amount) : null,
        dueInDays: Number(dueInDays) || 14,
      });
      toast.success(`${res?.data?.number ?? "Invoice"} raised for ${row.name}`);
      onSaved();
      onClose();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not raise the invoice.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={!!row} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-[420px]">
        <DialogHeader>
          <DialogTitle>Invoice {row?.name}</DialogTitle>
          <DialogDescription>
            Raises one period&rsquo;s charge. The period runs from the start date to the day before
            the next one begins, so consecutive invoices meet without overlapping.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-4 py-2">
          <div className="space-y-1.5">
            <Label>Period</Label>
            <Select value={cycle} onValueChange={setCycle}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Monthly">One month</SelectItem>
                <SelectItem value="Yearly">One year</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="inv-start">Starting</Label>
            <Input id="inv-start" type="date" value={start} onChange={(e) => setStart(e.target.value)} />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="inv-amount">Amount</Label>
              <Input
                id="inv-amount"
                inputMode="decimal"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                placeholder={row ? String(row.subscriptionAmount) : "0"}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="inv-due">Payable within</Label>
              <Input
                id="inv-due"
                inputMode="numeric"
                value={dueInDays}
                onChange={(e) => setDueInDays(e.target.value)}
              />
            </div>
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={generate} disabled={saving} className="bg-green-800 hover:bg-green-900">
            {saving ? "Raising…" : "Raise invoice"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function HistoryDialog({
  row,
  onClose,
  onChanged,
}: {
  row: SubscriptionRow | null;
  onClose: () => void;
  onChanged: () => void;
}) {
  const { invoices, mutate } = useInvoices(row?.id ?? null);
  const [busy, setBusy] = useState<string | null>(null);

  const act = async (id: string, what: "paid" | "void") => {
    setBusy(id);
    try {
      if (what === "paid") await subscriptionApi.markPaid(id, {});
      else await subscriptionApi.voidInvoice(id);
      toast.success(what === "paid" ? "Recorded as paid" : "Invoice voided");
      void mutate();
      onChanged();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "That did not work.");
    } finally {
      setBusy(null);
    }
  };

  return (
    <Dialog open={!!row} onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-[680px]">
        <DialogHeader>
          <DialogTitle>{row?.name} invoices</DialogTitle>
          <DialogDescription>
            Recording a payment moves this agency&rsquo;s next payment date on by one period.
          </DialogDescription>
        </DialogHeader>
        <div className="max-h-[50vh] overflow-y-auto">
          {invoices.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">
              Nothing invoiced yet.
            </p>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b text-left text-xs uppercase tracking-wide text-muted-foreground">
                  <th className="py-2 font-medium">Number</th>
                  <th className="py-2 font-medium">Period</th>
                  <th className="py-2 font-medium">Due</th>
                  <th className="py-2 font-medium">Amount</th>
                  <th className="py-2 font-medium">Status</th>
                  <th className="py-2" />
                </tr>
              </thead>
              <tbody>
                {invoices.map((inv) => (
                  <tr key={inv.id} className="border-b last:border-0">
                    <td className="py-2 font-medium">{inv.number}</td>
                    <td className="py-2 text-muted-foreground">
                      {onDate(inv.periodStart)} – {onDate(inv.periodEnd)}
                    </td>
                    <td className="py-2 text-muted-foreground">{onDate(inv.dueOn)}</td>
                    <td className="py-2">{money(inv.amount, inv.currency)}</td>
                    <td className="py-2">
                      <Badge
                        variant={inv.status === "Paid" ? "default" : "outline"}
                        className={inv.status === "Paid" ? "bg-green-700 hover:bg-green-700" : ""}
                      >
                        {inv.status}
                      </Badge>
                    </td>
                    <td className="py-2 text-right">
                      {inv.status === "Issued" ? (
                        <div className="flex justify-end gap-1">
                          <Button
                            size="sm"
                            variant="outline"
                            className="h-7"
                            disabled={busy === inv.id}
                            onClick={() => act(inv.id, "paid")}
                          >
                            Mark paid
                          </Button>
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-7 text-muted-foreground"
                            disabled={busy === inv.id}
                            onClick={() => act(inv.id, "void")}
                          >
                            Void
                          </Button>
                        </div>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            Close
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
