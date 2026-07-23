"use client";

import Link from "next/link";
import { PipelineTracker } from "@/components/workflow/pipeline-tracker";
import { getCandidateList, getPipelineCounts } from "@/lib/demo/demo-data";
import { getClearanceQueueCounts } from "@/lib/demo/clearances";
import { USE_MOCKS } from "@/lib/api/candidates-api";
import { AlertTriangle, ArrowRight, Users } from "lucide-react";
import { cn } from "@/lib/utils";
import { FlagBadge } from "@/components/workflow/status-pill";

export default function OverviewPage() {
  const counts = USE_MOCKS ? getPipelineCounts() : [];
  const candidates = USE_MOCKS ? getCandidateList() : [];
  const clearanceQueues = USE_MOCKS ? getClearanceQueueCounts() : [];
  const overdue = candidates.filter((c) => c.isOverdue);
  const total = candidates.length;
  const inPipeline = candidates.filter((c) => c.currentStageName !== "Commissions").length;

  return (
    <div className="flex flex-col gap-6 p-4 md:p-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Operations dashboard</h1>
        <p className="text-sm text-muted-foreground">
          Live pipeline snapshot{USE_MOCKS ? " from demo data" : ""}
        </p>
      </div>

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        {[
          { label: "Total candidates", value: total, icon: Users },
          { label: "Active pipeline", value: inPipeline },
          { label: "Overdue / stuck", value: overdue.length, danger: true },
          { label: "Stages tracked", value: counts.length || 7 },
        ].map((kpi) => (
          <div
            key={kpi.label}
            className={cn(
              "rounded-xl border bg-card p-4 shadow-sm",
              kpi.danger && kpi.value > 0 && "border-rose-200 bg-rose-50/40",
            )}
          >
            <p className="text-xs font-medium uppercase tracking-wide text-muted-foreground">{kpi.label}</p>
            <p className={cn("mt-2 text-3xl font-bold tabular-nums", kpi.danger && kpi.value > 0 && "text-rose-700")}>
              {kpi.value}
            </p>
          </div>
        ))}
      </div>

      <PipelineTracker />

      {USE_MOCKS && clearanceQueues.length > 0 && (
        <section>
          <div className="mb-3 flex items-center justify-between gap-2">
            <div>
              <h2 className="font-semibold">External services</h2>
              <p className="text-xs text-muted-foreground">
                Clearance queues across Musaned, Wafid, visa, insurance, COC, and tickets
              </p>
            </div>
            <FlagBadge tone="neutral">EasyEnjaz-style</FlagBadge>
          </div>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6">
            {clearanceQueues.map((q) => (
              <Link
                key={q.serviceId}
                href={q.href}
                className={cn(
                  "group rounded-xl border bg-card p-3 shadow-sm transition-colors hover:border-primary/40 hover:bg-accent/40",
                  q.blocked > 0 && "border-rose-200",
                )}
              >
                <div className="text-[11px] font-medium uppercase tracking-wide text-muted-foreground">
                  {q.easyenjazAlias}
                </div>
                <div className="mt-1 text-sm font-semibold group-hover:text-primary">{q.label}</div>
                <div className="mt-3 flex items-end justify-between gap-2">
                  <div>
                    <div className="text-2xl font-bold tabular-nums leading-none">{q.waiting}</div>
                    <div className="mt-0.5 text-[10px] text-muted-foreground">waiting</div>
                  </div>
                  {q.blocked > 0 ? (
                    <div className="text-right">
                      <div className="text-sm font-bold tabular-nums text-rose-700">{q.blocked}</div>
                      <div className="text-[10px] text-rose-600/80">blocked</div>
                    </div>
                  ) : (
                    <ArrowRight className="h-3.5 w-3.5 text-muted-foreground opacity-0 transition group-hover:opacity-100" />
                  )}
                </div>
              </Link>
            ))}
          </div>
        </section>
      )}

      <div className="grid gap-4 lg:grid-cols-2">
        <div className="rounded-xl border bg-card p-4 shadow-sm">
          <div className="mb-3 flex items-center justify-between">
            <h2 className="font-semibold">Stage load</h2>
            <Link href="/candidates" className="text-xs text-primary hover:underline inline-flex items-center gap-1">
              All candidates <ArrowRight className="h-3 w-3" />
            </Link>
          </div>
          <div className="space-y-2">
            {counts.map((s) => (
              <Link
                key={s.id}
                href={`/workflow/${s.slug}`}
                className="flex items-center gap-3 rounded-lg border px-3 py-2 hover:bg-muted/40 transition-colors"
              >
                <div className="w-28 text-sm font-medium">{s.name}</div>
                <div className="flex-1 h-2 rounded-full bg-muted overflow-hidden">
                  <div className="h-full bg-primary/80 rounded-full" style={{ width: `${Math.min(100, s.count * 10)}%` }} />
                </div>
                <div className="w-10 text-right text-sm font-bold tabular-nums">{s.count}</div>
              </Link>
            ))}
          </div>
        </div>

        <div className="rounded-xl border bg-card p-4 shadow-sm">
          <div className="mb-3 flex items-center gap-2">
            <AlertTriangle className="h-4 w-4 text-rose-600" />
            <h2 className="font-semibold">Needs attention</h2>
          </div>
          {overdue.length === 0 ? (
            <p className="text-sm text-muted-foreground py-8 text-center">No overdue candidates</p>
          ) : (
            <ul className="space-y-2">
              {overdue.slice(0, 8).map((c) => (
                <li key={c.id}>
                  <Link
                    href={`/candidates/${c.id}`}
                    className="flex items-center justify-between rounded-lg border border-rose-100 bg-rose-50/50 px-3 py-2 hover:bg-rose-50"
                  >
                    <div>
                      <div className="text-sm font-medium">{c.fullName}</div>
                      <div className="text-[11px] text-muted-foreground">
                        {c.currentStageName} · {c.daysInStage}d · {c.lastActionLabel}
                      </div>
                    </div>
                    <span className="text-xs font-bold text-rose-700">{c.passportNumber}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}
