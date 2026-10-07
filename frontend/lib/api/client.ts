/**
 * Unified API Client
 * Server/client aware; handles auth and context routing.
 */

import { getActingTenantId } from "@/lib/tenant/acting-tenant";
import { handleApiError, parseApiResponse } from "./helpers";
import { getCachedServerSession } from "./session-cache";
import { buildQueryString } from "@/lib/types/pagination";

const isServer = typeof window === "undefined";
const API_BASE_URL = process.env.API_BASE_URL;

/**
 * Core unified fetch function
 * 
 * Automatically detects execution context (server/client) and routes requests appropriately.
 * Utilizes cached session data to minimize repeated getServerSession calls for performance optimization.
 */
async function unifiedFetch<T>(
  url: string,
  options: RequestInit = {}
): Promise<T> {
  try {
    let token: string | undefined;
    let fullUrl: string;

    if (isServer) {
      // Server-side execution: route requests directly to backend API
      // Utilize cached session to optimize performance
      const session = await getCachedServerSession();
      token = session?.user?.accessToken;
      
      if (!API_BASE_URL) {
        throw new Error("API_BASE_URL environment variable is not set");
      }
      
      fullUrl = `${API_BASE_URL}${url}`;
    } else {
      // Client-side execution: route requests through API proxy for security.
      //
      // No token is attached here. The proxy route builds its own headers and sets
      // Authorization from the session it reads server-side, without ever looking at the one
      // that arrived — so anything put here was discarded.
      //
      // It was not free. Producing it meant getSession() on every single call, which is a
      // round trip to /api/auth/session and a run of the NextAuth jwt callback. Switching
      // agency revalidates every cached query at once, so dozens of those ran together, each
      // willing to refresh an access token near expiry, all presenting the same refresh token.
      // One rotation wins and the rest are a reused token, which is reuse detection working
      // exactly as designed: every session for that user revoked, the app refreshing in a loop
      // against tokens that will never be accepted again, and the tab pinned until Chrome
      // offered to kill it.
      fullUrl = `/api/proxy${url}`;
    }

    // Execute HTTP request with authentication headers
    const locationId = !isServer ? localStorage.getItem("simba_active_location_id") : null;
    // Platform admins can work inside a chosen agency. The backend ignores this header for
    // everyone else, so a tenant user cannot reach another agency by setting it.
    const actingTenantId = !isServer ? getActingTenantId() : null;
    
    const response = await fetch(fullUrl, {
      ...options,
      headers: {
        "Content-Type": "application/json",
        ...(token && { Authorization: `Bearer ${token}` }),
        ...(locationId && { "X-Current-Location": locationId }),
        ...(actingTenantId && { "X-Tenant-Id": actingTenantId }),
        ...options.headers,
      },
    });

    return parseApiResponse<T>(response);
  } catch (error) {
    throw handleApiError(error);
  }
}

/**
 * Unified API Client Interface
 * 
 * Provides a clean, type-safe interface for HTTP methods.
 * All methods automatically handle authentication and context-aware routing.
 */
export const apiClient = {
  /**
   * GET request. Pass params for query string (e.g. pageNumber, pageSize).
   */
  get: <T>(
    url: string,
    params?: Record<string, string | number | undefined | null>,
    options?: RequestInit
  ) => {
    const path = params ? `${url}${buildQueryString(params)}` : url;
    return unifiedFetch<T>(path, { ...options, method: "GET" });
  },

  /**
   * Execute HTTP POST request
   * @param url - API endpoint path
   * @param data - Request body data to be serialized as JSON
   * @param options - Optional fetch configuration
   * @returns Promise resolving to response data of type T
   */
  post: <T>(url: string, data?: any, options?: RequestInit) =>
    unifiedFetch<T>(url, {
      ...options,
      method: "POST",
      body: data ? JSON.stringify(data) : undefined,
    }),

  /**
   * Execute HTTP PUT request
   * @param url - API endpoint path
   * @param data - Request body data to be serialized as JSON
   * @param options - Optional fetch configuration
   * @returns Promise resolving to response data of type T
   */
  put: <T>(url: string, data?: any, options?: RequestInit) =>
    unifiedFetch<T>(url, {
      ...options,
      method: "PUT",
      body: data ? JSON.stringify(data) : undefined,
    }),

  /**
   * Execute HTTP PATCH request
   * @param url - API endpoint path
   * @param data - Request body data to be serialized as JSON
   * @param options - Optional fetch configuration
   * @returns Promise resolving to response data of type T
   */
  patch: <T>(url: string, data?: any, options?: RequestInit) =>
    unifiedFetch<T>(url, {
      ...options,
      method: "PATCH",
      body: data ? JSON.stringify(data) : undefined,
    }),

  /**
   * Execute HTTP DELETE request
   * @param url - API endpoint path
   * @param options - Optional fetch configuration
   * @returns Promise resolving to response data of type T
   */
  delete: <T>(url: string, options?: RequestInit) =>
    unifiedFetch<T>(url, { ...options, method: "DELETE" }),
};

