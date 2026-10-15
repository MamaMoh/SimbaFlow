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

/** One of my own linked Telegram accounts. */
export type BotChatLink = {
  id: string;
  telegramUsername?: string | null;
  displayName?: string | null;
  linkedAt: string;
  lastSeenAt?: string | null;
};

/** A linked Telegram account with the person it belongs to, for the owner's roster. */
export type AgencyBotChatLink = BotChatLink & {
  userId: string;
  userName?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  isActive: boolean;
  muted: boolean;
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

/** My own linked phones. Several are allowed; the card shows each one separately. */
export function useBotLinks(enabled = true) {
  return useSWR<BotChatLink[]>(enabled ? "/api/proxy/bot/links" : null, fetcher, {
    revalidateOnFocus: false,
  });
}

/**
 * Everyone in the agency who can be reached on Telegram.
 *
 * Owner and platform team only — the server refuses the rest, so this is not asked for unless
 * the signed-in user is one of them.
 */
export function useAgencyBotLinks(enabled = true) {
  return useSWR<AgencyBotChatLink[]>(
    enabled ? "/api/proxy/bot/agency/links" : null,
    fetcher,
    { revalidateOnFocus: false }
  );
}

/**
 * Whether this account currently wants the bot's messages.
 *
 * The same flag /mute and /unmute set in the chat, so the switch and the command never disagree.
 */
export function useBotNotifications(enabled = true) {
  return useSWR<boolean>(enabled ? "/api/proxy/bot/notifications" : null, fetcher, {
    revalidateOnFocus: false,
  });
}

export const botApi = {
  async setNotifications(on: boolean) {
    const res = await fetch("/api/proxy/bot/notifications", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ on }),
    });
    return res.json();
  },
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
  /** One of my own, by id — the other phones stay linked. */
  async removeLink(id: string) {
    const res = await fetch(`/api/proxy/bot/links/${id}`, { method: "DELETE" });
    return res.json();
  },
  /** Somebody else's, as the agency owner. */
  async removeAgencyLink(id: string) {
    const res = await fetch(`/api/proxy/bot/agency/links/${id}`, { method: "DELETE" });
    return res.json();
  },
};
