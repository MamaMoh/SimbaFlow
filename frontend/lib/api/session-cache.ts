/**
 * Server-side session access for route handlers.
 *
 * SECURITY: this used to memoize the session in a module-level global with a 5s TTL and **no key**.
 * Because the API proxy takes `session.user.accessToken` from here and forwards it as the Bearer
 * token, any second user hitting the server within that window was served the *first* user's
 * session — i.e. their requests ran as another user, against another tenant. In a multi-tenant SaaS
 * that is a cross-tenant data exposure, so the cross-request cache is gone.
 *
 * `getServerSession` only verifies the signed session cookie (no database round-trip), so calling
 * it per request is cheap. Where several calls happen inside a single request, React's `cache()`
 * de-duplicates them for that request only — which is safe because it is scoped per request.
 */

import { cache } from "react";
import { getServerSession } from "next-auth";
import { authOptions } from "@/lib/auth/authOption";
import type { Session } from "next-auth";

/**
 * Per-request memoized session. Never shared between requests or users.
 */
export const getCachedServerSession = cache(async (): Promise<Session | null> => {
  return getServerSession(authOptions);
});

/**
 * Retained for API compatibility. There is no cross-request cache to clear any more.
 */
export function clearSessionCache(): void {
  /* no-op: sessions are resolved per request */
}
