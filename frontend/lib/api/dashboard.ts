import useSWR from "swr";

type ApiResult<T> = {
  isSuccess?: boolean;
  data?: T;
  error?: string;
};

export type PipelineFunnelStage = {
  stageId: string;
  stageName: string;
  sortOrder: number;
  isFinalStage: boolean;
  count: number;
};

export type PipelineFunnel = {
  stages: PipelineFunnelStage[];
  totalCandidates: number;
  unassignedCount: number;
};

const fetcher = async <T>(url: string): Promise<T> => {
  const res = await fetch(url);
  const body = await res.json().catch(() => ({}));
  if (!res.ok || body?.isSuccess === false) {
    throw new Error(body?.error || `Request failed (${res.status})`);
  }
  return body.data as T;
};

export function usePipelineFunnel(enabled = true) {
  return useSWR<PipelineFunnel>(
    enabled ? "/api/proxy/dashboard/pipeline-funnel" : null,
    fetcher,
    { revalidateOnFocus: false }
  );
}

export type DashboardMetrics = {
  activeCandidates: number;
  newThisMonth: number;
  commissionsOwed: number;
  openExceptions: number;
  overdueCandidates: number;
};

export function useDashboardMetrics(enabled = true) {
  return useSWR<DashboardMetrics>(
    enabled ? "/api/proxy/dashboard/metrics" : null,
    fetcher,
    { revalidateOnFocus: false }
  );
}

export type TrendPoint = {
  month: string;
  registered: number;
  commissions: number;
  exceptions: number;
};

export function useDashboardTrends(enabled = true) {
  return useSWR<TrendPoint[]>(
    enabled ? "/api/proxy/dashboard/trends" : null,
    fetcher,
    { revalidateOnFocus: false }
  );
}
