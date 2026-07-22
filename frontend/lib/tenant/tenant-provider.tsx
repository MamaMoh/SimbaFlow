"use client";

import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
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
  isLoading: boolean;
}

const TenantContext = createContext<TenantContextValue>({
  tenant: null,
  currentOffice: null,
  permissions: [],
  hasPermission: () => false,
  isLoading: true,
});

export function TenantProvider({ children }: { children: ReactNode }) {
  const { data: session } = useSession();
  const [tenant, setTenant] = useState<TenantInfo | null>(null);
  const [currentOffice, setCurrentOffice] = useState<{ id: string; name: string } | null>(null);
  const [permissions, setPermissions] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const useMocks =
    process.env.NEXT_PUBLIC_USE_MOCKS === "true" ||
    process.env.NEXT_PUBLIC_USE_MOCKS === "1";

  useEffect(() => {
    if (useMocks) {
      setPermissions([
        "candidate.read",
        "candidate.create",
        "workflow.view",
        "embassy.read",
        "lmis.read",
        "travel.read",
        "arrival.read",
        "commission.read",
        "system.admin",
      ]);
      setTenant({
        id: "mock-tenant",
        name: "Demo Agency",
        slug: "demo",
        settings: {
          defaultLanguage: "en",
          supportedLanguages: ["en"],
          defaultCurrency: "ETB",
          supportedCurrencies: ["ETB", "USD"],
        },
      });
      setCurrentOffice({ id: "11111111-1111-1111-1111-111111111001", name: "Head Office — Addis Ababa" });
      setIsLoading(false);
      return;
    }

    if (!(session as any)?.accessToken) {
      setIsLoading(false);
      return;
    }

    // Extract permissions and tenant info from session/token
    const tokenPayload = session as any;
    if (tokenPayload?.permissions) {
      setPermissions(tokenPayload.permissions);
    }
    if (tokenPayload?.tenant) {
      setTenant(tokenPayload.tenant);
    }
    if (tokenPayload?.office) {
      setCurrentOffice(tokenPayload.office);
    }

    setIsLoading(false);
  }, [session, useMocks]);

  const hasPermission = (permission: string): boolean => {
    if (useMocks) return true;
    if ((session as any)?.isSuperAdmin) return true;
    if ((session as any)?.role === "AgencyOwner") return true;
    return permissions.includes(permission);
  };

  return (
    <TenantContext.Provider
      value={{ tenant, currentOffice, permissions, hasPermission, isLoading }}
    >
      {children}
    </TenantContext.Provider>
  );
}

export function useTenant() {
  return useContext(TenantContext);
}

export function usePermissions() {
  const { hasPermission, permissions } = useContext(TenantContext);
  return { hasPermission, permissions };
}
