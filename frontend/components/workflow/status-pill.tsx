"use client";

import { cn } from "@/lib/utils";

const STATUS_STYLES: Record<string, string> = {
  Fit: "bg-emerald-50 text-emerald-800 border-emerald-200",
  Done: "bg-emerald-50 text-emerald-800 border-emerald-200",
  Issued: "bg-emerald-50 text-emerald-800 border-emerald-200",
  Paid: "bg-emerald-50 text-emerald-800 border-emerald-200",
  Booked: "bg-sky-50 text-sky-800 border-sky-200",
  Submitted: "bg-sky-50 text-sky-800 border-sky-200",
  Notified: "bg-sky-50 text-sky-800 border-sky-200",
  OnDuty: "bg-emerald-50 text-emerald-800 border-emerald-200",
  OnProgress: "bg-amber-50 text-amber-900 border-amber-200",
  Ready: "bg-amber-50 text-amber-900 border-amber-200",
  Requested: "bg-amber-50 text-amber-900 border-amber-200",
  Verified: "bg-amber-50 text-amber-900 border-amber-200",
  Checked: "bg-amber-50 text-amber-900 border-amber-200",
  PaymentVerified: "bg-amber-50 text-amber-900 border-amber-200",
  Unfit: "bg-rose-50 text-rose-800 border-rose-200",
  Expired: "bg-rose-50 text-rose-800 border-rose-200",
  Rejected: "bg-rose-50 text-rose-800 border-rose-200",
  Unpaid: "bg-rose-50 text-rose-800 border-rose-200",
  NotBooked: "bg-rose-50 text-rose-800 border-rose-200",
  Returned: "bg-rose-50 text-rose-800 border-rose-200",
  Runaway: "bg-rose-50 text-rose-800 border-rose-200",
  NotDepart: "bg-amber-50 text-amber-900 border-amber-200",
  Depart: "bg-emerald-50 text-emerald-800 border-emerald-200",
};

export function StatusPill({
  label,
  value,
  sinceDays,
  className,
}: {
  label?: string;
  value?: string | null;
  sinceDays?: number;
  className?: string;
}) {
  if (!value) {
    return (
      <span className={cn("inline-flex items-center rounded border px-2 py-0.5 text-[11px] font-medium text-muted-foreground bg-muted/40", className)}>
        {label ? `${label}: ` : ""}—
      </span>
    );
  }
  const style = STATUS_STYLES[value] ?? "bg-slate-50 text-slate-700 border-slate-200";
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1 rounded border px-2 py-0.5 text-[11px] font-semibold tracking-wide",
        style,
        className,
      )}
      title={sinceDays != null ? `${sinceDays}d on this step` : undefined}
    >
      {label ? <span className="opacity-70 font-medium">{label}</span> : null}
      {value.replace(/([a-z])([A-Z])/g, "$1 $2")}
      {sinceDays != null && sinceDays > 0 ? (
        <span className="opacity-60 font-normal">· {sinceDays}d</span>
      ) : null}
    </span>
  );
}
