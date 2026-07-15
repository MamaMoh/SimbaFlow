"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { ApiError } from "@/lib/api/helpers";
import { getErrorPagePath } from "@/lib/utils/error-redirect";

/**
 * Global Error Handler Component
 * Catches unhandled API errors and redirects to appropriate error pages
 */
export function GlobalErrorHandler() {
  const router = useRouter();

  useEffect(() => {
    // Handle unhandled promise rejections (API errors)
    const handleUnhandledRejection = (event: PromiseRejectionEvent) => {
      const error = event.reason;
      
      if (error instanceof ApiError) {
        // Prevent default error handling
        event.preventDefault();
        
        const errorPath = getErrorPagePath(error.status);
        if (errorPath) {
          router.push(errorPath);
        }
      }
    };

    // Handle general errors
    const handleError = (event: ErrorEvent) => {
      // Check if it's an ApiError in the error message or stack
      const error = event.error;
      if (error?.name === "ApiError" || error instanceof ApiError) {
        event.preventDefault();
        const status = error.status || 500;
        const errorPath = getErrorPagePath(status);
        if (errorPath) {
          router.push(errorPath);
        }
      }
    };

    window.addEventListener("unhandledrejection", handleUnhandledRejection);
    window.addEventListener("error", handleError);

    return () => {
      window.removeEventListener("unhandledrejection", handleUnhandledRejection);
      window.removeEventListener("error", handleError);
    };
  }, [router]);

  return null;
}

