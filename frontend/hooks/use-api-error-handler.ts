"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { ApiError } from "@/lib/api/helpers";

/**
 * Hook to handle API errors and redirect to appropriate error pages
 * Use this in client components that make API calls
 */
export function useApiErrorHandler() {
  const router = useRouter();

  const handleError = (error: unknown) => {
    if (error instanceof ApiError) {
      if (error.status === 403) {
        router.push("/error/403");
        return;
      }
      if (error.status === 404) {
        router.push("/error/404");
        return;
      }
    }
    // Re-throw other errors to be handled by the caller
    throw error;
  };

  return { handleError };
}

/**
 * Wrapper function to catch and handle API errors
 * Use this to wrap API calls in client components
 */
export async function withErrorHandling<T>(
  apiCall: () => Promise<T>,
  onError?: (error: unknown) => void
): Promise<T | null> {
  try {
    return await apiCall();
  } catch (error) {
    if (error instanceof ApiError) {
      if (error.status === 403 || error.status === 404) {
        // Redirect will be handled by the hook or component
        if (typeof window !== "undefined") {
          if (error.status === 403) {
            window.location.href = "/error/403";
            return null;
          }
          if (error.status === 404) {
            window.location.href = "/error/404";
            return null;
          }
        }
      }
    }
    
    // Call custom error handler if provided
    if (onError) {
      onError(error);
    }
    
    // Re-throw if not handled
    throw error;
  }
}
