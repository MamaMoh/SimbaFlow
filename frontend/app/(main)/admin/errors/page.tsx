"use client";

import useSWR from "swr";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/components/ui/page-header";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { AgeCell } from "@/components/data-table/age-cell";

type ErrorGroup = {
  fingerprint: string;
  count: number;
  lastSeen: string;
  firstSeen: string;
  exceptionType: string;
  message: string;
  path: string | null;
  source: string;
  affectedUsers: number;
};

const fetcher = (url: string) => fetch(url).then((r) => r.json());

/** Days between a timestamp and now, for the age chip. */
function daysSince(iso: string) {
  return Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000));
}

export default function ErrorsPage() {
  const { hasPermission, isSuperAdmin } = usePermissions();
  const allowed = isSuperAdmin || hasPermission("system.admin");

  const { data, error, mutate } = useSWR(
    allowed ? "/api/proxy/diagnostics/errors" : null,
    fetcher,
    { revalidateOnFocus: false, refreshInterval: 60_000 }
  );

  if (!allowed) return <AccessDenied resource="errors" />;
  if (error) return <LoadError message={error.message} onRetry={() => mutate()} />;

  const groups: ErrorGroup[] = data?.data ?? [];

  const resolve = async (fingerprint: string) => {
    const res = await fetch(`/api/proxy/diagnostics/errors/${fingerprint}/resolve`, { method: "POST" });
    const body = await res.json().catch(() => null);
    if (res.ok && body?.isSuccess) {
      toast.success("Marked as resolved");
      mutate();
    } else {
      toast.error(body?.error || "Could not mark it resolved");
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Errors"
        description="Unhandled failures from the API and the browser, grouped so one recurring fault is one row."
      />

      {groups.length === 0 ? (
        <div className="rounded-xl border border-dashed p-10 text-center">
          <p className="text-sm font-medium">No unresolved errors</p>
          <p className="mt-1 text-sm text-muted-foreground">
            Anything that fails is recorded here, so you don&apos;t have to wait for someone to report it.
          </p>
        </div>
      ) : (
        <ul className="divide-y rounded-xl border bg-card shadow-sm">
          {groups.map((g) => (
            <li key={g.fingerprint} className="flex flex-wrap items-start gap-4 p-4">
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="rounded bg-muted px-1.5 py-0.5 text-xs font-medium uppercase tracking-wide">
                    {g.source}
                  </span>
                  <span className="font-mono text-sm font-semibold">{g.exceptionType}</span>
                  {g.path ? (
                    <span className="truncate font-mono text-xs text-muted-foreground">{g.path}</span>
                  ) : null}
                </div>
                <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">{g.message}</p>
                <p className="mt-1 text-xs text-muted-foreground">
                  {g.count} occurrence{g.count === 1 ? "" : "s"} · {g.affectedUsers} user
                  {g.affectedUsers === 1 ? "" : "s"} · last {new Date(g.lastSeen).toLocaleString()}
                </p>
              </div>
              <div className="flex items-center gap-3">
                <AgeCell days={daysSince(g.firstSeen)} title="Days since first seen" />
                <Button size="sm" variant="outline" onClick={() => resolve(g.fingerprint)}>
                  Resolve
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
