"use client";

import { useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { AccessDenied, LoadError, PageAlert } from "@/components/ui/page-alert";
import { ExceptionStatusBadge } from "@/components/workflow/remaining-days-badge";
import { exceptionsApi, useExceptionCase } from "@/lib/api/exceptions";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { toast } from "sonner";
import { Loader2 } from "lucide-react";

export default function ExceptionDetailPage() {
  const params = useParams();
  const id = typeof params.id === "string" ? params.id : null;
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canView =
    hasPermission("arrival.read") ||
    hasPermission("arrival.exception") ||
    hasPermission("system.admin");
  const canWrite = hasPermission("arrival.exception") || hasPermission("system.admin");

  const { exceptionCase, isLoading, error, mutate } = useExceptionCase(id);
  const [note, setNote] = useState("");
  const [party, setParty] = useState("Agency");
  const [amount, setAmount] = useState("");
  const [resolution, setResolution] = useState("");
  const [busy, setBusy] = useState(false);

  if (permsLoading || (isLoading && !exceptionCase)) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin mr-2" />
        Loading…
      </div>
    );
  }

  if (!canView) return <AccessDenied resource="Exception case" />;
  if (error) return <LoadError message={error.message} onRetry={() => mutate()} />;
  if (!exceptionCase) {
    return <PageAlert variant="error" title="Case not found" />;
  }

  const closed = exceptionCase.status === "Closed";

  const run = async (fn: () => Promise<void>, ok: string) => {
    setBusy(true);
    try {
      await fn();
      toast.success(ok);
      mutate();
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "Failed");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex flex-col gap-6 max-w-3xl">
      <div>
        <Link href="/workflow/exceptions" className="text-sm text-muted-foreground hover:underline">
          ← Exceptions
        </Link>
        <h1 className="mt-2 text-2xl font-semibold">{exceptionCase.candidateName}</h1>
        <p className="text-sm text-muted-foreground">
          {exceptionCase.passportNumber} · {exceptionCase.type} ·{" "}
          <ExceptionStatusBadge status={exceptionCase.status} />
        </p>
        <Link
          href={`/candidates/${exceptionCase.candidateId}`}
          className="text-sm text-primary hover:underline"
        >
          Open candidate
        </Link>
      </div>

      {canWrite && !closed && (
        <div className="flex flex-wrap gap-2">
          <Select
            onValueChange={(status) =>
              run(() => exceptionsApi.updateStatus(exceptionCase.id, status), "Status updated")
            }
          >
            <SelectTrigger className="w-[200px]">
              <SelectValue placeholder="Update status…" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="Open">Open</SelectItem>
              <SelectItem value="UnderInvestigation">Under investigation</SelectItem>
              <SelectItem value="Resolved">Resolved</SelectItem>
            </SelectContent>
          </Select>
        </div>
      )}

      <section className="space-y-3">
        <h2 className="font-medium">Notes</h2>
        <ul className="space-y-2 text-sm">
          {exceptionCase.notes.length === 0 && (
            <li className="text-muted-foreground">No notes yet</li>
          )}
          {exceptionCase.notes.map((n) => (
            <li key={n.id} className="rounded-md border p-3">
              <p>{n.body}</p>
              <p className="mt-1 text-xs text-muted-foreground">
                {new Date(n.createdAt).toLocaleString()}
              </p>
            </li>
          ))}
        </ul>
        {canWrite && !closed && (
          <div className="space-y-2">
            <Textarea
              value={note}
              onChange={(e) => setNote(e.target.value)}
              placeholder="Add investigation note…"
            />
            <Button
              size="sm"
              disabled={busy || !note.trim()}
              onClick={() =>
                run(async () => {
                  await exceptionsApi.addNote(exceptionCase.id, note.trim());
                  setNote("");
                }, "Note added")
              }
            >
              Add note
            </Button>
          </div>
        )}
      </section>

      <section className="space-y-3">
        <h2 className="font-medium">Liabilities</h2>
        <ul className="space-y-2 text-sm">
          {exceptionCase.liabilities.length === 0 && (
            <li className="text-muted-foreground">None assigned</li>
          )}
          {exceptionCase.liabilities.map((l) => (
            <li key={l.id} className="rounded-md border p-3">
              {l.party}: {l.amount} {l.currency}
              {l.notes ? ` — ${l.notes}` : ""}
            </li>
          ))}
        </ul>
        {canWrite && !closed && (
          <div className="grid gap-2 sm:grid-cols-3">
            <div>
              <Label>Party</Label>
              <Select value={party} onValueChange={setParty}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Agency">Agency</SelectItem>
                  <SelectItem value="Partner">Partner</SelectItem>
                  <SelectItem value="Candidate">Candidate</SelectItem>
                  <SelectItem value="Other">Other</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Amount</Label>
              <Input
                type="number"
                min={0}
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
              />
            </div>
            <div className="flex items-end">
              <Button
                size="sm"
                disabled={busy || amount === ""}
                onClick={() =>
                  run(async () => {
                    await exceptionsApi.assignLiability(
                      exceptionCase.id,
                      party,
                      Number(amount)
                    );
                    setAmount("");
                  }, "Liability assigned")
                }
              >
                Assign
              </Button>
            </div>
          </div>
        )}
      </section>

      {canWrite && !closed && (
        <section className="space-y-2 border-t pt-4">
          <h2 className="font-medium">Close case</h2>
          <Textarea
            value={resolution}
            onChange={(e) => setResolution(e.target.value)}
            placeholder="Resolution summary (required)"
          />
          <Button
            variant="destructive"
            size="sm"
            disabled={busy || !resolution.trim()}
            onClick={() =>
              run(
                () => exceptionsApi.close(exceptionCase.id, resolution.trim()),
                "Case closed"
              )
            }
          >
            Close exception
          </Button>
        </section>
      )}

      {closed && exceptionCase.resolutionSummary && (
        <PageAlert
          variant="success"
          title="Case closed"
          description={exceptionCase.resolutionSummary}
        />
      )}
    </div>
  );
}
