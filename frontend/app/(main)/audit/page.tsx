"use client";

import { useState } from "react";
import Link from "next/link";
import { ChevronLeft, ChevronRight, Loader2, ScrollText } from "lucide-react";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { PageHeader } from "@/components/ui/page-header";
import { AgencyAccessLog } from "@/components/platform/agency-access-log";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import {
  useAuditTrail,
  type AuditTrailEntry,
  type AuditTrailKind,
} from "@/lib/api/audit";
import { cn } from "@/lib/utils";

const PAGE_SIZE = 50;
const ANY = "all";

function When({ iso }: { iso: string }) {
  const d = new Date(iso);
  return (
    <span className="whitespace-nowrap tabular-nums" title={d.toISOString()}>
      {d.toLocaleDateString()} {d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
    </span>
  );
}

function Subject({ e }: { e: AuditTrailEntry }) {
  if (!e.entityType && !e.entityId) return <span className="text-muted-foreground">—</span>;
  const shortId = e.entityId ? (
    <span className="font-mono text-xs text-muted-foreground">{e.entityId.slice(0, 8)}</span>
  ) : null;
  const text = e.entityLabel ? (
    <span className="font-medium">{e.entityLabel}</span>
  ) : (
    <span>
      {e.entityType} {shortId}
    </span>
  );
  if (e.entityType === "Candidate" && e.entityId) {
    return (
      <Link href={`/candidates/${e.entityId}`} className="hover:underline">
        {text}
      </Link>
    );
  }
  return text;
}

export default function AuditTrailPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canRead = hasPermission("audit.read") || hasPermission("system.admin");

  const [kind, setKind] = useState<AuditTrailKind>("changes");
  const [userId, setUserId] = useState(ANY);
  const [action, setAction] = useState(ANY);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [failedOnly, setFailedOnly] = useState(false);
  const [page, setPage] = useState(1);

  const { data, error, isLoading, isValidating, mutate } = useAuditTrail(
    {
      kind,
      page,
      pageSize: PAGE_SIZE,
      userId: userId === ANY ? undefined : userId,
      action: action === ANY ? undefined : action,
      from: from || undefined,
      to: to || undefined,
      failedOnly,
    },
    !permsLoading && canRead
  );

  // Any change of filter starts again from the first page.
  const refilter = <T,>(set: (v: T) => void) => (v: T) => {
    set(v);
    setPage(1);
  };

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }

  if (!canRead) return <AccessDenied resource="the audit trail" />;

  const items = data?.items ?? [];
  const total = data?.total ?? 0;
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  const filtered = userId !== ANY || action !== ANY || from || to || failedOnly;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Audit trail" count={data ? total : undefined} />

      <AgencyAccessLog />

      <Tabs
        value={kind}
        onValueChange={(v) => {
          setKind(v as AuditTrailKind);
          // The two logs record different actions, so an action picked in one means nothing in the other.
          setAction(ANY);
          setPage(1);
        }}
      >
        <TabsList>
          <TabsTrigger value="changes" className="px-4">Changes</TabsTrigger>
          <TabsTrigger value="views" className="px-4">Views &amp; prints</TabsTrigger>
        </TabsList>
      </Tabs>

      <div className="flex flex-wrap items-end gap-3">
        <div className="flex flex-col gap-1">
          <Label htmlFor="audit-user" className="text-xs text-muted-foreground">Person</Label>
          <Select value={userId} onValueChange={refilter(setUserId)}>
            <SelectTrigger id="audit-user" className="h-8 w-[190px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>Anyone</SelectItem>
              {(data?.users ?? []).map((u) => (
                <SelectItem key={u.value} value={u.value}>{u.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="flex flex-col gap-1">
          <Label htmlFor="audit-action" className="text-xs text-muted-foreground">Action</Label>
          <Select value={action} onValueChange={refilter(setAction)}>
            <SelectTrigger id="audit-action" className="h-8 w-[220px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>Any action</SelectItem>
              {(data?.actions ?? []).map((a) => (
                <SelectItem key={a.value} value={a.value}>{a.label}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="flex flex-col gap-1">
          <Label htmlFor="audit-from" className="text-xs text-muted-foreground">From</Label>
          <Input
            id="audit-from"
            type="date"
            className="h-8 w-[150px]"
            value={from}
            max={to || undefined}
            onChange={(e) => refilter(setFrom)(e.target.value)}
          />
        </div>
        <div className="flex flex-col gap-1">
          <Label htmlFor="audit-to" className="text-xs text-muted-foreground">To</Label>
          <Input
            id="audit-to"
            type="date"
            className="h-8 w-[150px]"
            value={to}
            min={from || undefined}
            onChange={(e) => refilter(setTo)(e.target.value)}
          />
        </div>
        {kind === "changes" && (
          <div className="flex h-8 items-center gap-2">
            <Switch id="audit-failed" checked={failedOnly} onCheckedChange={refilter(setFailedOnly)} />
            <Label htmlFor="audit-failed" className="text-sm">Failed only</Label>
          </div>
        )}
        {filtered && (
          <Button
            variant="ghost"
            size="sm"
            className="h-8"
            onClick={() => {
              setUserId(ANY);
              setAction(ANY);
              setFrom("");
              setTo("");
              setFailedOnly(false);
              setPage(1);
            }}
          >
            Clear
          </Button>
        )}
      </div>

      {error && <LoadError message={error.message} onRetry={() => mutate()} />}

      <div className="overflow-x-auto rounded-lg border bg-card shadow-sm">
        {isLoading && !data ? (
          <div className="flex items-center justify-center p-10 text-sm text-muted-foreground">
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            Loading the audit trail…
          </div>
        ) : items.length === 0 ? (
          <div className="flex flex-col items-center justify-center gap-2 p-10 text-sm text-muted-foreground">
            <ScrollText className="h-7 w-7" />
            {filtered ? "Nothing matches these filters." : kind === "views" ? "No views or prints recorded yet." : "No changes recorded yet."}
          </div>
        ) : (
          <table className={cn("w-full text-sm", isValidating && "opacity-70")}>
            <thead className="border-b bg-muted/40 text-left text-xs text-muted-foreground">
              <tr>
                <th className="px-4 py-2 font-medium">When</th>
                <th className="px-4 py-2 font-medium">Who</th>
                <th className="px-4 py-2 font-medium">Action</th>
                <th className="px-4 py-2 font-medium">On</th>
                {kind === "changes" && <th className="px-4 py-2 font-medium">Result</th>}
                <th className="px-4 py-2 font-medium">From</th>
              </tr>
            </thead>
            <tbody>
              {items.map((e) => (
                <tr key={e.id} className="border-b last:border-0 hover:bg-muted/30">
                  <td className="px-4 py-2 text-muted-foreground"><When iso={e.at} /></td>
                  <td className="px-4 py-2">
                    <div className="font-medium">{e.userName}</div>
                    {e.userEmail && <div className="text-xs text-muted-foreground">{e.userEmail}</div>}
                  </td>
                  <td className="px-4 py-2" title={e.rawAction}>{e.action}</td>
                  <td className="px-4 py-2"><Subject e={e} /></td>
                  {kind === "changes" && (
                    <td className="px-4 py-2">
                      <span
                        className={cn(
                          "rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide",
                          e.success
                            ? "bg-emerald-500/15 text-emerald-600 dark:text-emerald-400"
                            : "bg-rose-500/15 text-rose-600 dark:text-rose-400"
                        )}
                      >
                        {e.success ? "OK" : "Failed"}
                      </span>
                    </td>
                  )}
                  <td className="px-4 py-2 font-mono text-xs text-muted-foreground" title={e.userAgent ?? undefined}>
                    {e.ipAddress ?? "—"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {total > PAGE_SIZE && (
        <div className="flex items-center justify-end gap-2 text-sm text-muted-foreground">
          <span className="tabular-nums">
            Page {page} of {pages}
          </span>
          <Button
            variant="outline"
            size="icon"
            className="h-8 w-8"
            disabled={page <= 1}
            onClick={() => setPage((p) => p - 1)}
            aria-label="Previous page"
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <Button
            variant="outline"
            size="icon"
            className="h-8 w-8"
            disabled={page >= pages}
            onClick={() => setPage((p) => p + 1)}
            aria-label="Next page"
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
      )}
    </div>
  );
}
