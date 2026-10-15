import useSWR from "swr";

type ApiResult<T> = {
  isSuccess?: boolean;
  data?: T;
  error?: string;
  statusCode?: number;
};

export type CommissionBoardRow = {
  id: string;
  candidateId: string;
  candidateName: string;
  passportNumber: string;
  labourId?: string | null;
  status: string;
  countryOfTravel: string | null;
  partnerName: string | null;
  openedAt: string;
  totalFeesAmount: number;
  totalPaidAmount: number;
  balanceAmount: number;
};

export type CommissionFee = {
  id: string;
  feeType: string;
  description: string | null;
  amount: number;
  currency: string;
  amountEtb: number;
  sortOrder: number;
};

export type CommissionPayment = {
  id: string;
  amount: number;
  currency: string;
  exchangeRateToEtb: number;
  amountEtb: number;
  paidAt: string;
  method: string;
  reference: string | null;
  notes: string | null;
  journalEntryId: string | null;
  receiptNumber: string | null;
  voidedAt: string | null;
  voidReason: string | null;
};

export type PaymentReceipt = {
  paymentId: string;
  receiptNumber: string;
  agencyName: string;
  agencyAddress: string | null;
  agencyPhone: string | null;
  agencyLicenseNumber: string | null;
  candidateName: string;
  passportNumber: string;
  partnerName: string | null;
  paidAt: string;
  amount: number;
  currency: string;
  exchangeRateToEtb: number;
  amountEtb: number;
  method: string;
  reference: string | null;
  notes: string | null;
  recordedBy: string;
  totalFeesEtb: number;
  totalPaidEtb: number;
  balanceEtb: number;
  voidedAt: string | null;
  voidReason: string | null;
};

export type CommissionDispute = {
  id: string;
  status: string;
  reason: string;
  openedAt: string;
  resolvedAt: string | null;
  resolutionNotes: string | null;
};

export type CommissionDetail = {
  id: string;
  candidateId: string;
  candidateName: string;
  passportNumber: string;
  labourId?: string | null;
  status: string;
  countryOfTravel: string | null;
  partnerName: string | null;
  openedAt: string;
  totalFeesAmount: number;
  totalPaidAmount: number;
  balanceAmount: number;
  fees: CommissionFee[];
  payments: CommissionPayment[];
  disputes: CommissionDispute[];
};

export type FeeLineInput = {
  feeType: string;
  description?: string | null;
  amount: number;
  currency?: string | null;
  sortOrder?: number;
};

type BoardResult = {
  items: CommissionBoardRow[];
  totalCount: number;
  page: number;
  pageSize: number;
};

const fetcher = async <T>(url: string): Promise<T> => {
  const res = await fetch(url);
  const body = await res.json().catch(() => ({}));
  if (!res.ok || body?.isSuccess === false) {
    throw new Error(body?.error || `Request failed (${res.status})`);
  }
  return body;
};

async function sendJson<T>(url: string, method: string, body?: unknown): Promise<T> {
  const res = await fetch(url, {
    method,
    headers: { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const data = await res.json().catch(() => ({}));
  if (!res.ok || data?.isSuccess === false) {
    throw new Error(data?.error || `Request failed (${res.status})`);
  }
  return data?.data as T;
}

export function useCommissionBoard(params?: {
  page?: number;
  pageSize?: number;
  status?: string;
  country?: string;
  search?: string;
  /** The funnel row, already encoded by the board. */
  filters?: string;
}) {
  const qs = new URLSearchParams({
    page: String(params?.page ?? 1),
    pageSize: String(params?.pageSize ?? 50),
  });
  if (params?.status) qs.set("status", params.status);
  if (params?.country) qs.set("country", params.country);
  if (params?.search) qs.set("search", params.search);
  if (params?.filters) qs.set("filters", params.filters);

  const key = `/api/proxy/commissions/board?${qs}`;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<BoardResult>>(key, fetcher, {
    revalidateOnFocus: false,
  });

  return {
    items: data?.data?.items ?? [],
    totalCount: data?.data?.totalCount ?? 0,
    isLoading,
    error,
    mutate,
    key,
  };
}

export function useCommission(id: string | undefined) {
  const key = id ? `/api/proxy/commissions/${id}` : null;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<CommissionDetail>>(key, fetcher, {
    revalidateOnFocus: false,
  });

  return {
    commission: data?.data,
    isLoading,
    error,
    mutate,
  };
}

export function usePaymentReceipt(paymentId: string | undefined) {
  const key = paymentId ? `/api/proxy/commissions/payments/${paymentId}/receipt` : null;
  const { data, error, isLoading } = useSWR<ApiResult<PaymentReceipt>>(key, fetcher, {
    revalidateOnFocus: false,
  });
  return { receipt: data?.data, isLoading, error };
}

export const commissionsApi = {
  voidPayment: (paymentId: string, reason: string) =>
    sendJson<void>(`/api/proxy/commissions/payments/${paymentId}/void`, "POST", { reason }),

  upsertFees: (id: string, fees: FeeLineInput[]) =>
    sendJson<void>(`/api/proxy/commissions/${id}/fees`, "PUT", { fees }),

  recordPayment: (
    id: string,
    body: {
      amount: number;
      currency?: string;
      method: string;
      paidAt?: string;
      reference?: string;
      notes?: string;
    }
  ) => sendJson<string>(`/api/proxy/commissions/${id}/payments`, "POST", body),

  openDispute: (id: string, reason: string) =>
    sendJson<string>(`/api/proxy/commissions/${id}/disputes`, "POST", { reason }),

  resolveDispute: (disputeId: string, resolutionNotes: string) =>
    sendJson<void>(`/api/proxy/commissions/disputes/${disputeId}/resolve`, "POST", {
      resolutionNotes,
    }),
};

export function formatEtb(n: number | null | undefined) {
  if (n == null) return "—";
  return new Intl.NumberFormat(undefined, {
    style: "currency",
    currency: "ETB",
    maximumFractionDigits: 2,
  }).format(n);
}
