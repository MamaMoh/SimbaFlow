"use client";

import { usePermissions } from "@/lib/tenant/tenant-provider";

/** The claim the server grants the platform support desk (and, with everything else, the admins). */
export const PLATFORM_SUPPORT_CLAIM = "platform.support";

/**
 * Who on the platform team this is.
 *
 * Administrators (SuperAdmin, PlatformAdmin) can change agencies and billing and work inside an
 * agency. The support desk (PlatformSupport) can see every agency and help it, but changes none of
 * them. The server decides both from the role; this only chooses what to draw.
 */
export function usePlatformStaff() {
  const { isSuperAdmin, permissions, isLoading } = usePermissions();
  const isSupport = !isSuperAdmin && permissions.includes(PLATFORM_SUPPORT_CLAIM);
  return { isSuperAdmin, isSupport, isStaff: isSuperAdmin || isSupport, isLoading };
}
