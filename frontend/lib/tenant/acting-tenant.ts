"use client";

import { useEffect, useState } from "react";

/**
 * Which agency a platform admin is currently working inside.
 *
 * The backend already accepts an X-Tenant-Id header and honours it only for SuperAdmin, so
 * everything here is about remembering the choice and telling the app to re-read its data when
 * it changes. For everyone else this is never set and the tenant stays bound to their token.
 */
const KEY = "simba_acting_tenant_id";
const NAME_KEY = "simba_acting_tenant_name";
export const ACTING_TENANT_EVENT = "simba:acting-tenant-changed";

export function getActingTenantId(): string | null {
  if (typeof window === "undefined") return null;
  try {
    return window.localStorage.getItem(KEY);
  } catch {
    // Private browsing and blocked site data both throw here; working inside the token's own
    // agency is the right thing to fall back to.
    return null;
  }
}

export function getActingTenantName(): string | null {
  if (typeof window === "undefined") return null;
  try {
    return window.localStorage.getItem(NAME_KEY);
  } catch {
    return null;
  }
}

export function setActingTenant(id: string | null, name?: string | null): void {
  if (typeof window === "undefined") return;
  try {
    if (id) {
      window.localStorage.setItem(KEY, id);
      if (name) window.localStorage.setItem(NAME_KEY, name);
    } else {
      window.localStorage.removeItem(KEY);
      window.localStorage.removeItem(NAME_KEY);
    }
  } catch {
    // Nothing to do — the switch just will not be remembered across reloads.
  }
  writeActingTenantCookie(id);
  window.dispatchEvent(new CustomEvent(ACTING_TENANT_EVENT, { detail: { id, name } }));
}

/**
 * The cookie the API proxy reads, which is what actually scopes a request.
 *
 * A year, not a day. It used to expire after 24 hours while the choice itself lived in
 * localStorage and did not, so the switcher went on naming an agency that no request carried
 * any more: every write came back "No agency on this account" and every read quietly answered
 * with the built-in defaults instead of that agency's.
 */
function writeActingTenantCookie(id: string | null): void {
  // Lax is enough: it only ever travels to our own proxy route.
  document.cookie = id
    ? `simba_acting_tenant=${encodeURIComponent(id)}; path=/; max-age=31536000; samesite=lax`
    : "simba_acting_tenant=; path=/; max-age=0; samesite=lax";
}

/**
 * Puts the cookie back in step with the stored choice.
 *
 * Called while rendering rather than from an effect, so the cookie is in place before the
 * page's own requests go out — an effect would run after them and the first load would be
 * answered for the wrong agency.
 */
export function syncActingTenantCookie(): void {
  if (typeof document === "undefined") return;
  const id = getActingTenantId();
  const present = document.cookie
    .split("; ")
    .some((c) => c.startsWith("simba_acting_tenant="));
  if (id) writeActingTenantCookie(id);
  else if (present) writeActingTenantCookie(null);
}

/**
 * The agency a platform administrator is currently working inside, as React state.
 *
 * `undefined` until it has been read — localStorage cannot be touched while
 * rendering on the server, and a page that assumed "null" on the first paint
 * would flash the wrong message before correcting itself.
 */
export function useActingTenantId(): string | null | undefined {
  const [id, setId] = useState<string | null | undefined>(undefined);

  useEffect(() => {
    setId(getActingTenantId());
    const onChange = (e: Event) =>
      setId((e as CustomEvent<{ id: string | null }>).detail?.id ?? null);
    window.addEventListener(ACTING_TENANT_EVENT, onChange);
    return () => window.removeEventListener(ACTING_TENANT_EVENT, onChange);
  }, []);

  return id;
}
