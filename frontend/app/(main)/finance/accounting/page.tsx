"use client";

import { useMemo, useState } from "react";
import useSWR from "swr";
import { toast } from "sonner";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { addDemoLedgerEntry, getDemoLedger } from "@/lib/demo/admin-demo-store";

export default function AccountingPage() {
  const { data: rows = [], mutate } = useSWR("demo-ledger", () => getDemoLedger(), {
    revalidateOnFocus: false,
  });
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({
    date: new Date().toISOString().slice(0, 10),
    reference: "",
    account: "",
    description: "",
    debit: "",
    credit: "",
    currency: "ETB",
  });

  const totals = useMemo(() => {
    return rows.reduce(
      (acc, r) => ({
        debit: acc.debit + r.debit,
        credit: acc.credit + r.credit,
      }),
      { debit: 0, credit: 0 },
    );
  }, [rows]);

  const save = () => {
    if (!form.reference || !form.account) {
      toast.error("Reference and account are required");
      return;
    }
    addDemoLedgerEntry({
      date: form.date,
      reference: form.reference,
      account: form.account,
      description: form.description,
      debit: Number(form.debit) || 0,
      credit: Number(form.credit) || 0,
      currency: form.currency,
    });
    toast.success("Journal line posted");
    setOpen(false);
    mutate();
  };

  return (
    <div className="flex flex-col gap-5 p-4 md:p-6">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold tracking-tight">Accounting</h1>
          <p className="text-sm text-muted-foreground">Demo ledger — post journal lines in-session</p>
        </div>
        <Button className="gap-1" onClick={() => setOpen(true)}>
          <Plus className="h-4 w-4" /> Post entry
        </Button>
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        <div className="rounded-xl border bg-card p-4 shadow-sm">
          <div className="text-xs uppercase text-muted-foreground">Total debit</div>
          <div className="mt-1 text-2xl font-bold tabular-nums">{totals.debit.toLocaleString()}</div>
        </div>
        <div className="rounded-xl border bg-card p-4 shadow-sm">
          <div className="text-xs uppercase text-muted-foreground">Total credit</div>
          <div className="mt-1 text-2xl font-bold tabular-nums">{totals.credit.toLocaleString()}</div>
        </div>
      </div>

      <div className="overflow-hidden rounded-xl border bg-card shadow-sm">
        <table className="w-full text-sm">
          <thead className="bg-muted/40 text-left text-[11px] uppercase text-muted-foreground">
            <tr>
              <th className="p-3">Date</th>
              <th className="p-3">Ref</th>
              <th className="p-3">Account</th>
              <th className="p-3">Description</th>
              <th className="p-3 text-right">Debit</th>
              <th className="p-3 text-right">Credit</th>
              <th className="p-3">CCY</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.id} className="border-t">
                <td className="p-3 whitespace-nowrap">{r.date}</td>
                <td className="p-3 font-mono text-xs">{r.reference}</td>
                <td className="p-3">{r.account}</td>
                <td className="p-3 text-muted-foreground">{r.description}</td>
                <td className="p-3 text-right tabular-nums">{r.debit ? r.debit.toLocaleString() : "—"}</td>
                <td className="p-3 text-right tabular-nums">{r.credit ? r.credit.toLocaleString() : "—"}</td>
                <td className="p-3">{r.currency}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <Sheet open={open} onOpenChange={setOpen}>
        <SheetContent className="flex w-full flex-col px-6 sm:max-w-[440px]">
          <SheetHeader>
            <SheetTitle>Post journal entry</SheetTitle>
            <SheetDescription>Demo double-entry line (in-session only).</SheetDescription>
          </SheetHeader>
          <div className="mt-4 space-y-3">
            <div className="space-y-2">
              <Label>Date</Label>
              <Input type="date" value={form.date} onChange={(e) => setForm((f) => ({ ...f, date: e.target.value }))} />
            </div>
            <div className="space-y-2">
              <Label>Reference</Label>
              <Input value={form.reference} onChange={(e) => setForm((f) => ({ ...f, reference: e.target.value }))} placeholder="JE-1060" />
            </div>
            <div className="space-y-2">
              <Label>Account</Label>
              <Input value={form.account} onChange={(e) => setForm((f) => ({ ...f, account: e.target.value }))} placeholder="1100 · Cash" />
            </div>
            <div className="space-y-2">
              <Label>Description</Label>
              <Input value={form.description} onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))} />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-2">
                <Label>Debit</Label>
                <Input type="number" value={form.debit} onChange={(e) => setForm((f) => ({ ...f, debit: e.target.value }))} />
              </div>
              <div className="space-y-2">
                <Label>Credit</Label>
                <Input type="number" value={form.credit} onChange={(e) => setForm((f) => ({ ...f, credit: e.target.value }))} />
              </div>
            </div>
            <div className="space-y-2">
              <Label>Currency</Label>
              <Input value={form.currency} onChange={(e) => setForm((f) => ({ ...f, currency: e.target.value }))} />
            </div>
            <Button className="w-full" onClick={save}>
              Post
            </Button>
          </div>
        </SheetContent>
      </Sheet>
    </div>
  );
}
