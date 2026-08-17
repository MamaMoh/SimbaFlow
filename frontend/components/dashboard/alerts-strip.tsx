"use client";

import Link from "next/link";
import { ShieldAlert, ListChecks, ChevronRight } from "lucide-react";
import type { ComplianceAlerts, MyTasks } from "@/lib/api/insights";

export function AlertsStrip({
  compliance,
  tasks,
}: {
  compliance?: ComplianceAlerts;
  tasks?: MyTasks;
}) {
  const expiringTotal = compliance
    ? compliance.expiredCount + compliance.within30Count
    : 0;
  const taskTotal = tasks
    ? tasks.overdueCount + tasks.expiringSoonCount + tasks.openExceptionCount
    : 0;

  return (
    <div className="grid gap-4 sm:grid-cols-2">
      <Link
        href="/compliance"
        className="group flex items-center gap-4 rounded-xl border bg-card p-4 shadow-sm transition hover:border-amber-500/50 hover:shadow-md"
      >
        <span className="inline-flex h-10 w-10 items-center justify-center rounded-lg bg-amber-500/10 text-amber-600 dark:text-amber-400">
          <ShieldAlert className="h-5 w-5" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-medium">Compliance</p>
          <p className="truncate text-xs text-muted-foreground">
            {expiringTotal > 0
              ? `${expiringTotal} document(s) expired or expiring within 30 days`
              : "No urgent document expiries"}
          </p>
        </div>
        <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground transition group-hover:translate-x-0.5" />
      </Link>

      <Link
        href="/my-work"
        className="group flex items-center gap-4 rounded-xl border bg-card p-4 shadow-sm transition hover:border-primary/50 hover:shadow-md"
      >
        <span className="inline-flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 text-primary">
          <ListChecks className="h-5 w-5" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-sm font-medium">My work</p>
          <p className="truncate text-xs text-muted-foreground">
            {taskTotal > 0
              ? `${taskTotal} item(s) need attention`
              : "You're all caught up"}
          </p>
        </div>
        <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground transition group-hover:translate-x-0.5" />
      </Link>
    </div>
  );
}
