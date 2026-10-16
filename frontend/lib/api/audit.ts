import useSWR from "swr";

const fetcher = async <T>(url: string): Promise<T> => {
  const res = await fetch(url);
  const body = await res.json().catch(() => ({}));
  if (!res.ok || body?.isSuccess === false) {
    throw new Error(body?.error || `Request failed (${res.status})`);
  }
  return body.data as T;
};

/** "changes" is what people did; "views" is what they opened or printed. */
export type AuditTrailKind = "changes" | "views";

export type AuditTrailEntry = {
  id: string;
  at: string;
  userId?: string | null;
  userName: string;
  userEmail?: string | null;
  action: string;
  rawAction: string;
  entityType?: string | null;
  entityId?: string | null;
  entityLabel?: string | null;
  success: boolean;
  ipAddress?: string | null;
  userAgent?: string | null;
  durationMs?: number | null;
  /** What was asked for (changes only), as JSON. */
  details?: string | null;
};

export type AuditTrailOption = { value: string; label: string };

export type AuditTrailPage = {
  items: AuditTrailEntry[];
  total: number;
  page: number;
  pageSize: number;
  users: AuditTrailOption[];
  actions: AuditTrailOption[];
};

export type AuditTrailFilters = {
  kind: AuditTrailKind;
  from?: string;
  to?: string;
  userId?: string;
  action?: string;
  failedOnly?: boolean;
  page: number;
  pageSize?: number;
};

export function auditTrailUrl(f: AuditTrailFilters): string {
  const params = new URLSearchParams({ kind: f.kind, page: String(f.page) });
  if (f.pageSize) params.set("pageSize", String(f.pageSize));
  if (f.from) params.set("from", f.from);
  if (f.to) params.set("to", f.to);
  if (f.userId) params.set("userId", f.userId);
  if (f.action) params.set("action", f.action);
  if (f.failedOnly && f.kind === "changes") params.set("failedOnly", "true");
  return `/api/proxy/audit?${params.toString()}`;
}

export function useAuditTrail(filters: AuditTrailFilters, enabled = true) {
  return useSWR<AuditTrailPage>(enabled ? auditTrailUrl(filters) : null, fetcher, {
    revalidateOnFocus: false,
    keepPreviousData: true,
  });
}
