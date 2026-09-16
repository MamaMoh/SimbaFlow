"use client";

import { useSession } from "next-auth/react";
import { Building2, Globe } from "lucide-react";
import { AgencySwitcher } from "@/components/layout/agency-switcher";

export function TenantBadge() {
  const { data: session } = useSession();
  const profile = session?.user?.userProfile;

  // A platform admin works across agencies, so the badge is a picker rather than a label —
  // otherwise there is no way to say which agency you are looking at.
  if (profile?.isSuperAdmin) {
    return (
      <div className="flex items-center">
        <AgencySwitcher />
      </div>
    );
  }

  const agency = profile?.tenantName?.trim();

  return (
    <div className="flex items-center gap-1.5 rounded-md border bg-muted/60 px-2.5 py-1 text-xs">
      {agency ? (
        <>
          <Building2 className="h-3.5 w-3.5 shrink-0 text-green-700" />
          <span className="max-w-[16rem] truncate font-medium text-green-800" title={agency}>
            {agency}
          </span>
        </>
      ) : (
        // No agency at all — a platform account below SuperAdmin. Saying "Platform" is true;
        // the old fallback said "Agency", which named nothing and read as a broken label.
        <>
          <Globe className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
          <span className="font-medium text-muted-foreground">Platform</span>
        </>
      )}
    </div>
  );
}
