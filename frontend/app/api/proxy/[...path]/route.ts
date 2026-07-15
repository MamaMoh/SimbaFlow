/**
 * Unified API Proxy Route Handler
 * 
 * Provides a single catch-all route that forwards all client-side API requests to the backend.
 * Handles authentication, request validation, and response normalization while preventing
 * server-side request forgery (SSRF) and path traversal attacks.
 */

import { NextRequest, NextResponse } from "next/server";
import { getCachedServerSession } from "@/lib/api/session-cache";

const API_BASE_URL = process.env.API_BASE_URL;

/**
 * Validates and sanitizes path segments to prevent security vulnerabilities
 * 
 * Prevents:
 * - Server-Side Request Forgery (SSRF) attacks
 * - Path traversal attacks (../, //, \)
 * - Protocol scheme injection (http://, file://, etc.)
 * - Control character injection
 * 
 * @param pathSegments - Array of path segments to validate
 * @returns Sanitized path string or null if validation fails
 */
function validateAndSanitizePath(pathSegments: string[]): string | null {
  if (!pathSegments || pathSegments.length === 0) {
    return null;
  }

  // Sanitize each path segment individually
  const sanitized = pathSegments
    .map((segment) => {
      // Reject path traversal patterns
      if (segment.includes("..") || segment.includes("//") || segment.includes("\\")) {
        return null;
      }
      // Reject URL protocol schemes
      if (segment.includes(":") && (segment.startsWith("http") || segment.startsWith("file") || segment.startsWith("ftp"))) {
        return null;
      }
      // Remove null bytes and control characters
      const cleaned = segment.replace(/[\x00-\x1F\x7F]/g, "");
      // Allow only alphanumeric characters, hyphens, underscores, and forward slashes
      if (!/^[a-zA-Z0-9\-_\/]+$/.test(cleaned)) {
        return null;
      }
      return cleaned;
    })
    .filter((segment): segment is string => segment !== null);

  if (sanitized.length === 0) {
    return null;
  }

  // Construct normalized path from validated segments
  const path = sanitized.join("/");
  
  // Validate final path does not contain absolute paths or protocol schemes
  if (path.startsWith("/") || path.startsWith("http://") || path.startsWith("https://") || path.startsWith("file://")) {
    return null;
  }

  return path;
}

async function handleRequest(
  request: NextRequest,
  pathSegments: string[],
  method: string
) {
  try {
    // Verify authentication using cached session for performance optimization
    const session = await getCachedServerSession();
    if (!session?.user?.accessToken) {
      return NextResponse.json(
        { error: "Unauthorized", code: "UNAUTHORIZED" },
        { status: 401 }
      );
    }

    if (!API_BASE_URL) {
      return NextResponse.json(
        { error: "API_BASE_URL not configured", code: "CONFIG_ERROR" },
        { status: 500 }
      );
    }

    // Validate and sanitize path to prevent SSRF and path traversal attacks
    const sanitizedPath = validateAndSanitizePath(pathSegments);
    if (!sanitizedPath) {
      return NextResponse.json(
        { error: "Invalid path", code: "INVALID_PATH" },
        { status: 400 }
      );
    }

    // Construct backend URL securely to prevent internal infrastructure exposure
    const url = new URL(request.url);
    const searchParams = url.searchParams.toString();
    
    // Build backend URL using URL constructor for safe path handling
    const backendUrlObj = new URL(API_BASE_URL);
    // Append to existing pathname instead of replacing it (preserves /api prefix if present)
    const existingPath = backendUrlObj.pathname.endsWith('/') 
      ? backendUrlObj.pathname.slice(0, -1) 
      : backendUrlObj.pathname;
    backendUrlObj.pathname = `${existingPath}/${sanitizedPath}`;
    if (searchParams) {
      backendUrlObj.search = searchParams;
    }
    const backendUrl = backendUrlObj.toString();
    
    // Backend URL is never logged or exposed to clients

    // Prepare request headers with authentication
    const headers: HeadersInit = {
      Authorization: `Bearer ${session.user.accessToken}`,
    };

    // Forward location context header if present
    const locationHeader = request.headers.get("x-current-location");
    if (locationHeader) {
      headers["X-Current-Location"] = locationHeader;
    }

    // Initialize fetch options with method and headers
    const fetchOptions: RequestInit = { method, headers };

    // Process request body for state-changing HTTP methods
    if (["POST", "PUT", "PATCH"].includes(method)) {
      const contentType = request.headers.get("content-type") || "";
      
      // Handle multipart/form-data requests
      if (contentType.includes("multipart/form-data")) {
        // Forward FormData directly; browser sets Content-Type with boundary automatically
        const formData = await request.formData();
        fetchOptions.body = formData;
      } else {
        // Handle JSON request bodies
        headers["Content-Type"] = "application/json";
        try {
          const body = await request.json();
          fetchOptions.body = JSON.stringify(body);
        } catch (e) {
          // Request has no body or contains invalid JSON
        }
      }
    } else {
      // Set Content-Type for read-only methods
      headers["Content-Type"] = "application/json";
    }

    // Forward validated request to backend API
    const response = await fetch(backendUrl, fetchOptions);
    
    // Handle 204 No Content responses
    if (response.status === 204) {
      return new NextResponse(null, { status: 204 });
    }

    // Get response content type
    const contentType = response.headers.get("content-type") || "";
    const isJson = contentType.includes("application/json");

    // Try to parse response as JSON if content-type suggests JSON, or if status indicates error
    if (isJson || response.status >= 400) {
      try {
        const text = await response.text();
        if (text) {
          try {
            const data = JSON.parse(text);
            // Return response with original HTTP status code
            return NextResponse.json(data, { status: response.status });
          } catch (parseError) {
            // If JSON parsing fails but we expected JSON, return the text as error
            return NextResponse.json(
              { error: text || response.statusText, code: "REQUEST_FAILED" },
              { status: response.status }
            );
          }
        } else {
          // Empty response body
          return NextResponse.json(
            { error: response.statusText || "Not Found", code: "REQUEST_FAILED" },
            { status: response.status }
          );
        }
      } catch (error) {
        // Error reading response body
        return NextResponse.json(
          { error: response.statusText || "Request failed", code: "REQUEST_FAILED" },
          { status: response.status }
        );
      }
    }

    // Non-JSON response (shouldn't happen for API endpoints, but handle gracefully)
    if (!response.ok) {
      return NextResponse.json(
        { error: response.statusText, code: "REQUEST_FAILED" },
        { status: response.status }
      );
    }
    
    return NextResponse.json({ success: true });
    
  } catch (error) {
    // Log errors server-side only to prevent information disclosure
// Return generic error message to prevent information disclosure
    return NextResponse.json(
      {
        error: "An error occurred processing your request",
        code: "INTERNAL_ERROR",
      },
      { status: 500 }
    );
  }
}

type ProxyRouteContext = {
  params: Promise<{ path: string[] }>;
};

async function resolveParams(context: ProxyRouteContext) {
  const params = await context.params;
  return params.path;
}

export async function GET(
  req: NextRequest,
  context: ProxyRouteContext
) {
  const path = await resolveParams(context);
  return handleRequest(req, path, "GET");
}

export async function POST(
  req: NextRequest,
  context: ProxyRouteContext
) {
  const path = await resolveParams(context);
  return handleRequest(req, path, "POST");
}

export async function PUT(
  req: NextRequest,
  context: ProxyRouteContext
) {
  const path = await resolveParams(context);
  return handleRequest(req, path, "PUT");
}

export async function PATCH(
  req: NextRequest,
  context: ProxyRouteContext
) {
  const path = await resolveParams(context);
  return handleRequest(req, path, "PATCH");
}

export async function DELETE(
  req: NextRequest,
  context: ProxyRouteContext
) {
  const path = await resolveParams(context);
  return handleRequest(req, path, "DELETE");
}

