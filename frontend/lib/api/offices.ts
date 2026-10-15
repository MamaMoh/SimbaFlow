"use client";

import useSWR from "swr";

/**
 * The agency's own branches. A candidate carries the office that registered them, and the
 * Office comparison report groups on it.
 */
export type Office = {
  id: string;
  name: string;
  city: string | null;
  phone: string | null;
  isActive: boolean;
  /** Active candidates registered by this office. */
  candidates: number;
};

export type OfficeInput = {
  name: string;
  city?: string | null;
  phone?: string | null;
  isActive?: boolean;
};

export const OFFICES_KEY = "/api/proxy/offices";

async function fetchOffices(url: string): Promise<Office[]> {
  const res = await fetch(url, { cache: "no-store" });
  const body = await res.json().catch(() => ({}));
  if (!res.ok || body?.isSuccess === false) {
    throw new Error(body?.error || "Could not load the agency's offices");
  }
  const rows = (body?.data ?? []) as Record<string, unknown>[];
  return rows.map((r) => ({
    id: String(r.id ?? ""),
    name: String(r.name ?? ""),
    city: (r.city as string | null) ?? null,
    phone: (r.phone as string | null) ?? null,
    isActive: r.isActive !== false,
    candidates: Number(r.candidates ?? 0),
  }));
}

export function useOffices(enabled = true) {
  const { data, error, mutate, isLoading } = useSWR<Office[]>(
    enabled ? OFFICES_KEY : null,
    fetchOffices,
    { revalidateOnFocus: false },
  );
  return { offices: data, error: error as Error | undefined, mutate, isLoading };
}

async function send(url: string, method: string, body?: unknown): Promise<void> {
  const res = await fetch(url, {
    method,
    headers: body === undefined ? undefined : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (res.status === 204) return;
  const json = await res.json().catch(() => ({}));
  if (!res.ok || json?.isSuccess === false) {
    throw new Error(json?.error || "Could not save the office");
  }
}

export const createOffice = (input: OfficeInput) => send(OFFICES_KEY, "POST", input);

export const updateOffice = (id: string, input: OfficeInput) =>
  send(`${OFFICES_KEY}/${id}`, "PUT", input);

export const deleteOffice = (id: string) => send(`${OFFICES_KEY}/${id}`, "DELETE");
