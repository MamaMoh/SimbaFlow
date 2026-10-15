"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { Loader2, Plus } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { PageHeader } from "@/components/ui/page-header";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
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
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  EXPENSE_CATEGORIES,
  accountingApi,
  useExpenses,
  type ExpenseRow,
} from "@/lib/api/accounting";
import { formatEtb } from "@/lib/api/commissions";
import { usePermissions } from "@/lib/tenant/tenant-provider";

const METHODS = [
  { value: "Cash", label: "Cash" },
  { value: "BankTransfer", label: "Bank transfer" },
  { value: "MobileMoney", label: "Mobile money" },
  { value: "Other", label: "Other" },
];

const categoryLabel = (value: string) =>
  EXPENSE_CATEGORIES.find((c) => c.value === value)?.label ?? value;

const today = () => new Date().toISOString().slice(0, 10);
const monthStart = () => today().slice(0, 8) + "01";

export default function ExpensesPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView = hasPermission("accounting.read") || hasPermission("system.admin");
  const canPost = hasPermission("accounting.post") || hasPermission("system.admin");

  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(today);
  const { expenses, isLoading, error, mutate } = useExpenses({ from, to });

  const [open, setOpen] = useState(false);
  const [voiding, setVoiding] = useState<ExpenseRow | null>(null);

  const totals = useMemo(() => {
    const live = expenses.filter((e) => !e.voidedAt);
    const payouts = live
      .filter((e) => EXPENSE_CATEGORIES.find((c) => c.value === e.category)?.payout)
      .reduce((sum, e) => sum + e.amountEtb, 0);
    const all = live.reduce((sum, e) => sum + e.amountEtb, 0);
    return { all, payouts, operating: all - payouts };
  }, [expenses]);

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }
  if (!canView) return <AccessDenied resource="expenses" />;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Expenses" />

      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="flex flex-wrap items-end gap-3">
          <div className="space-y-1.5">
            <Label htmlFor="exp-from">From</Label>
            <Input id="exp-from" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="exp-to">To</Label>
            <Input id="exp-to" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </div>
        </div>
        {canPost ? (
          <Button
            size="sm"
            className="h-9 gap-1.5 bg-green-800 text-white hover:bg-green-900"
            onClick={() => setOpen(true)}
          >
            <Plus className="h-3.5 w-3.5" />
            Record expense
          </Button>
        ) : null}
      </div>

      <div className="grid gap-4 sm:grid-cols-3">
        <Tile label="Operating expenses" value={totals.operating} hint="Counts against profit" />
        <Tile label="Payouts" value={totals.payouts} hint="Medical, ticket and partner money passed on" />
        <Tile label="Total paid out" value={totals.all} />
      </div>

      {error ? (
        <LoadError
          message={error instanceof Error ? error.message : "Failed to load"}
          onRetry={() => mutate()}
        />
      ) : (
        <div className="overflow-x-auto rounded-lg border bg-card p-4 shadow-sm">
          {isLoading ? (
            <p className="text-sm text-muted-foreground">Loading…</p>
          ) : expenses.length === 0 ? (
            <p className="text-sm text-muted-foreground">No expenses in this period.</p>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Date</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead>Payee</TableHead>
                  <TableHead>Method</TableHead>
                  <TableHead className="text-right">Amount</TableHead>
                  <TableHead className="text-right">ETB</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {expenses.map((e) => (
                  <TableRow key={e.id} className={e.voidedAt ? "text-muted-foreground" : undefined}>
                    <TableCell className="whitespace-nowrap">{e.expenseDate}</TableCell>
                    <TableCell className="whitespace-nowrap">
                      {categoryLabel(e.category)}
                      {e.voidedAt ? (
                        <span
                          className="ml-2 rounded bg-red-100 px-1.5 py-0.5 text-xs font-medium text-red-800"
                          title={e.voidReason ?? undefined}
                        >
                          Void
                        </span>
                      ) : null}
                    </TableCell>
                    <TableCell>{e.description}</TableCell>
                    <TableCell>{e.payee || "—"}</TableCell>
                    <TableCell>{METHODS.find((m) => m.value === e.method)?.label ?? e.method}</TableCell>
                    <TableCell className={`text-right tabular-nums ${e.voidedAt ? "line-through" : ""}`}>
                      {e.amount.toFixed(2)} {e.currency}
                    </TableCell>
                    <TableCell className={`text-right tabular-nums ${e.voidedAt ? "line-through" : ""}`}>
                      {formatEtb(e.amountEtb)}
                    </TableCell>
                    <TableCell className="whitespace-nowrap text-right">
                      {e.journalEntryId ? (
                        <Link
                          href={`/finance/journals/${e.journalEntryId}`}
                          className="text-primary underline-offset-4 hover:underline"
                        >
                          Journal
                        </Link>
                      ) : null}
                      {canPost && !e.voidedAt ? (
                        <Button
                          variant="ghost"
                          size="sm"
                          className="ml-2 h-7 text-red-700 hover:text-red-800"
                          onClick={() => setVoiding(e)}
                        >
                          Void
                        </Button>
                      ) : null}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </div>
      )}

      <RecordExpenseSheet open={open} onOpenChange={setOpen} onRecorded={() => mutate()} />
      <VoidExpenseDialog expense={voiding} onClose={() => setVoiding(null)} onVoided={() => mutate()} />
    </div>
  );
}

function Tile({ label, value, hint }: { label: string; value: number; hint?: string }) {
  return (
    <div className="rounded-lg border bg-card p-4 shadow-sm">
      <p className="text-sm text-muted-foreground">{label}</p>
      <p className="text-xl font-semibold tabular-nums">{formatEtb(value)}</p>
      {hint ? <p className="text-xs text-muted-foreground">{hint}</p> : null}
    </div>
  );
}

function RecordExpenseSheet({
  open,
  onOpenChange,
  onRecorded,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onRecorded: () => void;
}) {
  const [expenseDate, setExpenseDate] = useState(today);
  const [category, setCategory] = useState("Other");
  const [description, setDescription] = useState("");
  const [payee, setPayee] = useState("");
  const [amount, setAmount] = useState("");
  const [currency, setCurrency] = useState("ETB");
  const [method, setMethod] = useState("Cash");
  const [reference, setReference] = useState("");
  const [busy, setBusy] = useState(false);

  const payout = EXPENSE_CATEGORIES.find((c) => c.value === category)?.payout;

  const submit = async () => {
    const value = Number(amount);
    if (!(value > 0)) {
      toast.error("Amount must be greater than 0");
      return;
    }
    if (!description.trim()) {
      toast.error("Description is required");
      return;
    }
    setBusy(true);
    try {
      await accountingApi.recordExpense({
        expenseDate,
        category,
        description: description.trim(),
        amount: value,
        currency,
        method,
        payee: payee.trim() || undefined,
        reference: reference.trim() || undefined,
      });
      toast.success("Expense recorded and posted");
      setDescription("");
      setPayee("");
      setAmount("");
      setReference("");
      onOpenChange(false);
      onRecorded();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not record the expense");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="flex w-[420px] flex-col gap-4 overflow-y-auto sm:max-w-[420px]">
        <SheetHeader>
          <SheetTitle>Record expense</SheetTitle>
          <SheetDescription>
            {payout
              ? "A payout settles money collected for someone else, so it reduces what is owed onward rather than profit."
              : "Posts the expense against cash for the date paid."}
          </SheetDescription>
        </SheetHeader>
        <div className="space-y-3 px-6">
          <div className="space-y-1.5">
            <Label>Date paid</Label>
            <Input type="date" value={expenseDate} onChange={(e) => setExpenseDate(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label>Category</Label>
            <Select value={category} onValueChange={setCategory}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {EXPENSE_CATEGORIES.map((c) => (
                  <SelectItem key={c.value} value={c.value}>
                    {c.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label>Description</Label>
            <Input value={description} onChange={(e) => setDescription(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label>Paid to</Label>
            <Input value={payee} onChange={(e) => setPayee(e.target.value)} />
          </div>
          <div className="grid grid-cols-[1fr_6rem] gap-3">
            <div className="space-y-1.5">
              <Label>Amount</Label>
              <Input
                type="number"
                min={0.01}
                step="0.01"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Currency</Label>
              <Input
                value={currency}
                maxLength={3}
                onChange={(e) => setCurrency(e.target.value.toUpperCase())}
              />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label>Method</Label>
            <Select value={method} onValueChange={setMethod}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {METHODS.map((m) => (
                  <SelectItem key={m.value} value={m.value}>
                    {m.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label>Reference</Label>
            <Input value={reference} onChange={(e) => setReference(e.target.value)} />
          </div>
        </div>
        <Button
          className="mx-6 mb-6 mt-auto bg-green-800 text-white hover:bg-green-900"
          disabled={busy}
          onClick={submit}
        >
          {busy ? "Saving…" : "Save"}
        </Button>
      </SheetContent>
    </Sheet>
  );
}

function VoidExpenseDialog({
  expense,
  onClose,
  onVoided,
}: {
  expense: ExpenseRow | null;
  onClose: () => void;
  onVoided: () => void;
}) {
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    if (!expense) return;
    if (!reason.trim()) {
      toast.error("Say why the expense is being voided");
      return;
    }
    setBusy(true);
    try {
      await accountingApi.voidExpense(expense.id, reason.trim());
      toast.success("Expense voided and reversed in the ledger");
      setReason("");
      onClose();
      onVoided();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not void the expense");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={!!expense} onOpenChange={(o) => !o && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Void expense</DialogTitle>
          <DialogDescription>
            {expense ? `${expense.description}, ${formatEtb(expense.amountEtb)}. ` : ""}
            The expense stays listed, marked void, and a reversing entry is posted.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-1.5">
          <Label htmlFor="exp-void-reason">Reason</Label>
          <Textarea id="exp-void-reason" value={reason} onChange={(e) => setReason(e.target.value)} />
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button variant="destructive" onClick={submit} disabled={busy}>
            {busy ? "Voiding…" : "Void expense"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
