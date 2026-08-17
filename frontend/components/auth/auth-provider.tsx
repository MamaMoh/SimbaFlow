"use client";

import { SessionProvider } from "next-auth/react";
import { TenantProvider } from "@/lib/tenant/tenant-provider";
import { SignalRProvider } from "@/lib/signalr/signalr-provider";
import { NotificationListener } from "@/components/notifications/notification-listener";

interface AuthProviderProps {
  children: React.ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  return (
    <SessionProvider>
      <TenantProvider>
        <SignalRProvider>
          <NotificationListener />
          {children}
        </SignalRProvider>
      </TenantProvider>
    </SessionProvider>
  );
}
