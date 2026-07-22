"use client";

import { useSession, signOut } from "next-auth/react";
import { useRouter } from "next/navigation";
import { useState, useEffect } from "react";

export function useAuth() {
  const { data: session, status } = useSession();
  const router = useRouter();
  const [isLoggingOut, setIsLoggingOut] = useState(false);
  const [isHydrated, setIsHydrated] = useState(false);
  const useMocks =
    process.env.NEXT_PUBLIC_USE_MOCKS === "true" ||
    process.env.NEXT_PUBLIC_USE_MOCKS === "1";

  useEffect(() => {
    setIsHydrated(true);
  }, []);

  const isLoading = useMocks ? false : status === "loading" || !isHydrated;
  const isAuthenticated = useMocks || !!session?.user?.accessToken;

  const logout = async () => {
    try {
      setIsLoggingOut(true);
// Add a small delay to ensure loading state is visible
      await new Promise(resolve => setTimeout(resolve, 500));
      
      // Add timeout to ensure logout completes
      const logoutPromise = signOut({ redirect: false });
      const timeoutPromise = new Promise((_, reject) => 
        setTimeout(() => reject(new Error('Logout timeout')), 5000)
      );
      
      await Promise.race([logoutPromise, timeoutPromise]);
router.push("/login");
      
    } catch (error) {
// Even if signOut fails or times out, redirect to login
      router.push("/login");
    } finally {
setIsLoggingOut(false);
    }
  };

  const user = session?.user;
  
  // Helper functions for user data
  const getDisplayName = () => {
    if (useMocks) return "Demo User";
    if (!isHydrated || !user) return "User";
    const profile = (user as any).userProfile;
    return profile?.fullName || profile?.username || "User";
  };

  const getEmail = () => {
    if (useMocks) return "demo@simbaflow.local";
    if (!isHydrated || !user) return "";
    const profile = (user as any).userProfile;
    return profile?.email || "";
  };

  const getUsername = () => {
    if (useMocks) return "demo";
    if (!isHydrated || !user) return "";
    const profile = (user as any).userProfile;
    return profile?.username || "";
  };

  const getUserId = () => {
    if (useMocks) return "mock-user";
    if (!isHydrated || !user) return "";
    const profile = (user as any).userProfile;
    return profile?.userId || "";
  };

  const getPermissions = () => {
    if (useMocks) {
      return [
        "candidate.read",
        "candidate.create",
        "workflow.view",
        "embassy.read",
        "lmis.read",
        "travel.read",
        "arrival.read",
        "commission.read",
        "accounting.read",
        "report.view",
        "staff.read",
        "role.read",
        "office.read",
        "partner.read",
        "workflow.configure",
        "tenant.manage",
        "system.admin",
      ];
    }
    if (!isHydrated || !user) return [];
    return (user as any).grantedClaims || [];
  };

  const hasPermission = (permission: string) => {
    if (useMocks) return true;
    return getPermissions().includes(permission);
  };

  const hasAnyPermission = (permissions: string[]) => {
    if (useMocks) return true;
    const userPerms = getPermissions();
    return permissions.some((perm) => userPerms.includes(perm));
  };

  const hasAllPermissions = (permissions: string[]) => {
    if (useMocks) return true;
    const userPerms = getPermissions();
    return permissions.every((perm) => userPerms.includes(perm));
  };

  const isFirstLogin = () => {
    if (useMocks) return false;
    if (!isHydrated || !user) return false;
    const profile = (user as any).userProfile;
    return profile?.isFirstLogin || false;
  };

  const isSuperAdmin = () => {
    if (useMocks) return true;
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
