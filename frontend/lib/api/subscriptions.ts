import useSWR from "swr";

export type RenewalNotice = "None" | "DueSoon" | "DueToday" | "Overdue";

export type SubscriptionRow = {
  id: string;
  name: string;
  status: "Active" | "Suspended" | "Deactivated";
  hasAccess: boolean;
  cycle: "Monthly" | "Yearly";
  subscriptionAmount: number;
  subscriptionCurrency: string;
  nextPaymentDue: string | null;
  daysUntilDue: number | null;
  notice: RenewalNotice;
  outstanding: number;
};

export type InvoiceRow = {
  id: string;
  number: string;
  periodStart: string;
  periodEnd: string;
  issuedOn: string;
  dueOn: string;
  amount: number;
  currency: string;
  status: "Draft" | "Issued" | "Paid" | "Void";
  cycle: "Monthly" | "Yearly";
  paidOn: string | null;
  paymentReference: string | null;
};

export type MySubscription = {
  agency: string;
  status: "Active" | "Suspended" | "Deactivated";
  hasAccess: boolean;
  cycle: "Monthly" | "Yearly";
  amount: number;
  currency: string;
  nextPaymentDue: string | null;
  daysUntilDue: number | null;
  notice: RenewalNotice;
  outstanding: {
    number: string;
    dueOn: string;
    amount: number;
    currency: string;
    periodStart: string;
    periodEnd: string;
  }[];
};

const fetcher = async (url: string) => {
  const res = await fetch(url);
  if (!res.ok) throw new Error("Could not load subscriptions");
  return (await res.json()).data;
};

export function useSubscriptions(enabled = true) {
  const { data, error, isLoading, mutate } = useSWR<SubscriptionRow[]>(
    enabled ? "/api/proxy/subscriptions" : null,
    fetcher,
    { revalidateOnFocus: false },
  );
  return { rows: data ?? [], error, isLoading, mutate };
}

export function useInvoices(tenantId: string | null) {
  const { data, error, mutate } = useSWR<InvoiceRow[]>(
    tenantId ? `/api/proxy/subscriptions/${tenantId}/invoices` : null,
    fetcher,
    { revalidateOnFocus: false },
  );
  return { invoices: data ?? [], error, mutate };
}

/**
 * The signed-in agency's own standing.
 *
 * Reads the platform schema only, so it keeps answering while the agency is suspended — this is
 * the one call that can explain why nothing else works.
 */
export function useMySubscription(enabled = true) {
  const { data, mutate } = useSWR<MySubscription | null>(
    enabled ? "/api/proxy/subscription/mine" : null,
    fetcher,
    { revalidateOnFocus: false, refreshInterval: 15 * 60 * 1000 },
  );
  return { subscription: data ?? null, mutate };
}

async function post(url: string, body?: unknown) {
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body ?? {}),
  });
  const json = await res.json().catch(() => ({}));
  if (!res.ok || json?.isSuccess === false) {
    throw new Error(json?.error || "That did not work.");
  }
  return json;
}

export const subscriptionApi = {
  update: async (tenantId: string, body: Record<string, unknown>) => {
    const res = await fetch(`/api/proxy/subscriptions/${tenantId}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    const json = await res.json().catch(() => ({}));
    if (!res.ok || json?.isSuccess === false) {
      throw new Error(json?.error || "Could not update the subscription.");
    }
  },
  generateInvoice: (tenantId: string, body: Record<string, unknown>) =>
    post(`/api/proxy/subscriptions/${tenantId}/invoices`, body),
  markPaid: (invoiceId: string, body: Record<string, unknown>) =>
    post(`/api/proxy/subscriptions/invoices/${invoiceId}/paid`, body),
  voidInvoice: (invoiceId: string) =>
    post(`/api/proxy/subscriptions/invoices/${invoiceId}/void`),
};
