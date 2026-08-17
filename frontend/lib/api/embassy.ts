import useSWR, { useSWRConfig } from "swr";
import { useEffect } from "react";
import { useSignalR } from "@/lib/signalr/signalr-provider";

type ApiResult<T> = {
  isSuccess?: boolean;
  data?: T;
  error?: string;
  statusCode?: number;
};

export type EmbassyBoardRow = {
  id: string;
  fullName: string;
  passportNumber: string;
  labourId?: string | null;
  officeName?: string | null;
  countryOfTravel?: string | null;
  statusValues: Record<string, string>;
  daysInStage: number;
  isMirror: boolean;
  registeredAt: string;
};

type PaginatedBoard = {
  items: EmbassyBoardRow[];
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

function boardKey(
  path: string,
  params?: { page?: number; pageSize?: number; search?: string; officeId?: string }
) {
  const qs = new URLSearchParams({
    page: String(params?.page ?? 1),
    pageSize: String(params?.pageSize ?? 50),
  });
  if (params?.search) qs.set("search", params.search);
  if (params?.officeId) qs.set("officeId", params.officeId);
  return `/api/proxy/${path}?${qs.toString()}`;
}

export function useEmbassyBoard(params?: {
  page?: number;
  pageSize?: number;
  search?: string;
  officeId?: string;
}) {
  const key = boardKey("embassy/board", params);
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

export function useCaseExecutiveBoard(params?: {
  page?: number;
  pageSize?: number;
  search?: string;
  officeId?: string;
}) {
  const key = boardKey("embassy/case-executive/board", params);
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

/** Invalidate embassy/lmis board keys when SignalR fires candidate updates. */
export function useBoardRealtime(mutate: () => void) {
  const { subscribe, unsubscribe } = useSignalR();
  const { mutate: globalMutate } = useSWRConfig();

  useEffect(() => {
    const handler = () => {
      mutate();
      globalMutate(
        (k) => typeof k === "string" && (k.includes("/embassy/") || k.includes("/lmis/")),
        undefined,
        { revalidate: true }
      );
    };
    subscribe("candidateUpdated", handler as never);
    return () => unsubscribe("candidateUpdated", handler as never);
  }, [subscribe, unsubscribe, mutate, globalMutate]);
}

export const embassyApi = {
  bookMedical: (id: string, appointmentDate: string, facilityName: string, notes?: string) =>
    postJson(`/api/proxy/embassy/candidates/${id}/medical/book`, {
      appointmentDate,
      facilityName,
      notes,
    }),
  recordMedicalResult: (id: string, result: "Fit" | "Unfit", notes?: string) =>
    postJson(`/api/proxy/embassy/candidates/${id}/medical/result`, { result, notes }),
  bookTasheer: (id: string, appointmentDate: string, notes?: string) =>
    postJson(`/api/proxy/embassy/candidates/${id}/tasheer/book`, { appointmentDate, notes }),
  recordTasheerResult: (id: string, result: "Book Done" | "Expired", notes?: string) =>
    postJson(`/api/proxy/embassy/candidates/${id}/tasheer/result`, { result, notes }),
  setVisaReady: (id: string, notes?: string) =>
    postJson(`/api/proxy/embassy/candidates/${id}/visa/ready`, { notes }),
  submitVisa: (
    id: string,
    submissionDate?: string,
    referenceNumber?: string,
    notes?: string
  ) =>
    postJson(`/api/proxy/embassy/candidates/${id}/visa/submit`, {
      submissionDate,
      referenceNumber,
      notes,
    }),
  recordVisaOutcome: (
    id: string,
    outcome: "Issued" | "Rejected",
    rejectionReason?: string,
    notes?: string
  ) =>
    postJson(`/api/proxy/embassy/candidates/${id}/visa/outcome`, {
      outcome,
      rejectionReason,
      notes,
    }),
  resubmitVisa: (id: string, notes?: string) =>
    postJson(`/api/proxy/embassy/candidates/${id}/visa/resubmit`, { notes }),
};
