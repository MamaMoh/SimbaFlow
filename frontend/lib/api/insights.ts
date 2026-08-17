import useSWR from "swr";

const fetcher = async <T>(url: string): Promise<T> => {
  const res = await fetch(url);
  const body = await res.json().catch(() => ({}));
  if (!res.ok || body?.isSuccess === false) {
    throw new Error(body?.error || `Request failed (${res.status})`);
  }
  return body.data as T;
};

// ─── Compliance ───────────────────────────────────────────────
export type ComplianceAlert = {
  candidateId: string;
  candidateName: string;
  passport?: string | null;
  category: string;
  detail: string;
  expiryDate?: string | null;
  daysRemaining?: number | null;
  bucket: "expired" | "within30" | "within90";
};

export type ComplianceAlerts = {
  expiredCount: number;
  within30Count: number;
  within90Count: number;
  alerts: ComplianceAlert[];
};

export function useComplianceAlerts(enabled = true) {
  return useSWR<ComplianceAlerts>(
    enabled ? "/api/proxy/compliance/alerts" : null,
    fetcher,
    { revalidateOnFocus: false }
  );
}

// ─── My Tasks ─────────────────────────────────────────────────
export type MyTaskItem = {
  type: "exception" | "passport" | "overdue" | string;
  title: string;
  subtitle: string;
  severity: "high" | "medium" | "low" | string;
  candidateId?: string | null;
  href?: string | null;
};

export type MyTasks = {
  overdueCount: number;
  expiringSoonCount: number;
  openExceptionCount: number;
  items: MyTaskItem[];
};

export function useMyTasks(enabled = true) {
  return useSWR<MyTasks>(
    enabled ? "/api/proxy/tasks/mine" : null,
    fetcher,
    { revalidateOnFocus: false }
  );
}
