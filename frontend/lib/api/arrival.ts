import useSWR from "swr";
import { useTravelBoardRealtime } from "@/lib/api/travel";

type ApiResult<T> = {
  isSuccess?: boolean;
  data?: T;
  error?: string;
  statusCode?: number;
};

export type ArrivalBoardRow = {
  id: string;
  fullName: string;
  passportNumber: string;
  labourId?: string | null;
  partnerName?: string | null;
  countryOfTravel?: string | null;
  statusValues: Record<string, string>;
  daysInStage: number;
  daysSinceRegistered: number;
  commissionLinked: boolean;
  hasOpenException: boolean;
  registeredAt: string;
};

type PaginatedBoard = {
  /** Stage this board represents; scopes workflow buttons to it. */
  stageId: string;
  items: ArrivalBoardRow[];
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

export function useArrivalBoard(params?: {
  page?: number;
  pageSize?: number;
  search?: string;
}) {
  const qs = new URLSearchParams({
    page: String(params?.page ?? 1),
    pageSize: String(params?.pageSize ?? 50),
  });
  if (params?.search) qs.set("search", params.search);

  const key = `/api/proxy/arrival/board?${qs.toString()}`;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<PaginatedBoard>>(key, fetcher, {
    revalidateOnFocus: false,
  });
  useTravelBoardRealtime(mutate);

  return {
    stageId: data?.data?.stageId,
    candidates: data?.data?.items ?? [],
    totalCount: data?.data?.totalCount ?? 0,
    isLoading,
    error,
    mutate,
  };
}

export const arrivalApi = {
  confirmArrived: (id: string, notes?: string) =>
    postJson(`/api/proxy/arrival/candidates/${id}/arrived`, { notes }),
  flagException: (id: string, type: "Returned" | "Runaway", notes?: string) =>
    postJson(`/api/proxy/arrival/candidates/${id}/flag-exception`, { type, notes }),
  addToCommission: (id: string, notes?: string) =>
    postJson(`/api/proxy/arrival/candidates/${id}/add-to-commission`, { notes }),
};
