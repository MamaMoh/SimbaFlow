"use client";

import useSWR from "swr";

/**
 * The boxes on other people's forms that this agency answers the same way every time.
 *
 * Keyed by the form and the label printed on it, not by a column per answer, so a portal mapped
 * next year brings its own standing answers without a migration. The maps in lib/site-bridge
 * ship a default for each; a row here is this agency saying otherwise.
 */
export type PortalAnswer = { siteId: string; label: string; value: string };

const KEY = "/api/proxy/settings/portal-answers";

async function fetchAnswers(url: string): Promise<PortalAnswer[]> {
  const res = await fetch(url, { cache: "no-store" });
  const body = await res.json().catch(() => ({}));
  if (!res.ok || body?.isSuccess === false) {
    throw new Error(body?.error || "Could not load the agency's form answers");
  }
  const rows = (body?.data ?? body?.Data ?? []) as Record<string, unknown>[];
  return rows.map((r) => ({
    siteId: String(r.siteId ?? r.SiteId ?? ""),
    label: String(r.label ?? r.Label ?? ""),
    value: String(r.value ?? r.Value ?? ""),
  }));
}

export function usePortalAnswers(enabled = true) {
  const { data, error, mutate, isLoading } = useSWR<PortalAnswer[]>(
    enabled ? KEY : null,
    fetchAnswers,
    { revalidateOnFocus: false },
  );
  return { answers: data, error: error as Error | undefined, mutate, isLoading };
}

/** One lookup per form, which is the shape the fill planner wants. */
export function answersFor(answers: PortalAnswer[] | undefined, siteId: string) {
  const out: Record<string, string> = {};
  for (const a of answers ?? []) if (a.siteId === siteId) out[a.label] = a.value;
  return out;
}

export async function savePortalAnswers(answers: PortalAnswer[]): Promise<void> {
  const res = await fetch(KEY, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ answers }),
  });
  const body = await res.json().catch(() => ({}));
  if (!res.ok || body?.isSuccess === false) {
    throw new Error(body?.error || "Could not save the form answers");
  }
}
