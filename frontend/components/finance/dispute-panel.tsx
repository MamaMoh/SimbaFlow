"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { toast } from "sonner";
import { commissionsApi, formatEtb, type CommissionDispute } from "@/lib/api/commissions";

const OUTCOMES = [
  { value: "Enforced", label: "Enforced: candidate still owes everything" },
  { value: "Waived", label: "Waived: write off the disputed amount" },
  { value: "Adjusted", label: "Adjusted: write off an agreed amount" },
];

type Resolution = { notes: string; outcome: string; amount: string };

export function DisputePanel({
  commissionId,
  disputes,
  balance,
  canUpdate,
  onChanged,
}: {
  commissionId: string;
  disputes: CommissionDispute[];
  /** What the candidate owes now, in ETB. */
  balance: number;
  canUpdate: boolean;
  onChanged: () => void;
}) {
  const [reason, setReason] = useState("");
  const [amount, setAmount] = useState("");
  const [resolution, setResolution] = useState<Record<string, Resolution>>({});
  const [busy, setBusy] = useState(false);

  const openDispute = disputes.find((d) => d.status === "Open");
  const draftFor = (id: string): Resolution =>
    resolution[id] ?? { notes: "", outcome: "Enforced", amount: "" };
  const setDraft = (id: string, patch: Partial<Resolution>) =>
    setResolution((prev) => ({ ...prev, [id]: { ...draftFor(id), ...patch } }));

  const open = async () => {
    if (!reason.trim()) {
      toast.error("Reason is required");
      return;
    }
    const value = amount.trim() ? Number(amount) : undefined;
    if (value !== undefined && !(value > 0 && value <= balance)) {
      toast.error(`The disputed amount must be between 0 and the balance (${formatEtb(balance)})`);
      return;
    }
    setBusy(true);
    try {
      await commissionsApi.openDispute(commissionId, reason.trim(), value);
      toast.success("Dispute opened");
      setReason("");
      setAmount("");
      onChanged();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Failed to open dispute");
    } finally {
      setBusy(false);
    }
  };

  const resolve = async (disputeId: string) => {
    const draft = draftFor(disputeId);
    if (!draft.notes.trim()) {
      toast.error("Resolution notes are required");
      return;
    }
    const value = draft.outcome === "Adjusted" ? Number(draft.amount) : undefined;
    if (draft.outcome === "Adjusted" && !(value! > 0)) {
      toast.error("Enter the amount to write off");
      return;
    }
    setBusy(true);
    try {
      await commissionsApi.resolveDispute(disputeId, draft.notes.trim(), draft.outcome, value);
      toast.success("Dispute resolved");
      onChanged();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Failed to resolve dispute");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-4">
      {canUpdate && !openDispute ? (
        <div className="space-y-2 rounded-md border p-3">
          <Label>Open dispute</Label>
          <Input
            placeholder="Reason"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
          />
          <Input
            type="number"
            min={0.01}
            step="0.01"
            placeholder="Amount in dispute, ETB (optional)"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
          />
          <Button size="sm" variant="outline" disabled={busy} onClick={open}>
            Open dispute
          </Button>
        </div>
      ) : null}

      {disputes.length === 0 ? (
        <p className="text-sm text-muted-foreground">No disputes.</p>
      ) : (
        <ul className="space-y-3">
          {disputes.map((d) => {
            const draft = draftFor(d.id);
            return (
              <li key={d.id} className="rounded-md border p-3 space-y-2">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex items-center gap-2">
                    <Badge variant={d.status === "Open" ? "destructive" : "secondary"}>
                      {d.status}
                    </Badge>
                    {d.outcome ? <Badge variant="outline">{d.outcome}</Badge> : null}
                  </div>
                  <span className="text-xs text-muted-foreground">
                    {new Date(d.openedAt).toLocaleString()}
                  </span>
                </div>
                <p className="text-sm">
                  {d.reason}
                  {d.disputedAmountEtb != null ? (
                    <span className="text-muted-foreground"> · {formatEtb(d.disputedAmountEtb)} in dispute</span>
                  ) : null}
                </p>
                {d.resolutionNotes ? (
                  <p className="text-sm text-muted-foreground">
                    Resolution: {d.resolutionNotes}
                    {d.adjustmentAmountEtb > 0 ? ` · ${formatEtb(d.adjustmentAmountEtb)} written off` : ""}
                  </p>
                ) : null}
                {canUpdate && d.status === "Open" ? (
                  <div className="space-y-2 pt-1">
                    <Label>Outcome</Label>
                    <Select value={draft.outcome} onValueChange={(v) => setDraft(d.id, { outcome: v })}>
                      <SelectTrigger>
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {OUTCOMES.map((o) => (
                          <SelectItem key={o.value} value={o.value}>
                            {o.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    {draft.outcome === "Waived" ? (
                      <p className="text-xs text-muted-foreground">
                        Writes off{" "}
                        {formatEtb(Math.min(d.disputedAmountEtb ?? balance, Math.max(balance, 0)))}.
                      </p>
                    ) : null}
                    {draft.outcome === "Adjusted" ? (
                      <Input
                        type="number"
                        min={0.01}
                        step="0.01"
                        placeholder={`Amount to write off, ETB (up to ${formatEtb(balance)})`}
                        value={draft.amount}
                        onChange={(e) => setDraft(d.id, { amount: e.target.value })}
                      />
                    ) : null}
                    <Label>Resolution notes</Label>
                    <Input
                      value={draft.notes}
                      onChange={(e) => setDraft(d.id, { notes: e.target.value })}
                    />
                    <Button size="sm" disabled={busy} onClick={() => resolve(d.id)}>
                      Resolve
                    </Button>
                  </div>
                ) : null}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
