"use client";

import useSWR from "swr";
import { ShieldCheck } from "lucide-react";
import { Badge } from "@/components/ui/badge";

type Visit = {
  id: string;
  who: string;
  reason: string;
  startedAt: string;
  endsAt: string;
  isLive: boolean;
};

const fetcher = async (url: string): Promise<Visit[]> => {
  const res = await fetch(url);
  if (!res.ok) return [];
  const body = await res.json().catch(() => null);
  return Array.isArray(body?.data) ? body.data : [];
};

const when = (iso: string) => new Date(iso).toLocaleString([], { dateStyle: "medium", timeStyle: "short" });

/**
 * When the SimbaFlow platform team worked inside this agency, who, and why. Each visit has a
 * reason given up front and runs out on its own; what was done during it is in the trail below.
 */
export function AgencyAccessLog() {
  const { data } = useSWR("/api/proxy/agency-access", fetcher, { revalidateOnFocus: false });
  const visits = data ?? [];
  if (visits.length === 0) return null;

  return (
    <div className="rounded-lg border bg-card shadow-sm">
      <div className="flex items-center gap-2 border-b px-4 py-3">
        <ShieldCheck className="h-4 w-4 text-green-700" />
        <h2 className="text-sm font-semibold">SimbaFlow team access</h2>
      </div>
      <ul className="divide-y">
        {visits.slice(0, 10).map((v) => (
          <li key={v.id} className="flex flex-wrap items-start gap-3 px-4 py-3 text-sm">
            <div className="min-w-0 flex-1">
              <p>
                <span className="font-medium">{v.who}</span>
                <span className="text-muted-foreground"> · {when(v.startedAt)} to {when(v.endsAt)}</span>
              </p>
              <p className="text-muted-foreground">{v.reason}</p>
            </div>
            {v.isLive ? <Badge className="bg-amber-100 text-amber-800">In your agency now</Badge> : null}
          </li>
        ))}
      </ul>
    </div>
  );
}
