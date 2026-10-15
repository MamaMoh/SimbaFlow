"use client";

import { useState } from "react";
import useSWR from "swr";
import { PageHeader } from "@/components/ui/page-header";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { Button } from "@/components/ui/button";
import { SupportThread, StatusPill, type TicketStatus } from "@/components/platform/support-thread";
import { usePlatformStaff } from "@/lib/platform/use-platform-staff";
import { ago } from "@/lib/platform/health";

type Row = {
  id: string;
  agency: string;
  subject: string;
  status: TicketStatus;
  raisedBy: string;
  lastActivityAt: string;
  messages: number;
};

const FILTERS: { value: string; label: string }[] = [
  { value: "Open", label: "Needs a reply" },
  { value: "Answered", label: "Answered" },
  { value: "Closed", label: "Closed" },
  { value: "", label: "All" },
];

const fetcher = async (url: string): Promise<Row[]> => {
  const res = await fetch(url);
  const body = await res.json().catch(() => null);
  if (!res.ok || !body?.isSuccess) throw new Error(body?.error || "Could not load help requests");
  return body.data;
};

/** Every agency's help requests in one inbox, oldest unanswered first. */
export default function SupportInboxPage() {
  const { isStaff, isLoading } = usePlatformStaff();
  const [status, setStatus] = useState("Open");
  const [openId, setOpenId] = useState<string | null>(null);
  const { data, error, mutate } = useSWR(
    isStaff ? `/api/proxy/platform/support${status ? `?status=${status}` : ""}` : null,
    fetcher,
    { refreshInterval: 60_000 },
  );

  if (!isLoading && !isStaff) return <AccessDenied resource="help requests" />;
  if (error) return <LoadError message={error.message} onRetry={() => mutate()} />;

  const rows = data ?? [];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Help requests" count={rows.length} />

      <div className="flex flex-wrap gap-1.5">
        {FILTERS.map((f) => (
          <Button
            key={f.value || "all"}
            size="sm"
            variant={status === f.value ? "default" : "outline"}
            className="h-7"
            onClick={() => {
              setStatus(f.value);
              setOpenId(null);
            }}
          >
            {f.label}
          </Button>
        ))}
      </div>

      <div className="grid gap-4 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
        <div className="rounded-lg border bg-card shadow-sm">
          {rows.length === 0 ? (
            <p className="p-10 text-center text-sm text-muted-foreground">Nothing here.</p>
          ) : (
            <ul className="divide-y">
              {rows.map((r) => (
                <li key={r.id}>
                  <button
                    type="button"
                    onClick={() => setOpenId(r.id)}
                    className={`flex w-full flex-col items-start gap-1 px-4 py-3 text-left text-sm transition hover:bg-muted/40 ${openId === r.id ? "bg-muted/60" : ""}`}
                  >
                    <div className="flex w-full items-center gap-2">
                      <span className="min-w-0 flex-1 truncate font-medium">{r.subject}</span>
                      <StatusPill status={r.status} platformView />
                    </div>
                    <span className="text-xs text-muted-foreground">
                      {r.agency} · {r.raisedBy} · {r.messages} message{r.messages === 1 ? "" : "s"} · {ago(r.lastActivityAt)}
                    </span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
        <div className="rounded-lg border bg-card p-4 shadow-sm">
          {openId ? (
            <SupportThread
              key={openId}
              base={`/api/proxy/platform/support/${openId}`}
              platformView
              onChanged={() => void mutate()}
            />
          ) : (
            <p className="p-10 text-center text-sm text-muted-foreground">Pick a request to read and answer it.</p>
          )}
        </div>
      </div>
    </div>
  );
}
