"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { toast } from "sonner";
import { commissionsApi, formatEtb, type CommissionPayment } from "@/lib/api/commissions";

/**
 * Takes a payment back. The payment stays on record marked void, and a reversing journal entry
 * is posted, so the history shows both what was taken and that it was undone.
 */
export function VoidPaymentDialog({
  payment,
  onOpenChange,
  onVoided,
}: {
  payment: CommissionPayment | null;
  onOpenChange: (open: boolean) => void;
  onVoided: () => void;
}) {
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (payment) setReason("");
  }, [payment]);

  const submit = async () => {
    if (!payment) return;
    if (!reason.trim()) {
      toast.error("Say why the payment is being voided");
      return;
    }
    setBusy(true);
    try {
      await commissionsApi.voidPayment(payment.id, reason.trim());
      toast.success("Payment voided and reversed in the ledger");
      onOpenChange(false);
      onVoided();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Could not void the payment");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={!!payment} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Void payment {payment?.receiptNumber ?? ""}</DialogTitle>
          <DialogDescription>
            {payment ? formatEtb(payment.amountEtb) : ""} will no longer count as paid, and the
            balance goes back up by that much. This cannot be undone; record the payment again if
            it was voided by mistake.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-1.5">
          <Label htmlFor="void-reason">Reason</Label>
          <Textarea
            id="void-reason"
            value={reason}
            maxLength={1000}
            placeholder="For example: cheque bounced, entered on the wrong candidate"
            onChange={(e) => setReason(e.target.value)}
          />
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
            Cancel
          </Button>
          <Button variant="destructive" onClick={submit} disabled={busy}>
            {busy ? "Voiding…" : "Void payment"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
