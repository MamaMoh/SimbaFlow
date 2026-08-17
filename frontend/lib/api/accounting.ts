import useSWR from "swr";

type ApiResult<T> = {
  isSuccess?: boolean;
  data?: T;
  error?: string;
  statusCode?: number;
};

export type AccountRow = {
  id: string;
  code: string;
  name: string;
  type: string;
  currency: string;
  isSystem: boolean;
  isActive: boolean;
};

export type JournalLine = {
  id: string;
  accountId: string;
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
  memo: string | null;
};

export type JournalEntry = {
  id: string;
  entryNumber: string;
  postedAt: string;
  description: string;
  sourceType: string;
  sourceId: string | null;
  postedByUserId: string;
  lines: JournalLine[];
  totalDebit: number;
  totalCredit: number;
};

export type ExchangeRateRow = {
  id: string;
  fromCurrency: string;
  toCurrency: string;
  rate: number;
  effectiveDate: string;
  source: string | null;
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

export function useAccounts(activeOnly = true) {
  const key = `/api/proxy/accounting/accounts?activeOnly=${activeOnly}`;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<AccountRow[]>>(key, fetcher, {
    revalidateOnFocus: false,
  });
  return {
    accounts: data?.data ?? [],
    isLoading,
    error,
    mutate,
  };
}

export function useJournalEntry(id: string | undefined) {
  const key = id ? `/api/proxy/accounting/journals/${id}` : null;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<JournalEntry>>(key, fetcher, {
    revalidateOnFocus: false,
  });
  return {
    journal: data?.data,
    isLoading,
    error,
    mutate,
  };
}

export function useExchangeRates(params?: {
  fromCurrency?: string;
  toCurrency?: string;
  asOf?: string;
}) {
  const qs = new URLSearchParams({ take: "200" });
  if (params?.fromCurrency) qs.set("fromCurrency", params.fromCurrency);
  if (params?.toCurrency) qs.set("toCurrency", params.toCurrency);
  if (params?.asOf) qs.set("asOf", params.asOf);

  const key = `/api/proxy/accounting/rates?${qs}`;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<ExchangeRateRow[]>>(key, fetcher, {
    revalidateOnFocus: false,
  });

  return {
    rates: data?.data ?? [],
    isLoading,
    error,
    mutate,
  };
}

export const accountingApi = {
  upsertRate: (body: {
    fromCurrency: string;
    toCurrency: string;
    rate: number;
    effectiveDate: string;
    source?: string;
  }) => sendJson<string>("/api/proxy/accounting/rates", "POST", body),
};
