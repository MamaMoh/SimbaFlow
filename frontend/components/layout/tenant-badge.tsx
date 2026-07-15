"use client";

import { useSession } from "next-auth/react";
import { Building2, Shield } from "lucide-react";

export function TenantBadge() {
  const { data: session } = useSession();
  const user = (session as any)?.user;
  const isSuperAdmin = user?.userProfile?.isSuperAdmin;

  return (
    <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-md bg-muted/60 border text-xs">
      {isSuperAdmin ? (
        <>
          <Shield className="h-3.5 w-3.5 text-amber-600" />
          <span className="font-medium text-amber-700">Platform Admin</span>
        </>
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
