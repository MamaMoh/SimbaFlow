"use client";

import useSWR from "swr";

/**
 * The agency's own logo and letterhead.
 *
 * Served through the proxy, which attaches the acting agency's id — so a platform administrator
 * working inside an agency gets that agency's branding, and everyone else gets their own.
 */
export type AgencyBranding = {
  logoPath: string | null;
  letterheadPath: string | null;
};

const KEY = "/api/proxy/branding/agency";

/** The URL an <img> loads a stored branding image from. */
export function logoUrl(path: string): string {
  return `/api/proxy/branding/file?path=${encodeURIComponent(path)}`;
}

async function fetchBranding(url: string): Promise<AgencyBranding> {
  const res = await fetch(url, { cache: "no-store" });
  const body = await res.json().catch(() => ({}));
  // Thrown rather than returned as empty: a request that failed and an agency that has uploaded
  // nothing both produced "No logo", so a broken branding endpoint looked like an empty one and
  // nobody could tell that their upload had not come back.
  if (!res.ok || body?.isSuccess === false) {
    throw new Error(body?.error || "Could not load the agency's branding");
  }
  const data = body?.data ?? body?.Data ?? {};
  return {
    logoPath: data.logoPath ?? data.LogoPath ?? null,
    letterheadPath: data.letterheadPath ?? data.LetterheadPath ?? null,
  };
}

export function useAgencyBranding(enabled = true) {
  const { data, error, mutate, isLoading } = useSWR<AgencyBranding>(
    enabled ? KEY : null,
    fetchBranding,
    { revalidateOnFocus: false }
  );
  return { branding: data, error: error as Error | undefined, mutate, isLoading };
}

/** What a badge shows: the letterhead if there is one, otherwise the mark. */
export function brandingMark(branding?: AgencyBranding): string | null {
  return branding?.logoPath ?? branding?.letterheadPath ?? null;
}
