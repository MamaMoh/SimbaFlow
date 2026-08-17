import useSWR from "swr";
import { useTravelBoardRealtime } from "@/lib/api/travel";

type ApiResult<T> = {
  isSuccess?: boolean;
  data?: T;
  error?: string;
  statusCode?: number;
};

export type ExceptionCaseListItem = {
  id: string;
  candidateId: string;
  candidateName: string;
  passportNumber: string;
  type: string;
  status: string;
  openedAt: string;
  financialImpactAmount?: number | null;
  financialImpactCurrency?: string | null;
};

export type InvestigationNote = {
  id: string;
  authorUserId: string;
  body: string;
  createdAt: string;
  attachmentDocumentIds: string[];
};

export type LiabilityAssignment = {
  id: string;
  party: string;
  amount: number;
  currency: string;
  notes?: string | null;
  assignedAt: string;
};

export type ExceptionCaseDetail = {
  id: string;
  candidateId: string;
  candidateName: string;
  passportNumber: string;
  type: string;
  status: string;
  openedAt: string;
  openedByUserId: string;
  closedAt?: string | null;
  resolutionSummary?: string | null;
  financialImpactAmount?: number | null;
  financialImpactCurrency?: string | null;
  notes: InvestigationNote[];
  liabilities: LiabilityAssignment[];
};

type PaginatedList = {
  items: ExceptionCaseListItem[];
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

async function patchJson(url: string, body?: unknown): Promise<void> {
  const res = await fetch(url, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const data = await res.json().catch(() => ({}));
  if (!res.ok) throw new Error(data?.error || `Request failed (${res.status})`);
}

export function useExceptionCases(params?: {
  page?: number;
  pageSize?: number;
  status?: string;
  type?: string;
  officeId?: string;
}) {
  const qs = new URLSearchParams({
    page: String(params?.page ?? 1),
    pageSize: String(params?.pageSize ?? 50),
  });
  if (params?.status) qs.set("status", params.status);
  if (params?.type) qs.set("type", params.type);
  if (params?.officeId) qs.set("officeId", params.officeId);

  const key = `/api/proxy/exceptions?${qs.toString()}`;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<PaginatedList>>(key, fetcher, {
    revalidateOnFocus: false,
  });
  useTravelBoardRealtime(mutate);

  return {
    cases: data?.data?.items ?? [],
    totalCount: data?.data?.totalCount ?? 0,
    isLoading,
    error,
    mutate,
  };
}

export function useExceptionCase(id: string | null) {
  const key = id ? `/api/proxy/exceptions/${id}` : null;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<ExceptionCaseDetail>>(
    key,
    fetcher,
    { revalidateOnFocus: false }
  );
  useTravelBoardRealtime(mutate);

  return {
    exceptionCase: data?.data ?? null,
    isLoading,
    error,
    mutate,
  };
}

export const exceptionsApi = {
  addNote: (id: string, body: string, attachmentDocumentIds?: string[]) =>
    postJson(`/api/proxy/exceptions/${id}/notes`, { body, attachmentDocumentIds }),
  updateStatus: (id: string, status: string) =>
    patchJson(`/api/proxy/exceptions/${id}/status`, { status }),
  assignLiability: (
    id: string,
    party: string,
    amount: number,
    currency?: string,
    notes?: string
  ) =>
    postJson(`/api/proxy/exceptions/${id}/liabilities`, {
      party,
      amount,
      currency,
      notes,
    }),
  close: (
    id: string,
    resolutionSummary: string,
    financialImpactAmount?: number,
    financialImpactCurrency?: string
  ) =>
    postJson(`/api/proxy/exceptions/${id}/close`, {
      resolutionSummary,
      financialImpactAmount,
      financialImpactCurrency,
    }),
};
