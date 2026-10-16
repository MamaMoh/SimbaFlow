"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import {
  AlertTriangle,
  ArrowRight,
  CheckCircle2,
  Clock,
  Hourglass,
  Loader2,
  Search,
} from "lucide-react";
import { AccessDenied, LoadError } from "@/components/ui/page-alert";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import {
  MY_WORK_PERMISSIONS,
  useMyWorkQueues,
  type WorkQueue,
  type WorkQueueRow,
} from "@/lib/api/work-queues";
import { PageHeader } from "@/components/ui/page-header";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { cn } from "@/lib/utils";

const ANY = "__any";
const TAB_KEY = "simbaflow:my-work-tab";

function readTab(): string | null {
  try {
    return window.localStorage.getItem(TAB_KEY);
  } catch {
    return null;
  }
}

function saveTab(key: string) {
  try {
    window.localStorage.setItem(TAB_KEY, key);
  } catch {
    // Private windows and blocked storage: the page still works, it just opens on the first tab.
  }
}

function days(n: number) {
  return n === 1 ? "1 day" : `${n} days`;
}

function QueueRow({ row }: { row: WorkQueueRow }) {
  return (
    <Link
      href={row.href}
      className="group flex items-center gap-3 border-b px-4 py-3 text-sm transition last:border-0 hover:bg-muted/40"
    >
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
          <span className="truncate font-medium">{row.fullName}</span>
          <span className="text-xs text-muted-foreground tabular-nums">
            {row.passportNumber}
          </span>
          {row.warning && (
            <span className="inline-flex items-center gap-1 rounded-full bg-rose-500/15 px-2 py-0.5 text-[11px] font-medium text-rose-700 dark:text-rose-400">
              <AlertTriangle className="h-3 w-3" />
              {row.warning}
            </span>
          )}
        </div>
        <p
          className={cn(
            "mt-0.5 truncate",
            row.needsAction ? "text-foreground" : "text-muted-foreground"
          )}
        >
          {row.nextStep}
        </p>
        {(row.partnerName || row.officeName) && (
          <p className="truncate text-xs text-muted-foreground">
            {[row.partnerName, row.officeName].filter(Boolean).join(" · ")}
          </p>
        )}
      </div>
      <span
        className={cn(
          "shrink-0 text-xs tabular-nums",
          row.overdue
            ? "font-semibold text-rose-600 dark:text-rose-400"
            : "text-muted-foreground"
        )}
        title={row.overdue ? "In this stage longer than 14 days" : undefined}
      >
        {days(row.daysInStage)}
      </span>
      <ArrowRight className="h-4 w-4 shrink-0 text-muted-foreground opacity-0 transition group-hover:opacity-100" />
    </Link>
  );
}

function Section({
  title,
  icon: Icon,
  rows,
  empty,
}: {
  title: string;
  icon: React.ElementType;
  rows: WorkQueueRow[];
  empty: React.ReactNode;
}) {
  return (
    <div className="rounded-lg border bg-card shadow-sm">
      <div className="flex items-center gap-2 border-b px-4 py-3">
        <Icon className="h-4 w-4 text-muted-foreground" />
        <h2 className="text-sm font-semibold">{title}</h2>
        <span className="text-xs text-muted-foreground tabular-nums">{rows.length}</span>
      </div>
      {rows.length === 0 ? (
        <div className="flex items-center justify-center gap-2 p-8 text-sm text-muted-foreground">
          {empty}
        </div>
      ) : (
        <div>
          {rows.map((row) => (
            <QueueRow key={row.id} row={row} />
          ))}
        </div>
      )}
    </div>
  );
}

function QueueTabs({
  queues,
  active,
  onSelect,
}: {
  queues: WorkQueue[];
  active: string;
  onSelect: (key: string) => void;
}) {
  return (
    <div
      role="tablist"
      className="flex gap-1 overflow-x-auto rounded-lg border bg-muted/40 p-1"
    >
      {queues.map((q) => {
        const selected = q.key === active;
        return (
          <button
            key={q.key}
            role="tab"
            aria-selected={selected}
            onClick={() => onSelect(q.key)}
            className={cn(
              "inline-flex shrink-0 items-center gap-2 rounded-md px-3 py-1.5 text-sm font-medium transition",
              selected
                ? "bg-background text-foreground shadow-sm"
                : "text-muted-foreground hover:text-foreground"
            )}
          >
            {q.label}
            <span
              className={cn(
                "inline-flex h-5 min-w-5 items-center justify-center rounded-full px-1.5 text-[11px] font-semibold tabular-nums",
                q.overdueCount > 0
                  ? "bg-rose-500/15 text-rose-700 dark:text-rose-400"
                  : q.needsActionCount > 0
                    ? "bg-primary/15 text-primary"
                    : "bg-muted text-muted-foreground"
              )}
              title={`${q.needsActionCount} to do, ${q.waitingCount} waiting`}
            >
              {q.needsActionCount}
            </span>
          </button>
        );
      })}
    </div>
  );
}

export default function MyWorkPage() {
  const { hasPermission, isLoading: permsLoading } = usePermissions();
  const canRead = MY_WORK_PERMISSIONS.some((p) => hasPermission(p));

  const [typed, setTyped] = useState("");
  const [search, setSearch] = useState("");
  const [partner, setPartner] = useState(ANY);
  const [office, setOffice] = useState(ANY);
  const [tab, setTab] = useState<string | null>(null);

  useEffect(() => {
    const timer = setTimeout(() => setSearch(typed.trim()), 300);
    return () => clearTimeout(timer);
  }, [typed]);

  useEffect(() => {
    setTab(readTab());
  }, []);

  const { data, error, isLoading, mutate } = useMyWorkQueues(
    {
      search: search || undefined,
      partner: partner === ANY ? undefined : partner,
      office: office === ANY ? undefined : office,
    },
    !permsLoading && canRead
  );

  const queues = useMemo(() => data?.queues ?? [], [data]);
  const active = queues.find((q) => q.key === tab) ?? queues[0];

  const selectTab = (key: string) => {
    setTab(key);
    saveTab(key);
  };

  if (permsLoading) {
    return (
      <div className="flex items-center justify-center p-12 text-muted-foreground">
        <Loader2 className="mr-2 h-5 w-5 animate-spin" />
        Loading…
      </div>
    );
  }

  if (!canRead) return <AccessDenied resource="your work queues" />;

  const toDo = active?.items.filter((r) => r.needsAction) ?? [];
  const waiting = active?.items.filter((r) => !r.needsAction) ?? [];
  const filtered = !!search || partner !== ANY || office !== ANY;

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="My work" />

      {error && <LoadError message={error.message} onRetry={() => mutate()} />}

      <div className="flex flex-wrap items-end gap-3">
        <div className="relative w-full sm:w-72">
          <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="h-8 pl-8"
            placeholder="Name, passport or labour ID"
            value={typed}
            onChange={(e) => setTyped(e.target.value)}
          />
        </div>
        {(data?.partners.length ?? 0) > 0 && (
          <Select value={partner} onValueChange={setPartner}>
            <SelectTrigger className="h-8 w-48">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>All partners</SelectItem>
              {data!.partners.map((o) => (
                <SelectItem key={o.value} value={o.value}>
                  {o.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
        {(data?.offices.length ?? 0) > 0 && (
          <Select value={office} onValueChange={setOffice}>
            <SelectTrigger className="h-8 w-44">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value={ANY}>All offices</SelectItem>
              {data!.offices.map((o) => (
                <SelectItem key={o.value} value={o.value}>
                  {o.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        )}
        {isLoading && data && (
          <Loader2 className="mb-2 h-4 w-4 animate-spin text-muted-foreground" />
        )}
      </div>

      {isLoading && !data ? (
        <div className="flex items-center justify-center p-10 text-sm text-muted-foreground">
          <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          Loading your queues…
        </div>
      ) : queues.length === 0 ? (
        <div className="rounded-lg border bg-card p-10 text-center text-sm text-muted-foreground">
          Your role has no workflow boards yet. Ask an administrator to add one to your role.
        </div>
      ) : (
        <>
          {queues.length > 1 && (
            <QueueTabs queues={queues} active={active!.key} onSelect={selectTab} />
          )}

          <div className="flex items-center justify-between text-sm">
            <p className="text-muted-foreground">
              {active!.needsActionCount} to do · {active!.waitingCount} waiting
              {active!.overdueCount > 0 && (
                <span className="text-rose-600 dark:text-rose-400">
                  {" "}
                  · {active!.overdueCount} over 14 days
                </span>
              )}
            </p>
            <Link
              href={active!.boardHref}
              className="inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline"
            >
              Open {active!.label} board
              <ArrowRight className="h-3.5 w-3.5" />
            </Link>
          </div>

          <Section
            title="Needs action"
            icon={Clock}
            rows={toDo}
            empty={
              <>
                <CheckCircle2 className="h-5 w-5 text-emerald-500" />
                {filtered ? "Nothing matches these filters." : "You're all caught up here."}
              </>
            }
          />
          <Section
            title="Waiting on others"
            icon={Hourglass}
            rows={waiting}
            empty="Nobody is waiting on someone else."
          />
        </>
      )}
    </div>
  );
}
