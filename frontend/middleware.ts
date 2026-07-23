import { NextResponse, type NextRequest } from "next/server";
import { getToken } from "next-auth/jwt";

function isMockMode() {
  return (
    process.env.NEXT_PUBLIC_USE_MOCKS === "true" ||
    process.env.NEXT_PUBLIC_USE_MOCKS === "1"
  );
}

/**
 * Authentication and authorization middleware
 *
 * Protects routes by verifying JWT tokens and session validity.
 * Handles special cases for password change flows and public routes.
 * In mock mode, still requires a NextAuth demo session for app routes
 * so Sign in / Sign out work end-to-end.
 */
export async function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Bypass middleware for Next.js internal routes, static assets, and authentication endpoints
  if (
    pathname.includes("/_next/") ||
    pathname.includes("/api/auth/") ||
    pathname === "/api/auth" ||
    pathname.startsWith("/api/proxy/") ||
    pathname.startsWith("/__nextjs") ||
    pathname === "/theme-init.js"
  ) {
    return NextResponse.next();
  }

  // Note: "/" must be exact — do not use startsWith("/") (matches every path)
  const publicRoutes = ["/login", "/forgot-password", "/reset-password", "/accident-reporting"];
  const isErrorPage = pathname.startsWith("/error/") || pathname === "/not-found";
  const isPublicRoute =
    pathname === "/" ||
    publicRoutes.some((route) => pathname.startsWith(route)) ||
    isErrorPage;

  const isChangePasswordRoute = pathname.startsWith("/change-password");
  const isApiRoute = pathname.startsWith("/api/");

  try {
    const token = (await Promise.race([
      getToken({
        req: request,
        secret: process.env.NEXTAUTH_SECRET || "development-secret-key-change-in-production",
      }),
      new Promise((_, reject) => setTimeout(() => reject(new Error("Token timeout")), 1000)),
    ])) as any;

    const isTokenError = token?.isError === true;
    const hasAccessToken = !!token?.accessToken;
    const requiresPasswordChange = token?.requiresPasswordChange === true;
    const isAuthenticated = !!token && !isTokenError && hasAccessToken;

    if (isChangePasswordRoute) {
      if (requiresPasswordChange || (isMockMode() && isAuthenticated)) {
        return NextResponse.next();
      }
      return NextResponse.redirect(new URL("/login", request.url));
    }

    // Public pages: landing + login stay reachable; bounce signed-in users into the app
    if (isPublicRoute) {
      if (isAuthenticated && (pathname.startsWith("/login") || pathname === "/login")) {
        return NextResponse.redirect(new URL("/overview", request.url));
      }
      return NextResponse.next();
    }

    if (!isAuthenticated) {
      if (isApiRoute) {
        return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
      }
      const loginUrl = new URL("/login", request.url);
      loginUrl.searchParams.set("callbackUrl", pathname);
      return NextResponse.redirect(loginUrl);
    }

    return NextResponse.next();
  } catch {
    if (isPublicRoute) {
      return NextResponse.next();
    }
    if (isApiRoute) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
    }
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("callbackUrl", pathname);
    return NextResponse.redirect(loginUrl);
  }
}

export const config = {
  matcher: [
    "/((?!_next/static|_next/image|_next/webpack-hmr|favicon.ico|sitemap.xml|robots.txt|theme-init\\.js|.*\\.(?:jpg|jpeg|gif|png|svg|ico|webp|woff|woff2|ttf|eot|js|css)$).*)",
  ],
};
