import NextAuth, { DefaultSession, DefaultUser } from "next-auth";
import { JWT } from "next-auth/jwt";

interface UserProfile {
  userId: string;
  email: string;
  username: string;
  fullName: string;
  isFirstLogin: boolean;
}

interface CommonUserFields {
  accessToken: string;
  refreshToken: string;
  userProfile?: UserProfile;
  grantedClaims?: string[];
}

declare module "next-auth" {
  interface Session extends DefaultSession {
    user: (CommonUserFields & DefaultSession["user"]);
    isError?: boolean;
    expiresAt?: number;
  }
  interface User extends DefaultUser, CommonUserFields {}
}

declare module "next-auth/jwt" {
  interface JWT extends CommonUserFields {
    userId?: string;
    isError?: boolean;
    expiresAt?: number;
    lastSuccessfulRefresh?: number;
    lastRefreshError?: number;
  }
}
