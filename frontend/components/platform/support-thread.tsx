"use client";

import { useState } from "react";
import useSWR from "swr";
import { toast } from "sonner";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Textarea } from "@/components/ui/textarea";

export type TicketStatus = "Open" | "Answered" | "Closed";

type Ticket = {
  id: string;
  agency: string | null;
  subject: string;
  status: TicketStatus;
  messages: { id: string; body: string; fromPlatform: boolean; createdAt: string; author: string }[];
};

const fetcher = async (url: string): Promise<Ticket> => {
  const res = await fetch(url);
  const body = await res.json().catch(() => null);
  if (!res.ok || !body?.isSuccess) throw new Error(body?.error || "Could not load the request");
  return body.data;
};

export const STATUS_LABEL: Record<TicketStatus, string> = {
  Open: "Waiting on SimbaFlow",
  Answered: "Answered",
  Closed: "Closed",
};

export function StatusPill({ status, platformView }: { status: TicketStatus; platformView?: boolean }) {
  const label = platformView && status === "Open" ? "Needs a reply" : STATUS_LABEL[status];
  const tone =
    status === "Closed"
      ? "text-muted-foreground"
      : (platformView ? status === "Open" : status === "Answered")
        ? "border-amber-300 bg-amber-50 text-amber-800"
        : "";
  return (
    <Badge variant="outline" className={tone}>
      {label}
    </Badge>
  );
}

/**
 * One help request and its conversation, with a reply box. `base` is the
 * request's own URL on either side: the agency's (/api/proxy/support/{id}) or the platform's
 * (/api/proxy/platform/support/{id}).
 */
export function SupportThread({
  base,
  platformView,
  onChanged,
}: {
  base: string;
  platformView?: boolean;
  onChanged?: () => void;
}) {
  const { data, error, mutate } = useSWR(base, fetcher, { refreshInterval: 30_000 });
  const [reply, setReply] = useState("");
  const [busy, setBusy] = useState(false);

  if (error) return <p className="p-6 text-sm text-destructive">{error.message}</p>;
  if (!data)
    return (
      <div className="flex items-center justify-center p-10 text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin" />
      </div>
    );

  const post = async (path: string, payload?: unknown) => {
    setBusy(true);
    try {
      const res = await fetch(`${base}/${path}`, {
        method: "POST",
        headers: payload ? { "Content-Type": "application/json" } : undefined,
        body: payload ? JSON.stringify(payload) : undefined,
      });
      const body = await res.json().catch(() => ({}));
      if (!res.ok || !body?.isSuccess) {
        toast.error(body?.error || "That did not go through");
        return false;
      }
      await mutate();
      onChanged?.();
      return true;
    } finally {
      setBusy(false);
    }
  };

  const send = async () => {
    if (await post("reply", { message: reply })) setReply("");
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 className="text-base font-semibold">{data.subject}</h2>
          {platformView && data.agency ? (
            <p className="text-sm text-muted-foreground">{data.agency}</p>
          ) : null}
        </div>
        <StatusPill status={data.status} platformView={platformView} />
      </div>

      <ol className="flex flex-col gap-3">
        {data.messages.map((m) => (
          <li
            key={m.id}
            className={`rounded-lg border p-3 text-sm ${m.fromPlatform ? "border-green-200 bg-green-50/60" : "bg-card"}`}
          >
            <p className="mb-1 text-xs text-muted-foreground">
              <span className="font-medium text-foreground">{m.author}</span> ·{" "}
              {new Date(m.createdAt).toLocaleString([], { dateStyle: "medium", timeStyle: "short" })}
            </p>
            <p className="whitespace-pre-line">{m.body}</p>
          </li>
        ))}
      </ol>

      <div className="space-y-2">
        <Textarea
          rows={3}
          value={reply}
          onChange={(e) => setReply(e.target.value)}
          placeholder={data.status === "Closed" ? "Write to reopen this request" : "Write a reply"}
        />
        <div className="flex justify-end gap-2">
          {data.status !== "Closed" && (
            <Button variant="outline" size="sm" disabled={busy} onClick={() => post("close")}>
              Close request
            </Button>
          )}
          <Button
            size="sm"
            disabled={busy || reply.trim().length === 0}
            onClick={send}
            className="bg-green-800 hover:bg-green-900"
          >
            Send reply
          </Button>
        </div>
      </div>
    </div>
  );
}
