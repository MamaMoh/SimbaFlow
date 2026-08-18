/**
 * Canonical status → tone system. THE single source of truth for status colors
 * across the app. Never hard-code status colors in a page; map the status to a
 * tone here and render it with <StatusBadge />.
 */

export type StatusTone =
  | "success"
  | "warning"
  | "danger"
  | "info"
  | "progress"
  | "neutral";

/** Soft chip classes per tone, theme-aware (light + dark). */
export const TONE_CLASSES: Record<StatusTone, string> = {
  success:
    "border-emerald-200 bg-emerald-50 text-emerald-800 dark:border-emerald-900/60 dark:bg-emerald-950/40 dark:text-emerald-300",
  warning:
    "border-amber-200 bg-amber-50 text-amber-900 dark:border-amber-900/60 dark:bg-amber-950/40 dark:text-amber-300",
  danger:
    "border-red-200 bg-red-50 text-red-800 dark:border-red-900/60 dark:bg-red-950/40 dark:text-red-300",
  info:
    "border-sky-200 bg-sky-50 text-sky-800 dark:border-sky-900/60 dark:bg-sky-950/40 dark:text-sky-300",
  progress:
    "border-violet-200 bg-violet-50 text-violet-800 dark:border-violet-900/60 dark:bg-violet-950/40 dark:text-violet-300",
  neutral:
    "border-border bg-muted text-muted-foreground",
};

/** Solid dot color per tone (for the leading indicator). */
export const TONE_DOT: Record<StatusTone, string> = {
  success: "bg-emerald-500",
  warning: "bg-amber-500",
  danger: "bg-red-500",
  info: "bg-sky-500",
  progress: "bg-violet-500",
  neutral: "bg-muted-foreground/50",
};

const SUCCESS_WORDS = [
  "fit", "issued", "book done", "bookdone", "departed", "arrived", "settled",
  "paid", "completed", "verified", "approved", "done", "success", "active",
];
const DANGER_WORDS = [
  "unfit", "rejected", "expired", "runaway", "returned", "not departed",
  "notdeparted", "disputed", "cancelled", "canceled", "failed", "overdue",
  "suspended", "inactive", "deactivated", "blocked",
];
const INFO_WORDS = [
  "booked", "submitted", "notified", "ready", "pending", "new", "open",
  "uploaded", "in progress", "inprogress", "processing",
];
const WARNING_WORDS = ["partial", "warning", "review", "hold", "waiting", "resubmission"];

/**
 * Generic status → tone for free-text status values (candidate tracks,
 * medical/tasheer/visa, etc.). Domain helpers below override where a word
 * means different things (e.g. "Open").
 */
export function statusTone(value?: string | null): StatusTone {
  if (!value) return "neutral";
  const v = value.toLowerCase().trim();
  if (SUCCESS_WORDS.some((x) => v.includes(x))) return "success";
  if (DANGER_WORDS.some((x) => v.includes(x))) return "danger";
  if (WARNING_WORDS.some((x) => v.includes(x))) return "warning";
  if (INFO_WORDS.some((x) => v.includes(x))) return "info";
  return "neutral";
}

/** Commission status. */
export function commissionTone(status?: string | null): StatusTone {
  switch ((status ?? "").toLowerCase()) {
    case "open": return "info";
    case "partial": return "warning";
    case "settled": return "success";
    case "disputed": return "danger";
    default: return "neutral";
  }
}

/** Exception-case status (Open means "needs attention" → danger). */
export function exceptionTone(status?: string | null): StatusTone {
  switch ((status ?? "").toLowerCase()) {
    case "open": return "danger";
    case "investigating":
    case "investigation": return "warning";
    case "resolved":
    case "closed": return "neutral";
    default: return "neutral";
  }
}

/** Tenant subscription status. */
export function tenantTone(status?: string | null): StatusTone {
  switch ((status ?? "").toLowerCase()) {
    case "active": return "success";
    case "trial": return "info";
    case "suspended":
    case "cancelled":
    case "canceled": return "danger";
    default: return "neutral";
  }
}

/** Remaining-days-to-departure → tone + label. */
export function remainingDays(days?: number | null): { tone: StatusTone; label: string } {
  if (days == null) return { tone: "neutral", label: "—" };
  if (days < 0) return { tone: "danger", label: `${Math.abs(days)}d overdue` };
  if (days <= 3) return { tone: "warning", label: `${days}d left` };
  return { tone: "info", label: `${days}d` };
}

/**
 * Case age → tone.
 *
 * The number alone doesn't help a supervisor scanning 200 rows; the colour is the signal. Two
 * weeks is treated as healthy, a month as slipping, beyond that as needing intervention. These
 * thresholds are deliberately in one place so every board agrees on what "late" means.
 */
export const AGE_WARNING_DAYS = 14;
export const AGE_CRITICAL_DAYS = 30;

export function ageTone(days?: number | null): StatusTone {
  if (days == null) return "neutral";
  if (days >= AGE_CRITICAL_DAYS) return "danger";
  if (days >= AGE_WARNING_DAYS) return "warning";
  return "success";
}

/** Human labels for the workflow tracks stored in candidate status values. */
export const TRACK_LABELS: Record<string, string> = {
  medical: "Medical",
  tasheer: "Tasheer",
  visa: "Visa",
  insurance: "Insurance",
  milestone: "LMIS",
  ticket_status: "Ticket",
  notification_status: "Notify",
  departure_status: "Departure",
  arrival: "Arrival",
  status: "Status",
};

export function isPrimaryTrack(key: string): boolean {
  return key.toLowerCase() in TRACK_LABELS;
}
