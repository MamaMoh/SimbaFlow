"use client";

import { useState } from "react";
import useSWR from "swr";
import { PageHeader } from "@/components/ui/page-header";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { usePlatformStaff } from "@/lib/platform/use-platform-staff";

type Option = { value: string; label: string };

type Entry = {
  id: string;
  at: string;
  userName: string;
  isPlatformTeam: boolean;
  action: string;
  agency: string | null;
  details: string | null;
  success: boolean;
};

type AuditPage = {
  items: Entry[];
  total: number;
  page: number;
  pageSize: number;
  users: Option[];
  agencies: Option[];
};

const ANY = "any";

const fetcher = async (url: string): Promise<AuditPage> => {
  const res = await fetch(url);
  const body = await res.json().catch(() => null);
  if (!res.ok || !body?.isSuccess) throw new Error(body?.error || "Could not load the audit trail");
  return body.data;
};

/** A stored JSON detail as a short readable line: "reason: …, hours: 4". */
function describeDetails(raw: string | null): string | null {
  if (!raw) return null;
  try {
    const obj = JSON.parse(raw);
    if (!obj || typeof obj !== "object") return String(obj);
    return Object.entries(obj)
      .filter(([, v]) => v !== null && v !== "" && v !== undefined)
      .map(([k, v]) => `${k}: ${typeof v === "object" ? JSON.stringify(v) : v}`)
      .join(", ");
  } catch {
    return raw;
  }
}

/**
 * The audit trail across every agency. "Platform team only" narrows it to what the platform team
 * and the system did: entering agencies, suspensions, billing, announcements and help requests.
 */
export default function PlatformAuditPage() {
  const { isStaff, isLoading } = usePlatformStaff();
  const [agency, setAgency] = useState(ANY);
  const [user, setUser] = useState(ANY);
  const [platformOnly, setPlatformOnly] = useState(true);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [page, setPage] = useState(1);

  const params = new URLSearchParams({ page: String(page), pageSize: "50" });
  if (agency !== ANY) params.set("tenantId", agency);
  if (user !== ANY) params.set("userId", user);
  if (platformOnly) params.set("scope", "platform");
  if (from) params.set("from", from);
  if (to) params.set("to", to);

  const { data, error, mutate } = useSWR(isStaff ? `/api/proxy/platform/audit?${params}` : null, fetcher, {
    keepPreviousData: true,
  });

  if (!isLoading && !isStaff) return <AccessDenied resource="the platform audit trail" />;
  if (error) return <LoadError message={error.message} onRetry={() => mutate()} />;

  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;
  const reset = () => setPage(1);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Platform audit" count={data?.total} />

      <div className="flex flex-wrap items-end gap-3">
        <div className="w-56 space-y-1">
          <span className="text-xs text-muted-foreground">Agency</span>
          <Select value={agency} onValueChange={(v) => { setAgency(v); reset(); }}>
            <SelectTrigger className="h-8">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>All agencies</SelectItem>
              {(data?.agencies ?? []).map((o) => (
                <SelectItem key={o.value} value={o.value}>
                  {o.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="w-56 space-y-1">
          <span className="text-xs text-muted-foreground">Platform team member</span>
          <Select value={user} onValueChange={(v) => { setUser(v); reset(); }}>
            <SelectTrigger className="h-8">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>Anyone</SelectItem>
              {(data?.users ?? []).map((o) => (
                <SelectItem key={o.value} value={o.value}>
                  {o.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1">
          <span className="text-xs text-muted-foreground">From</span>
          <Input type="date" className="h-8 w-40" value={from} onChange={(e) => { setFrom(e.target.value); reset(); }} />
        </div>
        <div className="space-y-1">
          <span className="text-xs text-muted-foreground">To</span>
          <Input type="date" className="h-8 w-40" value={to} onChange={(e) => { setTo(e.target.value); reset(); }} />
        </div>
        <Button
          size="sm"
          variant={platformOnly ? "default" : "outline"}
          className="h-8"
          onClick={() => { setPlatformOnly(!platformOnly); reset(); }}
        >
          {platformOnly ? "Platform team only" : "Everyone"}
        </Button>
      </div>

      <div className="overflow-x-auto rounded-lg border bg-card shadow-sm">
        <table className="w-full text-sm">
          <thead className="border-b bg-muted/40 text-left text-xs text-muted-foreground">
            <tr>
              <th className="px-4 py-2 font-medium">When</th>
              <th className="px-4 py-2 font-medium">Who</th>
              <th className="px-4 py-2 font-medium">What</th>
              <th className="px-4 py-2 font-medium">Agency</th>
            </tr>
          </thead>
          <tbody className="divide-y">
            {(data?.items ?? []).length === 0 ? (
              <tr>
                <td colSpan={4} className="px-4 py-10 text-center text-muted-foreground">
                  Nothing recorded for these filters.
                </td>
              </tr>
            ) : (
              data!.items.map((e) => {
                const details = describeDetails(e.details);
                return (
                  <tr key={e.id} className="align-top">
                    <td className="whitespace-nowrap px-4 py-2 text-muted-foreground">
                      {new Date(e.at).toLocaleString([], { dateStyle: "medium", timeStyle: "short" })}
                    </td>
                    <td className="px-4 py-2">
                      {e.userName}
                      {e.isPlatformTeam ? (
                        <Badge variant="outline" className="ml-1.5 text-[10px]">Platform</Badge>
                      ) : null}
                    </td>
                    <td className="px-4 py-2">
                      <span className={e.success ? "" : "text-red-700"}>{e.action}</span>
                      {!e.success ? <span className="ml-1 text-xs text-red-700">(failed)</span> : null}
                      {details ? <p className="text-xs text-muted-foreground break-all">{details}</p> : null}
                    </td>
                    <td className="px-4 py-2 text-muted-foreground">{e.agency ?? "—"}</td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {pages > 1 && (
        <div className="flex items-center justify-end gap-2 text-sm">
          <Button size="sm" variant="outline" disabled={page <= 1} onClick={() => setPage(page - 1)}>
            Previous
          </Button>
          <span className="text-muted-foreground">
            Page {page} of {pages}
          </span>
          <Button size="sm" variant="outline" disabled={page >= pages} onClick={() => setPage(page + 1)}>
            Next
          </Button>
        </div>
      )}
    </div>
  );
}
