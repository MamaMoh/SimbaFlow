/**
 * Error Redirect Utility
 * Handles redirects to error pages based on HTTP status codes
 */

"use client";

import { useRouter } from "next/navigation";
import { ApiError } from "@/lib/api/helpers";

/**
 * Error page paths mapping
 */
export const ERROR_PAGES = {
  401: "/error/401",
  403: "/error/403",
  404: "/error/404",
  500: "/error/500",
  502: "/error/500", // Bad Gateway -> Server Error
  503: "/error/500", // Service Unavailable -> Server Error
  504: "/error/500", // Gateway Timeout -> Server Error
} as const;

/**
 * Get error page path for a given status code
 */
export function getErrorPagePath(status: number): string | null {
  if (status in ERROR_PAGES) {
    return ERROR_PAGES[status as keyof typeof ERROR_PAGES];
  }
  
  // Default mappings
  if (status >= 400 && status < 500) {
    return "/error/404"; // Client errors -> 404
  }
  
  if (status >= 500) {
    return "/error/500"; // Server errors -> 500
  }
  
  return null;
}

/**
 * Hook to handle API errors and redirect to appropriate error pages
 */
export function useErrorRedirect() {
  const router = useRouter();

  const handleError = (error: unknown, redirect: boolean = true) => {
    if (error instanceof ApiError) {
      const errorPath = getErrorPagePath(error.status);
      
      if (errorPath && redirect) {
        router.push(errorPath);
        return;
      }
    }
    
    // Fallback to generic error handling
    throw error;
  };

  return { handleError, getErrorPagePath };
}

/**
 * Client-side error redirect (for use in React components)
 */
export function redirectToErrorPage(status: number): void {
  if (typeof window === "undefined") return;
  
  const errorPath = getErrorPagePath(status);
  if (errorPath) {
    window.location.href = errorPath;
  }
}

/**
 * Server-side error redirect (for use in server components/actions)
 */
export function getErrorRedirectPath(status: number): string | null {
  return getErrorPagePath(status);
}

