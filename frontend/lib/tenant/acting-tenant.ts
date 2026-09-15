"use client";

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
  // The cookie is what the API proxy actually reads, so every request picks the agency up
  // regardless of which fetcher a page happens to use. Lax is enough: it only ever travels to
  // our own proxy route.
  document.cookie = id
    ? `simba_acting_tenant=${encodeURIComponent(id)}; path=/; max-age=86400; samesite=lax`
    : "simba_acting_tenant=; path=/; max-age=0; samesite=lax";
  window.dispatchEvent(new CustomEvent(ACTING_TENANT_EVENT, { detail: { id, name } }));
}
