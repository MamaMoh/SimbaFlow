"use client";

import { useSession, signOut } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useState, useEffect } from "react";
import { MOCK_ACCESS_TOKEN, MOCK_GRANTED_CLAIMS, isMockAuthEnabled } from "@/lib/auth/mock-auth";

export function useAuth() {
  const { data: session, status } = useSession();
  const router = useRouter();
  const [isLoggingOut, setIsLoggingOut] = useState(false);
  const [isHydrated, setIsHydrated] = useState(false);
  const useMocks = isMockAuthEnabled();

  useEffect(() => {
    setIsHydrated(true);
  }, []);

  const accessToken = (session?.user as any)?.accessToken as string | undefined;
  const isMockSession = accessToken === MOCK_ACCESS_TOKEN;

  const isLoading = status === "loading" || !isHydrated;
  const isAuthenticated = !!accessToken;

  const logout = async () => {
    try {
      setIsLoggingOut(true);
      await new Promise((resolve) => setTimeout(resolve, 400));

      const logoutPromise = signOut({ redirect: false });
      const timeoutPromise = new Promise((_, reject) =>
        setTimeout(() => reject(new Error("Logout timeout")), 5000),
      );

      await Promise.race([logoutPromise, timeoutPromise]);
      router.push("/");
      router.refresh();
    } catch {
      router.push("/");
      router.refresh();
    } finally {
      setIsLoggingOut(false);
    }
  };

  const user = session?.user;

  const getDisplayName = () => {
    if (!isHydrated || !user) return "User";
    const profile = (user as any).userProfile;
    return profile?.fullName || profile?.username || "User";
  };

  const getEmail = () => {
    if (!isHydrated || !user) return "";
    const profile = (user as any).userProfile;
    return profile?.email || "";
  };

  const getUsername = () => {
    if (!isHydrated || !user) return "";
    const profile = (user as any).userProfile;
    return profile?.username || "";
  };

  const getUserId = () => {
    if (!isHydrated || !user) return "";
    const profile = (user as any).userProfile;
    return profile?.userId || "";
  };

  const getPermissions = () => {
    if (!isHydrated || !user) return [];
    if (useMocks || isMockSession) return [...MOCK_GRANTED_CLAIMS];
    return (user as any).grantedClaims || [];
  };

  const hasPermission = (permission: string) => {
    if (useMocks || isMockSession) return true;
    return getPermissions().includes(permission);
  };

  const hasAnyPermission = (permissions: string[]) => {
    if (useMocks || isMockSession) return true;
    const userPerms = getPermissions();
    return permissions.some((perm) => userPerms.includes(perm));
  };

  const hasAllPermissions = (permissions: string[]) => {
    if (useMocks || isMockSession) return true;
    const userPerms = getPermissions();
    return permissions.every((perm) => userPerms.includes(perm));
  };

  const isFirstLogin = () => {
    if (useMocks || isMockSession) return false;
    if (!isHydrated || !user) return false;
    const profile = (user as any).userProfile;
    return profile?.isFirstLogin || false;
  };

  const isSuperAdmin = () => {
    if (useMocks || isMockSession) return true;
    return hasPermission("system.admin");
  };

  const isAdmin = () => {
    return isSuperAdmin() || hasPermission("system.user");
  };

  const canAccess = (permission: string) => {
    return hasPermission(permission);
  };

  return {
    user,
    session,
    isLoading,
    isAuthenticated,
    isLoggingOut,
    logout,
    getDisplayName,
    getEmail,
    getUsername,
    getUserId,
    getPermissions,
    hasPermission,
    hasAnyPermission,
    hasAllPermissions,
    isFirstLogin,
    isSuperAdmin,
    isAdmin,
    canAccess,
  };
}
