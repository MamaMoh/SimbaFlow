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
