import useSWR from "swr";

type ApiEnvelope<T> = {
  isSuccess?: boolean;
  data?: T;
  error?: string;
};

export type BotStatus = {
  configured: boolean;
  enabled: boolean;
  pollingEnabled: boolean;
  isConnected: boolean;
  botUsername?: string | null;
  lastError?: string | null;
  lastConnectedAt?: string | null;
};

export type DeliveryItem = {
  id: string;
  tenantId: string;
  userId?: string | null;
  channel: string;
  eventType: string;
  payloadSummary: string;
  status: string;
  sentAt?: string | null;
  createdAt: string;
};

const fetcher = async <T>(url: string): Promise<T> => {
  const res = await fetch(url);
  const body: ApiEnvelope<T> = await res.json().catch(() => ({}));
  if (!res.ok || body?.isSuccess === false) {
    throw new Error(body?.error || `Request failed (${res.status})`);
  }
  return body.data as T;
};

export function useBotStatus(enabled = true) {
  return useSWR<BotStatus>(enabled ? "/api/proxy/bot/status" : null, fetcher, {
    revalidateOnFocus: false,
  });
}

export function useBotDeliveries(enabled = true) {
  return useSWR<{ totalCount: number; items: DeliveryItem[] }>(
    enabled ? "/api/proxy/bot/deliveries?page=1&pageSize=20" : null,
    fetcher,
    { revalidateOnFocus: false }
  );
}

export const botApi = {
  async testConnection() {
    const res = await fetch("/api/proxy/bot/test", { method: "POST" });
    return res.json();
  },
  async createLinkCode() {
    const res = await fetch("/api/proxy/bot/link-code", { method: "POST" });
    return res.json();
  },
  async unlink() {
    const res = await fetch("/api/proxy/bot/link", { method: "DELETE" });
    return res.json();
  },
};
