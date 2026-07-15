/** API helpers: error handling and response parsing. */

/** App base URL for server-side fetch. Production requires NEXT_PUBLIC_APP_URL or VERCEL_URL. */
export function getAppBaseUrl(): string {
  if (typeof window !== "undefined") return "";
  const url =
    process.env.NEXT_PUBLIC_APP_URL ??
    (process.env.VERCEL_URL ? `https://${process.env.VERCEL_URL}` : null);
  if (url) return url;
  if (process.env.NODE_ENV === "production") return "";
  return "http://localhost:3000";
}

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public code?: string,
    public errors?: unknown[]
  ) {
    super(message);
    this.name = "ApiError";
    
    if (typeof Error.captureStackTrace === "function") {
      Error.captureStackTrace(this, ApiError);
    }
  }
}

/** Get HTTP status from caught error (ApiError.status or 500). Use in API route catch blocks. */
export function getErrorStatus(error: unknown): number {
  if (error instanceof ApiError) return error.status;
  const n = (error as { status?: number; statusCode?: number })?.status ?? (error as { statusCode?: number })?.statusCode;
  return typeof n === "number" && n >= 400 && n < 600 ? n : 500;
}

/**
 * Unwrap a list from backend response. Handles StandardApiResponse<Paginated<T>> (after parseApiResponse
 * returns the inner Data) and Paginated<T> with Data/data array. Returns empty array if not found.
 */
export function unwrapPaginatedList<T = unknown>(response: unknown): T[] {
  if (Array.isArray(response)) return response as T[];
  const inner = (response as { Data?: unknown[]; data?: unknown[] })?.Data ?? (response as { data?: unknown[] })?.data;
  return Array.isArray(inner) ? (inner as T[]) : [];
}

/**
 * Parse and normalize API response
 */
export async function parseApiResponse<T>(response: Response): Promise<T> {
  const contentType = response.headers.get("content-type") || "";

  // Handle empty responses (204 No Content)
  if (response.status === 204 || !contentType.includes("application/json")) {
    // Try to read the raw text body for better diagnostics
    const rawText = await response.text().catch(() => "");

    if (!response.ok) {
      // Provide more helpful error messages for common status codes
      // Never expose internal URLs in error messages
      let errorMessage = rawText?.trim() || response.statusText || "Request failed";
      
      if (response.status === 404 && !rawText) {
        errorMessage = `Resource not found (404): The requested endpoint may not exist`;
      } else if (response.status === 401 && !rawText) {
        errorMessage = "Unauthorized - Please sign in again";
      } else if (response.status === 403 && !rawText) {
        errorMessage = "Forbidden - You don't have permission to access this resource";
      } else if (response.status === 500 && !rawText) {
        errorMessage = "Server error - Please try again later";
      }
      
      throw new ApiError(errorMessage, response.status, `HTTP_${response.status}`);
    }
    return (rawText ? (rawText as unknown as T) : undefined) as T;
  }

  // Parse JSON response
  let data: any;
  try {
    data = await response.json();
  } catch (e) {
    // Attempt to read raw text to include backend error details
    const rawText = await response.text().catch(() => "");
    const msg = rawText?.trim() || "Invalid JSON response";
    throw new ApiError(msg, response.status, "PARSE_ERROR");
  }

  // Handle standardized error format { succeeded: false, errors: [...] }
  // Check both camelCase (errors) and PascalCase (Errors) for compatibility
  const errorsArray = data?.errors || data?.Errors;
  
  // Check if response indicates failure
  const isFailure = data?.succeeded === false || 
                    (!response.ok && data?.succeeded !== true && data?.succeeded !== undefined);
  
  if (isFailure) {
    let errorMsg = "Request failed";
    
    // Prioritize errors array - could be strings or objects
    if (Array.isArray(errorsArray) && errorsArray.length > 0) {
      const errorMessages = errorsArray.map((err: any) => {
        if (typeof err === "string") {
          return err;
        } else if (err?.errorMessage) {
          return err.errorMessage;
        } else if (err?.message) {
          return err.message;
        } else if (err?.propertyName && err?.errorMessage) {
          return `${err.propertyName}: ${err.errorMessage}`;
        }
        return String(err);
      }).filter((msg: string) => msg && msg.trim());
      
      if (errorMessages.length > 0) {
        errorMsg = errorMessages.join("\n");
      }
    }
    
    // Fall back to message field if errors array didn't provide messages
    if (errorMsg === "Request failed" && data.message) {
      errorMsg = data.message;
    }
    
    // If still no message, provide a more descriptive default
    if (errorMsg === "Request failed") {
      errorMsg = `Request failed with status ${response.status}`;
      if (errorsArray && Array.isArray(errorsArray) && errorsArray.length > 0) {
        errorMsg += `: ${JSON.stringify(errorsArray)}`;
      }
    }
    
    throw new ApiError(errorMsg, response.status, data.code, errorsArray);
  }

  // Handle HTTP errors
  if (!response.ok) {
    // Check for validation errors with errors array (ASP.NET Core format)
    // Check both camelCase (errors) and PascalCase (Errors) for compatibility
    const errorsArray = data?.errors || data?.Errors;
    
    if (errorsArray && Array.isArray(errorsArray) && errorsArray.length > 0) {
      // Check if it's validation error format (has propertyName and errorMessage)
      const isValidationError = errorsArray.some((err: any) => err.propertyName && err.errorMessage);
      
      if (isValidationError) {
        // Extract validation error messages
        const errorMessages = errorsArray
          .map((err: any) => err.errorMessage || err.message || `${err.propertyName}: Validation failed`)
          .filter((msg: string) => msg) // Remove empty messages
          .join("\n");
        
        if (errorMessages) {
          throw new ApiError(
            errorMessages,
            response.status,
            data.code || `HTTP_${response.status}`,
            errorsArray
          );
        }
      }
      
      // If errors array contains strings, use them directly
      if (errorsArray.every((err: any) => typeof err === "string")) {
        const errorMessages = errorsArray.filter((msg: string) => msg && msg.trim()).join("\n");
        if (errorMessages) {
          throw new ApiError(
            errorMessages,
            response.status,
            data.code || `HTTP_${response.status}`,
            errorsArray
          );
        }
      }
    }
    
    // Fall back to other error formats
    // Try different possible error message fields
    let errorMessage = 
      data.message || 
      data.error || 
      data.detail || 
      data.title ||
      "Request failed";
    
    // Provide more helpful error messages for common status codes if no specific message
    if (response.status === 404 && errorMessage === "Request failed") {
      errorMessage = `Resource not found (404): The requested endpoint may not exist`;
    } else if (response.status === 400 && errorMessage === "Request failed") {
      errorMessage = "Bad request - Please check your input";
    } else if (response.status === 401 && errorMessage === "Request failed") {
      errorMessage = "Unauthorized - Please sign in again";
    } else if (response.status === 403 && errorMessage === "Request failed") {
      errorMessage = "Forbidden - You don't have permission to access this resource";
    } else if (response.status === 500 && errorMessage === "Request failed") {
      errorMessage = "Server error - Please try again later";
    }
    
    throw new ApiError(
      errorMessage,
      response.status,
      data.code || `HTTP_${response.status}`,
      data.errors
    );
  }

  // Unwrap data envelope { data: ... } or { Data: ... } or { succeeded: true, data: ... }
  // Handle both camelCase (data) and PascalCase (Data) from C# API
  if (data?.Data !== undefined) {
    return data.Data as T;
  }
  return data?.data ?? data;
}

/**
 * Handle and normalize API errors
 */
export function handleApiError(error: unknown): Error {
  if (error instanceof ApiError) {
    return error;
  }

  // Preserve message/status when wrapped errors contain a response-like shape
  const maybeStatus = (error as any)?.status || (error as any)?.statusCode;
  let maybeMessage =
    (error as any)?.message ||
    (error as any)?.error ||
    (error as any)?.detail ||
    (error as any)?.title;

  // Network-level "fetch failed" (backend unreachable, TLS, connection refused). User-facing message only.
  if (
    typeof maybeMessage === "string" &&
    (maybeMessage === "fetch failed" || maybeMessage.toLowerCase().includes("fetch failed"))
  ) {
    maybeMessage = "Unable to connect. Please try again later.";
  }

  if (error instanceof Error) {
    return new ApiError(maybeMessage || error.message, maybeStatus || 500, "UNKNOWN_ERROR");
  }

  if (maybeMessage) {
    return new ApiError(String(maybeMessage), maybeStatus || 500, "UNKNOWN_ERROR");
  }

  return new ApiError("An unknown error occurred", maybeStatus || 500, "UNKNOWN_ERROR");
}

/**
 * Format error for user-facing messages
 */
export function formatErrorForUI(error: unknown): string {
  if (error instanceof ApiError) {
    return error.message;
  }
  if (error instanceof Error) {
    return error.message;
  }
  // Preserve string/primitive errors
  if (typeof error === "string") return error;
  if (typeof error === "object" && error !== null) {
    const maybeMsg =
      (error as any).message ||
      (error as any).error ||
      (error as any).detail ||
      (error as any).title;
    if (maybeMsg) return String(maybeMsg);
  }
  return "An unexpected error occurred";
}

