/**
 * Unified API Client
 * Server/client aware; handles auth and context routing.
 */

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
      // Client-side execution: route requests through API proxy for security
      const { getSession } = await import("next-auth/react");
      const session = await getSession();
      token = session?.user?.accessToken;
      
      fullUrl = `/api/proxy${url}`;
    }

    // Execute HTTP request with authentication headers
    const locationId = !isServer ? localStorage.getItem("simba_active_location_id") : null;
    
    const response = await fetch(fullUrl, {
      ...options,
      headers: {
        "Content-Type": "application/json",
        ...(token && { Authorization: `Bearer ${token}` }),
        ...(locationId && { "X-Current-Location": locationId }),
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

