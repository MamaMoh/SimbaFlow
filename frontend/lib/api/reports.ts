import useSWR from "swr";

const fetcher = async <T>(url: string): Promise<T> => {
  const res = await fetch(url);
  const body = await res.json().catch(() => ({}));
  if (!res.ok || body?.isSuccess === false) {
    throw new Error(body?.error || `Request failed (${res.status})`);
  }
  return body.data as T;
};

export type ReportCatalogItem = {
  key: string;
  name: string;
  category: string;
  description: string;
};

export type ReportColumnType = "Text" | "Number" | "Money" | "Date" | "Percent";

export type ReportColumn = {
  key: string;
  label: string;
  type: ReportColumnType;
};

export type ReportTable = {
  key: string;
  title: string;
  subtitle?: string | null;
  columns: ReportColumn[];
  rows: Record<string, unknown>[];
  chartLabelKey?: string | null;
  chartValueKey?: string | null;
  generatedAtUtc?: string | null;
};

export function useReportCatalog(enabled = true) {
  return useSWR<ReportCatalogItem[]>(
    enabled ? "/api/proxy/reports" : null,
    fetcher,
    { revalidateOnFocus: false }
  );
}

export function useReport(key: string | null, enabled = true) {
  return useSWR<ReportTable>(
    enabled && key ? `/api/proxy/reports/${key}` : null,
    fetcher,
    { revalidateOnFocus: false }
  );
}

export function reportExportUrl(key: string, format: "excel" | "pdf") {
  return `/api/proxy/reports/${key}/export?format=${format}`;
}

export function formatCell(value: unknown, type: ReportColumnType): string {
  if (value === null || value === undefined || value === "") return "—";
  switch (type) {
    case "Number":
      return Number(value).toLocaleString("en-US", { maximumFractionDigits: 1 });
    case "Money":
      return Number(value).toLocaleString("en-US", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
      });
    case "Percent":
      return `${Number(value).toLocaleString("en-US", { maximumFractionDigits: 1 })}%`;
    case "Date":
      return String(value).slice(0, 10);
    default:
      return String(value);
  }
}

// ─── Scheduled report emails ─────────────────────────────────────────────

export type ReportScheduleFrequency = "daily" | "weekly" | "monthly";

export type ReportSchedule = {
  id: string;
  reportKey: string;
  reportName: string;
  format: "excel" | "pdf";
  frequency: ReportScheduleFrequency;
  hourLocal: number;
  dayOfWeek: number | null;
  dayOfMonth: number | null;
  recipients: string[];
  isActive: boolean;
  description: string;
  nextRunAt: string | null;
  lastRunAt: string | null;
  lastStatus: "Sent" | "Partial" | "Failed" | "Skipped" | null;
  lastError: string | null;
  createdBy: string | null;
  createdAt: string;
};

export type ReportScheduleInput = {
  reportKey: string;
  format: "excel" | "pdf";
  frequency: ReportScheduleFrequency;
  hourLocal: number;
  dayOfWeek: number | null;
  dayOfMonth: number | null;
  /** Addresses separated by commas, spaces or new lines. */
  recipients: string;
  isActive: boolean;
};

const SCHEDULES = "/api/proxy/report-schedules";

export function useReportSchedules(enabled = true) {
  return useSWR<ReportSchedule[]>(enabled ? SCHEDULES : null, fetcher, {
    revalidateOnFocus: false,
  });
}

async function send<T>(url: string, method: string, body?: unknown): Promise<T> {
  const res = await fetch(url, {
    method,
    headers: body ? { "Content-Type": "application/json" } : undefined,
    body: body ? JSON.stringify(body) : undefined,
    cache: "no-store",
  });
  if (res.status === 204) return undefined as T;
  const json = await res.json().catch(() => ({}));
  if (!res.ok || json?.isSuccess === false) {
    const first = Array.isArray(json?.errors) ? json.errors[0]?.errorMessage : null;
    throw new Error(first || json?.error || `Request failed (${res.status})`);
  }
  return json.data as T;
}

export const reportSchedulesApi = {
  create: (input: ReportScheduleInput) =>
    send<ReportSchedule>(SCHEDULES, "POST", input),
  update: (id: string, input: ReportScheduleInput) =>
    send<ReportSchedule>(`${SCHEDULES}/${id}`, "PUT", { id, ...input }),
  remove: (id: string) => send<void>(`${SCHEDULES}/${id}`, "DELETE"),
  sendNow: (id: string) =>
    send<ReportSchedule>(`${SCHEDULES}/${id}/send`, "POST"),
};
