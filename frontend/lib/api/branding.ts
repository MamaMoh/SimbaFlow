"use client";

import useSWR from "swr";
import { getActingTenantId } from "@/lib/tenant/acting-tenant";

/**
 * The agency's own logo and letterhead.
 *
 * Served through the proxy, which attaches the acting agency's id — so a platform administrator
 * working inside an agency gets that agency's branding, and everyone else gets their own.
 */
export type AgencyBranding = {
  /** Whose branding this is; null when no agency is in scope or the one asked for is gone. */
  agencyName: string | null;
  logoPath: string | null;
  letterheadPath: string | null;
};

const KEY = "/api/proxy/branding/agency";

/**
 * The acting agency, sent the same way the API client sends it.
 *
 * These requests used plain fetch and so relied on the proxy finding the agency in a cookie,
 * while the CV download goes through the API client, which sends the header. When the two
 * disagreed the CV came out with the letterhead and this page said "Nothing uploaded". Sending
 * the header here puts both on the same agency.
 */
export function brandingHeaders(): HeadersInit {
  const id = getActingTenantId();
  return id ? { "X-Tenant-Id": id } : {};
}

/** The URL an <img> loads a stored branding image from. */
export function logoUrl(path: string): string {
  return `/api/proxy/branding/file?path=${encodeURIComponent(path)}`;
}

async function fetchBranding([url]: readonly [string, string | null]): Promise<AgencyBranding> {
  const res = await fetch(url, { cache: "no-store", headers: brandingHeaders() });
  const body = await res.json().catch(() => ({}));
  // Thrown rather than returned as empty: a request that failed and an agency that has uploaded
  // nothing both produced "No logo", so a broken branding endpoint looked like an empty one and
  // nobody could tell that their upload had not come back.
  if (!res.ok || body?.isSuccess === false) {
    throw new Error(body?.error || "Could not load the agency's branding");
  }
  const data = body?.data ?? body?.Data ?? {};
  return {
    agencyName: data.agencyName ?? data.AgencyName ?? null,
    logoPath: data.logoPath ?? data.LogoPath ?? null,
    letterheadPath: data.letterheadPath ?? data.LetterheadPath ?? null,
  };
}

export function useAgencyBranding(enabled = true) {
  // Keyed on the acting agency too, so one agency's branding is never shown under another's.
  const { data, error, mutate, isLoading } = useSWR<AgencyBranding>(
    enabled ? ([KEY, getActingTenantId()] as const) : null,
    fetchBranding,
    { revalidateOnFocus: false }
  );
  return { branding: data, error: error as Error | undefined, mutate, isLoading };
}

/** What a badge shows: the letterhead if there is one, otherwise the mark. */
export function brandingMark(branding?: AgencyBranding): string | null {
  return branding?.logoPath ?? branding?.letterheadPath ?? null;
}
