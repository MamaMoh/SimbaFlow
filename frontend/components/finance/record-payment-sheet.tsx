"use client";

import { useState } from "react";
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { toast } from "sonner";
import { commissionsApi } from "@/lib/api/commissions";

const METHODS = ["Cash", "BankTransfer", "MobileMoney", "Other"];

export function RecordPaymentSheet({
  open,
  onOpenChange,
  commissionId,
  disabledReason,
  onRecorded,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  commissionId: string;
  disabledReason?: string | null;
  onRecorded: () => void;
}) {
  const [amount, setAmount] = useState("0");
  const [currency, setCurrency] = useState("ETB");
  const [method, setMethod] = useState("Cash");
  const [paidAt, setPaidAt] = useState(() => new Date().toISOString().slice(0, 10));
  const [reference, setReference] = useState("");
  const [notes, setNotes] = useState("");
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    if (disabledReason) {
      toast.error(disabledReason);
      return;
    }
    const value = Number(amount);
    if (!(value > 0)) {
      toast.error("Amount must be greater than 0");
      return;
    }
    setBusy(true);
    try {
      await commissionsApi.recordPayment(commissionId, {
        amount: value,
        currency,
        method,
        paidAt: new Date(paidAt).toISOString(),
        reference: reference || undefined,
        notes: notes || undefined,
      });
      toast.success("Payment recorded and journal posted");
      onOpenChange(false);
      setAmount("0");
      setReference("");
      setNotes("");
      onRecorded();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Payment failed");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-[420px] sm:max-w-[420px] flex flex-col gap-4 px-6">
        <SheetHeader>
          <SheetTitle>Record payment</SheetTitle>
          <SheetDescription>
            Posts Cash (1100) Dr / Revenue (4100) Cr in ETB for the converted amount.
          </SheetDescription>
        </SheetHeader>

        {disabledReason ? (
          <p className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
            {disabledReason}
          </p>
        ) : null}

        <div className="space-y-3">
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
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Currency</Label>
              <Input
                value={currency}
                onChange={(e) => setCurrency(e.target.value.toUpperCase())}
                maxLength={3}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Method</Label>
              <Select value={method} onValueChange={setMethod}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {METHODS.map((m) => (
                    <SelectItem key={m} value={m}>
                      {m}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <div className="space-y-1.5">
            <Label>Paid date</Label>
            <Input type="date" value={paidAt} onChange={(e) => setPaidAt(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label>Reference</Label>
            <Input value={reference} onChange={(e) => setReference(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label>Notes</Label>
            <Input value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
        </div>

        <Button
          className="mt-auto bg-green-800 hover:bg-green-900 text-white"
          disabled={busy || !!disabledReason}
          onClick={submit}
        >
          {busy ? "Posting…" : "Record payment"}
        </Button>
      </SheetContent>
    </Sheet>
  );
}
