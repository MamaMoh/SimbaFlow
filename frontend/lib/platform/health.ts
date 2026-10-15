/** How long without anyone signing in before an agency is called idle. */
export const IDLE_DAYS = 14;

export type AgencyHealth = {
  status: number;
  activeUsers?: number;
  maxUsers?: number;
  lastSignInAt?: string | null;
  licenseExpiresAt?: string | null;
  openSupportTickets?: number;
};

/** Days since a moment, or null when it never happened. */
export function daysSince(iso: string | null | undefined, now = new Date()): number | null {
  if (!iso) return null;
  const then = new Date(iso);
  if (Number.isNaN(then.getTime())) return null;
  return Math.floor((now.getTime() - then.getTime()) / 86_400_000);
}

/**
 * Plain-language reasons an active agency may need the platform team's help, most urgent first.
 * Empty when nothing stands out. Suspended and offboarded agencies are judged elsewhere.
 */
export function attentionReasons(a: AgencyHealth, now = new Date()): string[] {
  if (a.status !== 0) return [];
  const reasons: string[] = [];
  if ((a.openSupportTickets ?? 0) > 0) {
    const n = a.openSupportTickets!;
    reasons.push(`${n} open help request${n === 1 ? "" : "s"}`);
  }
  const idle = daysSince(a.lastSignInAt, now);
  if (idle === null) reasons.push("Nobody has signed in yet");
  else if (idle >= IDLE_DAYS) reasons.push(`No sign-in for ${idle} days`);
  if (a.maxUsers && (a.activeUsers ?? 0) >= a.maxUsers) reasons.push("At its user limit");
  if (a.licenseExpiresAt) {
    const left = -(daysSince(a.licenseExpiresAt, now) ?? 0);
    if (left < 0) reasons.push("Licence expired");
    else if (left <= 30) reasons.push(`Licence expires in ${left} days`);
  }
  return reasons;
}

/** "today", "3 days ago", "never". */
export function ago(iso: string | null | undefined, now = new Date()): string {
  const d = daysSince(iso, now);
  if (d === null) return "never";
  if (d <= 0) return "today";
  if (d === 1) return "yesterday";
  return `${d} days ago`;
}
