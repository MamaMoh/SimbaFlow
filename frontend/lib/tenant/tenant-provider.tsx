"use client";

import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { useSession } from "next-auth/react";
import { syncActingTenantCookie } from "@/lib/tenant/acting-tenant";

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
  permissions: string[];
  hasPermission: (permission: string) => boolean;
  isSuperAdmin: boolean;
  /**
   * The one account that answers for this agency — the admin created when the agency was set
   * up. Not a permission: see IRequireAgencyOwner on the server for why the distinction is
   * load-bearing rather than cosmetic.
   */
  isAgencyOwner: boolean;
  isLoading: boolean;
}

const TenantContext = createContext<TenantContextValue>({
  tenant: null,
  permissions: [],
  hasPermission: () => false,
  isSuperAdmin: false,
  isAgencyOwner: false,
  isLoading: true,
});

function readClaims(session: unknown): string[] {
  const s = session as any;
  const fromUser = s?.user?.grantedClaims;
  if (Array.isArray(fromUser)) return fromUser;
  if (Array.isArray(s?.permissions)) return s.permissions;
  return [];
}

/** The role name is the server's; see AgencyRoles on the other side. */
const AGENCY_OWNER = "AgencyOwner";

function readIsAgencyOwner(session: unknown): boolean {
  const s = session as any;
  if (s?.role === AGENCY_OWNER) return true;
  return s?.user?.roles?.includes?.(AGENCY_OWNER) === true;
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
  // Before anything below fetches: the proxy scopes every request by this cookie, and a stale
  // one sends the whole page to the wrong agency — or to none.
  syncActingTenantCookie();

  const { data: session, status } = useSession();
  const [tenant, setTenant] = useState<TenantInfo | null>(null);

  const permissions = useMemo(() => readClaims(session), [session]);
  const isSuperAdmin = useMemo(
    () => readIsSuperAdmin(session, permissions),
    [session, permissions]
  );
  const isAgencyOwner = useMemo(() => readIsAgencyOwner(session), [session]);
  const isLoading = status === "loading";

  useEffect(() => {
    if (!(session as any)?.user?.accessToken && !(session as any)?.accessToken) {
      return;
    }

    const tokenPayload = session as any;
    if (tokenPayload?.tenant) {
      setTenant(tokenPayload.tenant);
    }
  }, [session]);

  const hasPermission = (permission: string): boolean => {
    if (isLoading) return false;
    if (isSuperAdmin) return true;
    if (isAgencyOwner) return true;
    return permissions.includes(permission);
  };

  return (
    <TenantContext.Provider
      value={{
        tenant,
        permissions,
        hasPermission,
        isSuperAdmin,
        isAgencyOwner,
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
  const { hasPermission, permissions, isLoading, isSuperAdmin, isAgencyOwner } =
    useContext(TenantContext);
  return { hasPermission, permissions, isLoading, isSuperAdmin, isAgencyOwner };
}
