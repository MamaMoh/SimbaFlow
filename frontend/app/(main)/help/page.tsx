"use client";

import { useState } from "react";
import useSWR from "swr";
import { toast } from "sonner";
import { LifeBuoy } from "lucide-react";
import { PageHeader } from "@/components/ui/page-header";
import { LoadError } from "@/components/ui/page-alert";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { SupportThread, StatusPill, type TicketStatus } from "@/components/platform/support-thread";
import { ago } from "@/lib/platform/health";

type Row = {
  id: string;
  subject: string;
  status: TicketStatus;
  lastActivityAt: string;
  messages: number;
};

const fetcher = async (url: string): Promise<Row[]> => {
  const res = await fetch(url);
  const body = await res.json().catch(() => null);
  if (!res.ok || !body?.isSuccess) throw new Error(body?.error || "Could not load your help requests");
  return body.data;
};

/**
 * Ask the SimbaFlow team for help. Everyone in the agency sees the agency's requests, so a
 * colleague can pick up an answer.
 */
export default function HelpPage() {
  const { data, error, mutate } = useSWR("/api/proxy/support", fetcher, { refreshInterval: 60_000 });
  const [openId, setOpenId] = useState<string | null>(null);
  const [writing, setWriting] = useState(false);
  const [subject, setSubject] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);

  if (error) return <LoadError message={error.message} onRetry={() => mutate()} />;
  const rows = data ?? [];

  const raise = async () => {
    setBusy(true);
    try {
      const res = await fetch("/api/proxy/support", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ subject, message }),
      });
      const body = await res.json().catch(() => ({}));
      if (!res.ok || !body?.isSuccess) {
        toast.error(body?.error || "Could not send your request");
        return;
      }
      toast.success("Sent. The SimbaFlow team will reply here.");
      setSubject("");
      setMessage("");
      setWriting(false);
      setOpenId(body.data.id);
      void mutate();
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Help"
        count={rows.length}
        actions={
          <Button
            size="sm"
            className="h-8 bg-green-800 text-white hover:bg-green-900"
            onClick={() => {
              setWriting(true);
              setOpenId(null);
            }}
          >
            <LifeBuoy className="mr-1.5 h-3.5 w-3.5" /> Ask for help
          </Button>
        }
      />

      <div className="grid gap-4 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
        <div className="rounded-lg border bg-card shadow-sm">
          {rows.length === 0 ? (
            <p className="p-10 text-center text-sm text-muted-foreground">
              No requests yet. Ask the SimbaFlow team anything about using the system.
            </p>
          ) : (
            <ul className="divide-y">
              {rows.map((r) => (
                <li key={r.id}>
                  <button
                    type="button"
                    onClick={() => {
                      setOpenId(r.id);
                      setWriting(false);
                    }}
                    className={`flex w-full flex-col items-start gap-1 px-4 py-3 text-left text-sm transition hover:bg-muted/40 ${openId === r.id ? "bg-muted/60" : ""}`}
                  >
                    <div className="flex w-full items-center gap-2">
                      <span className="min-w-0 flex-1 truncate font-medium">{r.subject}</span>
                      <StatusPill status={r.status} />
                    </div>
                    <span className="text-xs text-muted-foreground">
                      {r.messages} message{r.messages === 1 ? "" : "s"} · {ago(r.lastActivityAt)}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <div className="rounded-lg border bg-card p-4 shadow-sm">
          {writing ? (
            <div className="space-y-4">
              <div className="space-y-1.5">
                <Label htmlFor="help-subject">What is it about?</Label>
                <Input
                  id="help-subject"
                  value={subject}
                  onChange={(e) => setSubject(e.target.value)}
                  placeholder="e.g. Cannot print the visa form for one candidate"
                />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="help-message">Tell us more</Label>
                <Textarea
                  id="help-message"
                  rows={6}
                  value={message}
                  onChange={(e) => setMessage(e.target.value)}
                  placeholder="What were you doing, what did you expect, and what happened instead?"
                />
              </div>
              <div className="flex justify-end gap-2">
                <Button variant="outline" size="sm" onClick={() => setWriting(false)}>
                  Cancel
                </Button>
                <Button
                  size="sm"
                  onClick={raise}
                  disabled={busy || !subject.trim() || !message.trim()}
                  className="bg-green-800 hover:bg-green-900"
                >
                  Send
                </Button>
              </div>
            </div>
          ) : openId ? (
            <SupportThread key={openId} base={`/api/proxy/support/${openId}`} onChanged={() => void mutate()} />
          ) : (
            <p className="p-10 text-center text-sm text-muted-foreground">
              Pick a request to read it, or ask for help.
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
