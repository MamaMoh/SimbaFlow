"use client";

import Link from "next/link";
import {
  Loader2,
  Clock,
  CalendarClock,
  AlertTriangle,
  CheckCircle2,
} from "lucide-react";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { useMyTasks, type MyTaskItem } from "@/lib/api/insights";
import { PageHeader } from "@/components/ui/page-header";
import { cn } from "@/lib/utils";

const TYPE_ICON: Record<string, React.ElementType> = {
  exception: AlertTriangle,
  passport: CalendarClock,
  overdue: Clock,
};

const SEVERITY: Record<string, string> = {
  high: "bg-rose-500/15 text-rose-600 dark:text-rose-400",
  medium: "bg-amber-500/15 text-amber-600 dark:text-amber-400",
  low: "bg-sky-500/15 text-sky-600 dark:text-sky-400",
};

function TaskRow({ item }: { item: MyTaskItem }) {
  const Icon = TYPE_ICON[item.type] ?? Clock;
  const body = (
    <div className="flex items-center gap-3 border-b px-4 py-3 text-sm transition last:border-0 hover:bg-muted/40">
      <span
        className={cn(
          "inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-lg",
          SEVERITY[item.severity] ?? SEVERITY.low
        )}
      >
        <Icon className="h-4 w-4" />
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate font-medium">{item.title}</p>
        <p className="truncate text-xs text-muted-foreground">{item.subtitle}</p>
      </div>
    </div>
  );
  return item.href ? <Link href={item.href}>{body}</Link> : body;
}

export default function MyWorkPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canRead =
    hasPermission("candidate.read") || hasPermission("system.admin");
  const { data, error, isLoading, mutate } = useMyTasks(
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

  if (!canRead) return <AccessDenied resource="your task list" />;

  const tiles = [
    { key: "overdue", label: "Overdue candidates", value: data?.overdueCount ?? 0, icon: Clock },
    { key: "expiring", label: "Expiring passports", value: data?.expiringSoonCount ?? 0, icon: CalendarClock },
    { key: "exceptions", label: "Open exceptions", value: data?.openExceptionCount ?? 0, icon: AlertTriangle },
  ];

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="My work"
        description="Candidates and cases that need attention"
      />

      {error && <LoadError message={error.message} onRetry={() => mutate()} />}

      <div className="grid grid-cols-3 gap-4">
        {tiles.map((t) => {
          const Icon = t.icon;
          return (
            <div key={t.key} className="rounded-xl border bg-card p-4 shadow-sm">
              <Icon className="h-4 w-4 text-muted-foreground" />
              <p className="mt-2 text-2xl font-bold tabular-nums">{t.value}</p>
              <p className="text-xs text-muted-foreground">{t.label}</p>
            </div>
          );
        })}
      </div>

      <div className="rounded-xl border bg-card shadow-sm">
        <div className="border-b px-4 py-3">
          <h2 className="text-sm font-semibold">Action items</h2>
        </div>
        {isLoading ? (
          <div className="flex items-center justify-center p-10 text-sm text-muted-foreground">
            <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            Loading tasks…
          </div>
        ) : (data?.items.length ?? 0) === 0 ? (
          <div className="flex flex-col items-center justify-center gap-2 p-10 text-sm text-muted-foreground">
            <CheckCircle2 className="h-7 w-7 text-emerald-500" />
            You&apos;re all caught up.
          </div>
        ) : (
          <div>
            {data!.items.map((item, i) => (
              <TaskRow key={i} item={item} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
