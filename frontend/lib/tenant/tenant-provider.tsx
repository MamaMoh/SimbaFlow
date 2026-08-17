"use client";

import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { useSession } from "next-auth/react";

interface TenantInfo {
  id: string;
  name: string;
  slug: string;
  settings: {
    defaultLanguage: string;
    supportedLanguages: string[];
    defaultCurrency: string;
    supportedCurrencies: string[];
  };
}

interface TenantContextValue {
  tenant: TenantInfo | null;
  currentOffice: { id: string; name: string } | null;
  permissions: string[];
  hasPermission: (permission: string) => boolean;
  isSuperAdmin: boolean;
  isLoading: boolean;
}

const TenantContext = createContext<TenantContextValue>({
  tenant: null,
  currentOffice: null,
  permissions: [],
  hasPermission: () => false,
  isSuperAdmin: false,
  isLoading: true,
});

function readClaims(session: unknown): string[] {
  const s = session as any;
  const fromUser = s?.user?.grantedClaims;
  if (Array.isArray(fromUser)) return fromUser;
  if (Array.isArray(s?.permissions)) return s.permissions;
  return [];
}

function readIsSuperAdmin(session: unknown, claims: string[]): boolean {
  const s = session as any;
  if (s?.user?.userProfile?.isSuperAdmin === true) return true;
  if (s?.user?.isSuperAdmin === true) return true;
  if (s?.isSuperAdmin === true) return true;
  // Platform operators often carry system.admin without the profile flag
  return claims.includes("system.admin");
}

export function TenantProvider({ children }: { children: ReactNode }) {
  const { data: session, status } = useSession();
  const [tenant, setTenant] = useState<TenantInfo | null>(null);
  const [currentOffice, setCurrentOffice] = useState<{ id: string; name: string } | null>(null);

  const permissions = useMemo(() => readClaims(session), [session]);
  const isSuperAdmin = useMemo(
    () => readIsSuperAdmin(session, permissions),
    [session, permissions]
  );
  const isLoading = status === "loading";

  useEffect(() => {
    if (!(session as any)?.user?.accessToken && !(session as any)?.accessToken) {
      return;
    }

    const tokenPayload = session as any;
    if (tokenPayload?.tenant) {
      setTenant(tokenPayload.tenant);
    }
    if (tokenPayload?.office) {
      setCurrentOffice(tokenPayload.office);
    }
  }, [session]);

  const hasPermission = (permission: string): boolean => {
    if (isLoading) return false;
    if (isSuperAdmin) return true;
    if ((session as any)?.role === "AgencyOwner") return true;
    if ((session as any)?.user?.roles?.includes?.("AgencyOwner")) return true;
    return permissions.includes(permission);
  };

  return (
    <TenantContext.Provider
      value={{
        tenant,
        currentOffice,
        permissions,
        hasPermission,
        isSuperAdmin,
        isLoading,
      }}
    >
      {children}
    </TenantContext.Provider>
  );
}

export function useTenant() {
  return useContext(TenantContext);
}

export function usePermissions() {
  const { hasPermission, permissions, isLoading, isSuperAdmin } =
    useContext(TenantContext);
  return { hasPermission, permissions, isLoading, isSuperAdmin };
}
