/**
 * Client-side error handler for API errors
 * Redirects to appropriate error pages for 403 and 404 errors
 */

import { ApiError } from "./helpers";

/**
 * Handle API errors and redirect to appropriate error pages
 * Only works on client-side (browser)
 */
export function handleClientApiError(error: unknown): void {
  // Only handle on client-side
  if (typeof window === "undefined") {
    return;
  }

  if (error instanceof ApiError) {
    // Redirect to error pages based on status code
    if (error.status === 403) {
      window.location.href = "/error/403";
      return;
    }
    
    if (error.status === 404) {
      window.location.href = "/error/404";
      return;
    }
  }
}

/**
 * Check if error should trigger a redirect
 */
export function shouldRedirectOnError(error: unknown): boolean {
  if (error instanceof ApiError) {
    return error.status === 403 || error.status === 404;
  }
  return false;
}
