import useSWR from "swr";
import { useBoardRealtime } from "@/lib/api/embassy";

type ApiResult<T> = {
  isSuccess?: boolean;
  data?: T;
  error?: string;
  statusCode?: number;
};

export type LmisBoardRow = {
  id: string;
  fullName: string;
  passportNumber: string;
  labourId?: string | null;
  partnerName?: string | null;
  statusValues: Record<string, string>;
  insurance?: string | null;
  milestone?: string | null;
  daysInStage: number;
  daysSinceRegistered: number;
  isMirror: boolean;
  source: string;
  registeredAt: string;
};

type PaginatedBoard = {
  items: LmisBoardRow[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};

const fetcher = async <T>(url: string): Promise<T> => {
  const res = await fetch(url);
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new Error(body?.error || `Request failed (${res.status})`);
  }
  return res.json();
};

async function postJson(url: string, body?: unknown): Promise<void> {
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const data = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(data?.error || `Request failed (${res.status})`);
}

export function useLmisBoard(params?: {
  page?: number;
  pageSize?: number;
  search?: string;
  insurance?: string;
  milestone?: string;
  mirrorOnly?: boolean;
}) {
  const qs = new URLSearchParams({
    page: String(params?.page ?? 1),
    pageSize: String(params?.pageSize ?? 50),
  });
  if (params?.search) qs.set("search", params.search);
  if (params?.insurance) qs.set("insurance", params.insurance);
  if (params?.milestone) qs.set("milestone", params.milestone);
  if (params?.mirrorOnly) qs.set("mirrorOnly", "true");

  const key = `/api/proxy/lmis/board?${qs.toString()}`;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<PaginatedBoard>>(key, fetcher, {
    revalidateOnFocus: false,
  });
  useBoardRealtime(mutate);

  return {
    candidates: data?.data?.items ?? [],
    totalCount: data?.data?.totalCount ?? 0,
    isLoading,
    error,
    mutate,
  };
}

export const lmisApi = {
  recordInsurancePaid: (id: string, paymentDate?: string, notes?: string) =>
    postJson(`/api/proxy/lmis/candidates/${id}/insurance/paid`, { paymentDate, notes }),
  advanceMilestone: (id: string, milestone: string, notes?: string) =>
    postJson(`/api/proxy/lmis/candidates/${id}/milestone`, { milestone, notes }),
};

/** Next allowed LMIS milestone given current milestone track value. */
export function nextLmisMilestone(current?: string | null): string | null {
  const c = (current ?? "").trim();
  if (!c) return "Uploaded";
  if (c === "Uploaded") return "Check Verified";
  if (c === "Check Verified") return "Issued";
  return null;
}
