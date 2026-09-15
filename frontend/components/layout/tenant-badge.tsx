"use client";

import { useSession } from "next-auth/react";
import { Building2 } from "lucide-react";
import { AgencySwitcher } from "@/components/layout/agency-switcher";

export function TenantBadge() {
  const { data: session } = useSession();
  const user = (session as any)?.user;
  const isSuperAdmin = user?.userProfile?.isSuperAdmin;

  return (
    <div className={isSuperAdmin ? "flex items-center" : "flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-muted/60 border text-xs"}>
      {isSuperAdmin ? (
        // A platform admin works across agencies, so the badge is a picker rather than a label —
        // otherwise there is no way to say which agency you are looking at.
        <AgencySwitcher />
      ) : (
        <>
          <Building2 className="h-3.5 w-3.5 text-green-700" />
          <span className="font-medium text-green-800">
            {user?.userProfile?.tenantName || "Agency"}
          </span>
        </>
      )}
    </div>
  );
}
