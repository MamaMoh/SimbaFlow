"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { toast } from "sonner";
import { commissionsApi, type CommissionDispute } from "@/lib/api/commissions";

export function DisputePanel({
  commissionId,
  disputes,
  canUpdate,
  onChanged,
}: {
  commissionId: string;
  disputes: CommissionDispute[];
  canUpdate: boolean;
  onChanged: () => void;
}) {
  const [reason, setReason] = useState("");
  const [resolution, setResolution] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);

  const openDispute = disputes.find((d) => d.status === "Open");

  const open = async () => {
    if (!reason.trim()) {
      toast.error("Reason is required");
      return;
    }
    setBusy(true);
    try {
      await commissionsApi.openDispute(commissionId, reason.trim());
      toast.success("Dispute opened");
      setReason("");
      onChanged();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Failed to open dispute");
    } finally {
      setBusy(false);
    }
  };

  const resolve = async (disputeId: string) => {
    const notes = resolution[disputeId]?.trim();
    if (!notes) {
      toast.error("Resolution notes are required");
      return;
    }
    setBusy(true);
    try {
      await commissionsApi.resolveDispute(disputeId, notes);
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
          <Button size="sm" variant="outline" disabled={busy} onClick={open}>
            Open dispute
          </Button>
        </div>
      ) : null}

      {disputes.length === 0 ? (
        <p className="text-sm text-muted-foreground">No disputes.</p>
      ) : (
        <ul className="space-y-3">
          {disputes.map((d) => (
            <li key={d.id} className="rounded-md border p-3 space-y-2">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <Badge variant={d.status === "Open" ? "destructive" : "secondary"}>
                  {d.status}
                </Badge>
                <span className="text-xs text-muted-foreground">
                  {new Date(d.openedAt).toLocaleString()}
                </span>
              </div>
              <p className="text-sm">{d.reason}</p>
              {d.resolutionNotes ? (
                <p className="text-sm text-muted-foreground">Resolution: {d.resolutionNotes}</p>
              ) : null}
              {canUpdate && d.status === "Open" ? (
                <div className="space-y-2 pt-1">
                  <Label>Resolution notes</Label>
                  <Input
                    value={resolution[d.id] ?? ""}
                    onChange={(e) =>
                      setResolution((prev) => ({ ...prev, [d.id]: e.target.value }))
                    }
                  />
                  <Button size="sm" disabled={busy} onClick={() => resolve(d.id)}>
                    Resolve
                  </Button>
                </div>
              ) : null}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
