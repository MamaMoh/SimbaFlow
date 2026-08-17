"use client";

import Link from "next/link";
import { Loader2, ShieldAlert, ShieldCheck } from "lucide-react";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { useComplianceAlerts, type ComplianceAlert } from "@/lib/api/insights";
import { PageHeader } from "@/components/ui/page-header";
import { cn } from "@/lib/utils";

const BUCKET_META: Record<
  string,
  { label: string; badge: string }
> = {
  expired: {
    label: "Expired",
    badge: "bg-rose-500/15 text-rose-600 dark:text-rose-400",
  },
  within30: {
    label: "≤ 30 days",
    badge: "bg-amber-500/15 text-amber-600 dark:text-amber-400",
  },
  within90: {
    label: "≤ 90 days",
    badge: "bg-sky-500/15 text-sky-600 dark:text-sky-400",
  },
};

function AlertRow({ a }: { a: ComplianceAlert }) {
  const meta = BUCKET_META[a.bucket] ?? BUCKET_META.within90;
  return (
    <Link
      href={`/candidates/${a.candidateId}`}
      className="flex items-center gap-3 border-b px-4 py-3 text-sm transition last:border-0 hover:bg-muted/40"
    >
      <span
        className={cn(
          "shrink-0 rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide",
          meta.badge
        )}
      >
        {meta.label}
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate font-medium">{a.candidateName}</p>
        <p className="truncate text-xs text-muted-foreground">
          {a.category} · {a.detail}
          {a.passport ? ` · ${a.passport}` : ""}
        </p>
      </div>
      {a.expiryDate && (
        <span className="shrink-0 text-xs tabular-nums text-muted-foreground">
          {a.expiryDate.slice(0, 10)}
        </span>
      )}
    </Link>
  );
}

export default function CompliancePage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canRead =
    hasPermission("candidate.read") || hasPermission("system.admin");
  const { data, error, isLoading, mutate } = useComplianceAlerts(
    !permsLoading && canRead
  );

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }

  if (!canRead) return <AccessDenied resource="compliance alerts" />;

  const tiles = [
    { key: "expired", value: data?.expiredCount ?? 0, ...BUCKET_META.expired },
    { key: "within30", value: data?.within30Count ?? 0, ...BUCKET_META.within30 },
    { key: "within90", value: data?.within90Count ?? 0, ...BUCKET_META.within90 },
  ];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Compliance"
        description="Passports and Tasheer bookings that are expired or expiring soon"
      />

      {error && <LoadError message={error.message} onRetry={() => mutate()} />}

      <div className="grid grid-cols-3 gap-4">
        {tiles.map((t) => (
          <div key={t.key} className="rounded-xl border bg-card p-4 shadow-sm">
            <span
              className={cn(
                "inline-block rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide",
                t.badge
              )}
            >
              {t.label}
            </span>
            <p className="mt-2 text-2xl font-bold tabular-nums">{t.value}</p>
          </div>
        ))}
      </div>

      <div className="rounded-xl border bg-card shadow-sm">
        <div className="flex items-center gap-2 border-b px-4 py-3">
          <ShieldAlert className="h-4 w-4 text-amber-500" />
          <h2 className="text-sm font-semibold">Attention needed</h2>
        </div>
        {isLoading ? (
          <div className="flex items-center justify-center p-10 text-sm text-muted-foreground">
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            Loading alerts…
          </div>
        ) : (data?.alerts.length ?? 0) === 0 ? (
          <div className="flex flex-col items-center justify-center gap-2 p-10 text-sm text-muted-foreground">
            <ShieldCheck className="h-7 w-7 text-emerald-500" />
            All clear — no upcoming expiries.
          </div>
        ) : (
          <div>
            {data!.alerts.map((a, i) => (
              <AlertRow key={`${a.candidateId}-${a.category}-${i}`} a={a} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
