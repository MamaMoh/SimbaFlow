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
  
  // The public marketing site is always reachable, signed in or not
  if (pathname === "/") {
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

  /**
   * How a request that has no session is turned away.
   *
   * A page is sent to the login screen. An API route is answered in JSON, because a fetch
   * follows a redirect without saying so and hands the caller the login page with a 200 on it —
   * which is then parsed as JSON and fails on the first "<". What the person sees is a complaint
   * about a bracket rather than being told they are signed out.
   *
   * There was already a branch returning 401 for an API route, but it sat below the redirects
   * and so could never be reached. Turning away in one place is what stops that happening again:
   * the three exits below cannot disagree about what an API route gets.
   */
  const turnAway = () => {
    if (isApiRoute) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
    }
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("callbackUrl", pathname);
    return NextResponse.redirect(loginUrl);
  };

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
    
    // Turn away when the token is invalid or missing
    if (isTokenError || !hasAccessToken) {
      return turnAway();
    }
    
    // Determine authentication status from token validation
    const isAuthenticated = token && !isTokenError && hasAccessToken;
    
    // Turn away anyone without a session
    if (!isAuthenticated) {
      return turnAway();
    }
    
    // Redirect authenticated users away from authentication pages
    if (isAuthenticated && isPublicRoute) {
      return NextResponse.redirect(new URL("/overview", request.url));
    }
  } catch {
    // Reading the token failed or took longer than the second it is given. The session cannot
    // be established either way, so this is turned away like any other request without one —
    // and an API caller is told so in JSON rather than handed a login page.
    return turnAway();
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
