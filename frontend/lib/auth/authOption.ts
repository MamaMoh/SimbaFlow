import { getServerSession, type NextAuthOptions } from "next-auth";
import Credentials from "next-auth/providers/credentials";
import { authenticate, refreshAccessToken } from "../services/userService";
import { jwtDecode } from "jwt-decode";

/**
 * NextAuth URL configuration for production
 */
if (process.env.NODE_ENV === "production") {
  if (!process.env.NEXTAUTH_TRUST_HOST) {
    process.env.NEXTAUTH_TRUST_HOST = "true";
  }
  if (!process.env.NEXTAUTH_URL) {
    const host = process.env.VERCEL_URL || process.env.HOST || process.env.SERVER_HOSTNAME;
    if (host) {
      const hasProtocol = /^https?:\/\//i.test(host);
      const forceHttps = process.env.FORCE_HTTPS !== "false";
      const protocol = forceHttps ? "https" : "http";
      process.env.NEXTAUTH_URL = hasProtocol ? host : `${protocol}://${host}`;
    }
  }
}

/**
 * Cookie security configuration
 */
const nextAuthUrl = process.env.NEXTAUTH_URL || "";
const isHttps = nextAuthUrl.startsWith("https://");
const isLocalhost = nextAuthUrl.includes("localhost") || nextAuthUrl.includes("127.0.0.1") || !nextAuthUrl;
const useSecureCookies = isHttps && !isLocalhost;

export const authOptions: NextAuthOptions = {
  session: {
    strategy: "jwt",
    // Session maxAge matches refresh token lifetime (7 days)
    maxAge: 7 * 24 * 60 * 60,
  },
  cookies: {
    sessionToken: {
      name: useSecureCookies ? "__Secure-next-auth.session-token" : "next-auth.session-token",
      options: { httpOnly: true, sameSite: "lax", path: "/", secure: useSecureCookies },
    },
    callbackUrl: {
      name: useSecureCookies ? "__Secure-next-auth.callback-url" : "next-auth.callback-url",
      options: { httpOnly: true, sameSite: "lax", path: "/", secure: useSecureCookies },
    },
    csrfToken: {
      name: useSecureCookies ? "__Host-next-auth.csrf-token" : "next-auth.csrf-token",
      options: { httpOnly: true, sameSite: "lax", path: "/", secure: useSecureCookies },
    },
  },
  secret: (() => {
    const isBrowser = typeof window !== "undefined";
    const secret = process.env.NEXTAUTH_SECRET;
    if (isBrowser) return secret || "client-side-placeholder";
    if (!secret && process.env.NODE_ENV === "production") {
      throw new Error("NEXTAUTH_SECRET is required in production");
    }
    return secret || "development-secret-key-change-in-production";
  })(),
  callbacks: {
    async jwt({ token, user }) {
      // Initial sign-in: attach tokens from authenticate()
      if (user) {
        const u = user as any;

        // Handle MFA required
        if (u.requiresMfa) {
          (token as any).requiresMfa = true;
          (token as any).username = u.username;
          (token as any).isError = false;
          return token;
        }

        // Handle password change required
        if (u.requiresPasswordChange) {
          (token as any).requiresPasswordChange = true;
          (token as any).username = u.username;
          (token as any).accessToken = u.accessToken;
          (token as any).isError = false;
          return token;
        }

        // Normal login — store tokens and profile
        token.accessToken = u.accessToken as any;
        token.refreshToken = u.refreshToken as any;
        (token as any).userProfile = u.userProfile;
        (token as any).grantedClaims = u.grantedClaims;
        (token as any).roles = u.roles;
        (token as any).requiresPasswordChange = false;
        (token as any).requiresMfa = false;
        (token as any).isError = false;

        // Set expiration from decoded token
        if (u.accessToken) {
          try {
            const decoded = jwtDecode(u.accessToken) as any;
            if (decoded?.exp) {
              (token as any).expiresAt = decoded.exp * 1000;
            }
          } catch { /* ignore decode errors */ }
        }

        if (!(token as any).expiresAt) {
          (token as any).expiresAt = u.expiresAt || Date.now() + 15 * 60 * 1000;
        }

        return token;
      }

      // Subsequent requests — check if token refresh is needed
      const expiresAt = Number((token as any)?.expiresAt || 0);
      const refreshBuffer = 2 * 60 * 1000; // Refresh 2 minutes before expiry
      const needsRefresh = expiresAt > 0 && expiresAt < Date.now() + refreshBuffer;

      // Cooldown to prevent refresh loops
      const lastRefresh = (token as any)?.lastSuccessfulRefresh || 0;
      const refreshCooldown = 30 * 1000; // 30 seconds
      if (lastRefresh > 0 && Date.now() - lastRefresh < refreshCooldown) {
        return token;
      }

      // Skip refresh if already in error state (cooldown)
      if ((token as any)?.isError && (token as any)?.lastRefreshError) {
        if (Date.now() - (token as any).lastRefreshError < 60000) {
          return token;
        }
      }

      // Attempt refresh if needed and refresh token is available
      if (needsRefresh && (token as any)?.refreshToken) {
        try {
          const refreshed = await refreshAccessToken((token as any).refreshToken);

          (token as any).accessToken = refreshed.accessToken;
          (token as any).refreshToken = refreshed.refreshToken;
          (token as any).isError = false;
          (token as any).lastRefreshError = undefined;
          (token as any).lastSuccessfulRefresh = Date.now();

          // Update expiration
          try {
            const decoded = jwtDecode(refreshed.accessToken) as any;
            if (decoded?.exp) (token as any).expiresAt = decoded.exp * 1000;
          } catch {
            (token as any).expiresAt = Date.now() + 15 * 60 * 1000;
          }

          // Update claims if returned
          if (refreshed.grantedClaims?.length) {
            (token as any).grantedClaims = refreshed.grantedClaims;
          }
          if (refreshed.roles?.length) {
            (token as any).roles = refreshed.roles;
          }
          if (refreshed.userProfile) {
            (token as any).userProfile = refreshed.userProfile;
          }
        } catch (error: any) {
          (token as any).isError = true;
          (token as any).lastRefreshError = Date.now();

          // If refresh token is truly invalid, clear everything
          const msg = error?.message || "";
          if (msg.includes("401") || msg.includes("compromised") || msg.includes("expired")) {
            delete (token as any).accessToken;
            delete (token as any).refreshToken;
            delete (token as any).grantedClaims;
          }
        }
      }

      return token;
    },

    async session({ session, token }) {
      (session as any).isError = (token as any)?.isError || false;

      // Handle MFA required
      if ((token as any)?.requiresMfa) {
        (session.user as any).requiresMfa = true;
        (session.user as any).username = (token as any).username;
        return session;
      }

      // Handle password change required
      if ((token as any)?.requiresPasswordChange) {
        (session.user as any).requiresPasswordChange = true;
        (session.user as any).username = (token as any).username;
        (session.user as any).accessToken = (token as any).accessToken;
        return session;
      }

      // Normal session — propagate tokens and profile
      if ((token as any)?.accessToken) {
        (session.user as any).accessToken = (token as any).accessToken;
        (session.user as any).refreshToken = (token as any).refreshToken;
      } else {
        (session as any).isError = true;
      }

      if ((token as any)?.userProfile) {
        (session.user as any).userProfile = (token as any).userProfile;
      }

      (session.user as any).grantedClaims = (token as any)?.accessToken
        ? (token as any).grantedClaims || []
        : undefined;
      (session.user as any).roles = (token as any)?.accessToken
        ? (token as any).roles || []
        : undefined;

      (session.user as any).requiresPasswordChange = false;
      (session.user as any).requiresMfa = false;
      (session as any).expiresAt = (token as any).expiresAt;

      return session;
    },
  },
  pages: {
    signIn: "/login",
  },
  providers: [
    Credentials({
      name: "Credentials",
      credentials: {
        username: { label: "Username", type: "text" },
        password: { label: "Password", type: "password" },
      },
      async authorize(credentials) {
        if (!credentials?.username || !credentials?.password) return null;

        const formdata = new FormData();
        formdata.append("username", credentials.username);
        formdata.append("password", credentials.password);

        try {
          const result = await authenticate(formdata);

          if (!result) return null;

          // Pass through MFA or password change requirements
          if (result.requiresMfa || result.requiresPasswordChange) {
            return result as any;
          }

          // Normal successful login
          if (!result.accessToken) return null;

          return {
            id: result.userProfile?.userId || "unknown",
            accessToken: result.accessToken,
            refreshToken: result.refreshToken,
            expiresAt: result.expiresAt,
            userProfile: result.userProfile,
            grantedClaims: result.grantedClaims,
            roles: result.roles,
            requiresPasswordChange: result.requiresPasswordChange || false,
            requiresMfa: false,
          } as any;
        } catch {
          return null;
        }
      },
    }),
  ],
};

export const getServerAuthSession = () => getServerSession(authOptions);
