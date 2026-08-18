import useSWR, { useSWRConfig } from "swr";
import { useEffect } from "react";
import { useSignalR } from "@/lib/signalr/signalr-provider";

type ApiResult<T> = {
  isSuccess?: boolean;
  data?: T;
  error?: string;
  statusCode?: number;
};

export type TravelBoardRow = {
  id: string;
  fullName: string;
  passportNumber: string;
  labourId?: string | null;
  partnerName?: string | null;
  countryOfTravel?: string | null;
  statusValues: Record<string, string>;
  daysInStage: number;
  remainingDays?: number | null;
  isCanceled: boolean;
  registeredAt: string;
};

type PaginatedBoard = {
  items: TravelBoardRow[];
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

/** Invalidate travel/arrival/exception board keys on SignalR candidate updates. */
export function useTravelBoardRealtime(mutate: () => void) {
  const { subscribe, unsubscribe } = useSignalR();
  const { mutate: globalMutate } = useSWRConfig();

  useEffect(() => {
    const handler = () => {
      mutate();
      globalMutate(
        (k) =>
          typeof k === "string" &&
          (k.includes("/travel/") ||
            k.includes("/arrival/") ||
            k.includes("/exceptions")),
        undefined,
        { revalidate: true }
      );
    };
    subscribe("candidateUpdated", handler as never);
    return () => unsubscribe("candidateUpdated", handler as never);
  }, [subscribe, unsubscribe, mutate, globalMutate]);
}

export function useTicketBoard(params?: {
  page?: number;
  pageSize?: number;
  search?: string;
}) {
  const qs = new URLSearchParams({
    page: String(params?.page ?? 1),
    pageSize: String(params?.pageSize ?? 50),
  });
  if (params?.search) qs.set("search", params.search);

  const key = `/api/proxy/travel/ticket/board?${qs.toString()}`;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<PaginatedBoard>>(key, fetcher, {
    revalidateOnFocus: false,
  });
  useTravelBoardRealtime(mutate);

  return {
    candidates: data?.data?.items ?? [],
    totalCount: data?.data?.totalCount ?? 0,
    isLoading,
    error,
    mutate,
  };
}

export function useDepartureBoard(params?: {
  page?: number;
  pageSize?: number;
  search?: string;
  includeCanceled?: boolean;
}) {
  const qs = new URLSearchParams({
    page: String(params?.page ?? 1),
    pageSize: String(params?.pageSize ?? 50),
  });
  if (params?.search) qs.set("search", params.search);
  if (params?.includeCanceled) qs.set("includeCanceled", "true");

  const key = `/api/proxy/travel/departure/board?${qs.toString()}`;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<PaginatedBoard>>(key, fetcher, {
    revalidateOnFocus: false,
  });
  useTravelBoardRealtime(mutate);

  return {
    candidates: data?.data?.items ?? [],
    totalCount: data?.data?.totalCount ?? 0,
    isLoading,
    error,
    mutate,
  };
}

export const travelApi = {
  bookTicket: (
    id: string,
    destination: string,
    flightDate: string,
    ticketRef?: string,
    notes?: string
  ) =>
    postJson(`/api/proxy/travel/candidates/${id}/ticket/book`, {
      destination,
      flightDate,
      ticketRef,
      notes,
    }),
  markNotified: (id: string, notes?: string) =>
    postJson(`/api/proxy/travel/candidates/${id}/notify`, { notes }),
  confirmDeparted: (id: string, notes?: string) =>
    postJson(`/api/proxy/travel/candidates/${id}/departed`, { notes }),
  recordNotDeparted: (
    id: string,
    reason: string,
    outcome: "BackToTicket" | "CancelDeparture",
    reasonOther?: string,
    notes?: string
  ) =>
    postJson(`/api/proxy/travel/candidates/${id}/not-departed`, {
      reason,
      outcome,
      reasonOther,
      notes,
    }),
};
