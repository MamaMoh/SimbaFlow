import { NextResponse, type NextRequest } from "next/server";
import { getToken } from "next-auth/jwt";

/**
 * Authentication and authorization middleware
 * 
 * Protects routes by verifying JWT tokens and session validity.
 * Handles special cases for password change flows and public routes.
 */
export async function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Mock-data mode: allow browsing the UI without a live API/session
  if (process.env.NEXT_PUBLIC_USE_MOCKS === "true" || process.env.NEXT_PUBLIC_USE_MOCKS === "1") {
    if (pathname === "/login" || pathname.startsWith("/login")) {
      return NextResponse.redirect(new URL("/candidates", request.url));
    }
    return NextResponse.next();
  }
  
  // Bypass middleware for Next.js internal routes, static assets, and authentication endpoints
  if (
    pathname.includes('/_next/') ||
    pathname.includes('/api/auth/') ||
    pathname === '/api/auth' ||
    pathname.startsWith('/api/proxy/') ||
    pathname.startsWith('/__nextjs') ||
    pathname === '/theme-init.js'
  ) {
    return NextResponse.next();
  }
  
  // Define public routes that do not require authentication
  const publicRoutes = ["/login", "/forgot-password", "/reset-password", "/accident-reporting"];
  // Allow access to error pages without authentication
  const isErrorPage = pathname.startsWith('/error/') || pathname === '/not-found';
  const isPublicRoute = publicRoutes.some(route => pathname.startsWith(route)) || isErrorPage;
  
  // Allow unauthenticated access to public routes
  if (isPublicRoute) {
    return NextResponse.next();
  }
  
  // Identify password change route for special handling
  const isChangePasswordRoute = pathname.startsWith("/change-password");
  
  // Identify API routes that require authentication
  const isApiRoute = pathname.startsWith("/api/");
  
  try {
    // Retrieve authentication token with timeout to prevent request blocking
    const token = await Promise.race([
      getToken({
        req: request,
        secret: process.env.NEXTAUTH_SECRET,
      }),
      new Promise((_, reject) => 
        setTimeout(() => reject(new Error('Token timeout')), 1000)
      )
    ]) as any;
    
    // Validate token state and authentication status
    const isTokenError = token?.isError === true;
    const hasAccessToken = !!token?.accessToken;
    const requiresPasswordChange = token?.requiresPasswordChange === true;
    
    // Handle password change route access control
    if (isChangePasswordRoute) {
      // Grant access only when password change is required
      if (requiresPasswordChange) {
        return NextResponse.next();
      } else {
        // Redirect unauthorized access attempts to login
const loginUrl = new URL("/login", request.url);
        return NextResponse.redirect(loginUrl);
      }
    }
    
    // Redirect to login when token is invalid or missing
    if ((isTokenError || !hasAccessToken) && !isPublicRoute) {
const loginUrl = new URL("/login", request.url);
      loginUrl.searchParams.set("callbackUrl", pathname);
      return NextResponse.redirect(loginUrl);
    }
    
    // Determine authentication status from token validation
    const isAuthenticated = token && !isTokenError && hasAccessToken;
    
    // Redirect unauthenticated users to login page
    if (!isAuthenticated && !isPublicRoute) {
      const loginUrl = new URL("/login", request.url);
      loginUrl.searchParams.set("callbackUrl", pathname);
      return NextResponse.redirect(loginUrl);
    }
    
    // Redirect authenticated users away from authentication pages
    if (isAuthenticated && isPublicRoute) {
      return NextResponse.redirect(new URL("/overview", request.url));
    }
    
    // Enforce authentication for API routes
    if (isApiRoute && !isAuthenticated) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
    }
    
  } catch (error) {
    // Handle authentication errors by redirecting to login
    if (!isPublicRoute) {
const loginUrl = new URL("/login", request.url);
      loginUrl.searchParams.set("callbackUrl", pathname);
      return NextResponse.redirect(loginUrl);
    }
  }
  
  // Allow authenticated requests to proceed
  return NextResponse.next();
}

export const config = {
  matcher: [
    /*
     * Match all request paths except:
     * - _next/static (static files)
     * - _next/image (image optimization)
     * - _next/webpack-hmr (hot module reload)
     * - favicon.ico, sitemap.xml, robots.txt
     * - Static files (images, fonts, etc.)
     */
    '/((?!_next/static|_next/image|_next/webpack-hmr|favicon.ico|sitemap.xml|robots.txt|theme-init\\.js|.*\\.(?:jpg|jpeg|gif|png|svg|ico|webp|woff|woff2|ttf|eot|js|css)$).*)',
  ],
};
