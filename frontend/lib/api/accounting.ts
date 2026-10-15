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
  /** True for this agency's own rate; false for a platform default it falls back to. */
  isAgencyRate: boolean;
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

const EMPTY_RATES: ExchangeRateRow[] = [];

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
    // Kept, not rebuilt: a fresh array on every render is new data as far as TanStack Table is
    // concerned, and that is a loop. See lib/data/use-table-rows.
    rates: data?.data ?? EMPTY_RATES,
    isLoading,
    error,
    mutate,
  };
}

export type ExpenseRow = {
  id: string;
  expenseDate: string;
  category: string;
  description: string;
  payee: string | null;
  amount: number;
  currency: string;
  exchangeRateToEtb: number;
  amountEtb: number;
  method: string;
  reference: string | null;
  commissionId: string | null;
  journalEntryId: string | null;
  voidedAt: string | null;
  voidReason: string | null;
};

export const EXPENSE_CATEGORIES: { value: string; label: string; payout?: boolean }[] = [
  { value: "MedicalPayout", label: "Medical payout", payout: true },
  { value: "TicketPayout", label: "Ticket payout", payout: true },
  { value: "PartnerPayout", label: "Partner payout", payout: true },
  { value: "Salaries", label: "Salaries and wages" },
  { value: "RentAndUtilities", label: "Rent and utilities" },
  { value: "TransportAndTravel", label: "Transport and travel" },
  { value: "GovernmentFees", label: "Government and licence fees" },
  { value: "Marketing", label: "Marketing" },
  { value: "OfficeSupplies", label: "Office supplies" },
  { value: "Other", label: "Other" },
];

const EMPTY_EXPENSES: ExpenseRow[] = [];

export function useExpenses(params?: { from?: string; to?: string }) {
  const qs = new URLSearchParams();
  if (params?.from) qs.set("from", params.from);
  if (params?.to) qs.set("to", params.to);
  const key = `/api/proxy/accounting/expenses?${qs}`;
  const { data, error, isLoading, mutate } = useSWR<ApiResult<ExpenseRow[]>>(key, fetcher, {
    revalidateOnFocus: false,
  });
  return { expenses: data?.data ?? EMPTY_EXPENSES, isLoading, error, mutate };
}

export const accountingApi = {
  recordExpense: (body: {
    expenseDate: string;
    category: string;
    description: string;
    amount: number;
    currency: string;
    method: string;
    payee?: string;
    reference?: string;
    commissionId?: string;
  }) => sendJson<string>("/api/proxy/accounting/expenses", "POST", body),

  voidExpense: (id: string, reason: string) =>
    sendJson<void>(`/api/proxy/accounting/expenses/${id}/void`, "POST", { reason }),

  upsertRate: (body: {
    fromCurrency: string;
    toCurrency: string;
    rate: number;
    effectiveDate: string;
    source?: string;
  }) => sendJson<string>("/api/proxy/accounting/rates", "POST", body),
};
