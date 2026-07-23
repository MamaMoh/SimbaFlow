"use client";

import type { ReactNode } from "react";
import { cn } from "@/lib/utils";
import { AlertTriangle, Clock3 } from "lucide-react";

type Tone = "success" | "info" | "warning" | "danger" | "neutral";

const TONE_STYLES: Record<Tone, { pill: string; dot: string }> = {
  success: {
    pill: "bg-emerald-100/80 text-emerald-900 ring-1 ring-inset ring-emerald-600/15",
    dot: "bg-emerald-500",
  },
  info: {
    pill: "bg-sky-100/80 text-sky-900 ring-1 ring-inset ring-sky-600/15",
    dot: "bg-sky-500",
  },
  warning: {
    pill: "bg-amber-100/80 text-amber-950 ring-1 ring-inset ring-amber-600/20",
    dot: "bg-amber-500",
  },
  danger: {
    pill: "bg-rose-100/80 text-rose-900 ring-1 ring-inset ring-rose-600/15",
    dot: "bg-rose-500",
  },
  neutral: {
    pill: "bg-slate-100/90 text-slate-700 ring-1 ring-inset ring-slate-500/10",
    dot: "bg-slate-400",
  },
};

const STATUS_TONE: Record<string, Tone> = {
  Fit: "success",
  Done: "success",
  Issued: "success",
  Paid: "success",
  Booked: "info",
  Submitted: "info",
  Notified: "info",
  OnDuty: "success",
  Depart: "success",
  Linked: "success",
  Approved: "success",
  "Not started": "neutral",
  OnProgress: "warning",
  Ready: "warning",
  Requested: "warning",
  Verified: "warning",
  Checked: "warning",
  PaymentVerified: "warning",
  Unfit: "danger",
  Expired: "danger",
  Rejected: "danger",
  Unpaid: "danger",
  NotBooked: "danger",
  Returned: "danger",
  Runaway: "danger",
  NotDepart: "warning",
};

const STAGE_TONE: Record<string, Tone> = {
  Submitted: "info",
  "Pre-Medical": "warning",
  Medical: "warning",
  "MOL / COC": "info",
  Visa: "info",
  Ticketing: "info",
  Deployment: "success",
  Deployed: "success",
  Returned: "danger",
  Intake: "neutral",
};

const LABEL_SHORT: Record<string, string> = {
  Medical: "Med",
  PreMedical: "Pre-Med",
  Pre_Medical: "Pre-Med",
  Visa: "Visa",
  Ticket: "Ticket",
  Ticketing: "Ticket",
  Mol: "MOL",
  MOL: "MOL",
  COC: "COC",
  Payment: "Pay",
  Embassy: "Emb",
  Labour: "Labour",
  Contract: "Contract",
};

function humanize(value: string) {
  return value
    .replace(/[_-]+/g, " ")
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/\s+/g, " ")
    .trim();
}

function shortLabel(label: string) {
  if (LABEL_SHORT[label]) return LABEL_SHORT[label];
  const h = humanize(label);
  return h.length > 10 ? h.slice(0, 9) + "…" : h;
}

function resolveTone(value: string): Tone {
  return STATUS_TONE[value] ?? STAGE_TONE[value] ?? "neutral";
}

export function StatusPill({
  label,
  value,
  sinceDays,
  size = "sm",
  showDot = true,
  className,
}: {
  label?: string;
  value?: string | null;
  sinceDays?: number;
  size?: "sm" | "md";
  showDot?: boolean;
  className?: string;
}) {
  const empty = !value;
  const tone = empty ? "neutral" : resolveTone(value);
  const styles = TONE_STYLES[tone];
  const display = empty ? "—" : humanize(value);
  const title =
    [
      label ? humanize(label) : null,
      empty ? null : display,
      sinceDays != null && sinceDays > 0 ? `${sinceDays}d on step` : null,
    ]
      .filter(Boolean)
      .join(" · ") || undefined;

  return (
    <span className={cn("inline-flex max-w-full items-center gap-1.5", className)} title={title}>
      {label ? (
        <span className="shrink-0 text-[10px] font-medium uppercase tracking-wider text-muted-foreground">
          {shortLabel(label)}
        </span>
      ) : null}
      <span
        className={cn(
          "inline-flex max-w-full items-center gap-1.5 rounded-full font-semibold",
          size === "sm" ? "px-2 py-0.5 text-[11px] leading-4" : "px-2.5 py-1 text-xs leading-4",
          styles.pill,
        )}
      >
        {showDot && !empty ? (
          <span className={cn("size-1.5 shrink-0 rounded-full", styles.dot)} aria-hidden />
        ) : null}
        <span className="truncate">{display}</span>
      </span>
    </span>
  );
}

export function StatusTrackGroup({
  items,
  max = 2,
  className,
}: {
  items: { key: string; label?: string; value?: string | null; sinceDays?: number }[];
  max?: number;
  className?: string;
}) {
  if (!items.length) {
    return <span className="text-xs text-muted-foreground">—</span>;
  }

  const visible = items.slice(0, max);
  const hidden = items.slice(max);
  const overflowTitle = hidden
    .map((i) => `${i.label ? humanize(i.label) + ": " : ""}${i.value ? humanize(i.value) : "—"}`)
    .join("\n");

  return (
    <div className={cn("flex max-w-[300px] flex-wrap items-center gap-x-2.5 gap-y-1", className)}>
      {visible.map((item) => (
        <StatusPill
          key={item.key}
          label={item.label}
          value={item.value}
          sinceDays={item.sinceDays}
          size="sm"
        />
      ))}
      {hidden.length > 0 ? (
        <span
          className="inline-flex items-center rounded-full bg-muted/80 px-2 py-0.5 text-[10px] font-semibold tabular-nums text-muted-foreground ring-1 ring-inset ring-border"
          title={overflowTitle}
        >
          +{hidden.length}
        </span>
      ) : null}
    </div>
  );
}

export function FlagBadge({
  tone = "neutral",
  children,
  className,
}: {
  tone?: "danger" | "info" | "warning" | "neutral";
  children: ReactNode;
  className?: string;
}) {
  const styles: Record<string, string> = {
    danger: "bg-rose-50 text-rose-700 ring-rose-600/15",
    info: "bg-violet-50 text-violet-700 ring-violet-600/15",
    warning: "bg-amber-50 text-amber-800 ring-amber-600/20",
    neutral: "bg-muted text-muted-foreground ring-border",
  };

  return (
    <span
      className={cn(
        "inline-flex items-center gap-0.5 rounded-full px-1.5 py-0.5 text-[10px] font-semibold ring-1 ring-inset",
        styles[tone],
        className,
      )}
    >
      {children}
    </span>
  );
}

export function TimingChip({
  days,
  overdue,
  className,
}: {
  days: number;
  overdue?: boolean;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-bold tabular-nums ring-1 ring-inset",
        overdue
          ? "bg-rose-50 text-rose-700 ring-rose-600/15"
          : "bg-slate-100/90 text-slate-700 ring-slate-500/10",
        className,
      )}
    >
      <Clock3 className="h-3 w-3 opacity-70" />
      {days}d
      {overdue ? <AlertTriangle className="h-3 w-3" /> : null}
    </span>
  );
}
