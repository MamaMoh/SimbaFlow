"use client";

import { usePermissions } from "@/lib/tenant/tenant-provider";
import { useActingTenantId } from "@/lib/tenant/acting-tenant";

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

/**
 * Whether this person is working inside an agency right now — the question to ask before
 * fetching anything that belongs to one (its pipeline, candidates, dashboard).
 *
 * Agency staff always are. A platform administrator is only once they pick an agency in the
 * switcher; on "All agencies" there is none, and the server has no schema to read from. The
 * support desk never is.
 *
 * `undefined` while that is not known yet — the session is loading, or the switcher's choice has
 * not been read from localStorage — so a caller holding its request back until `true` never fires
 * one on the first render that the second would have stopped.
 */
export function useInsideAgency(): boolean | undefined {
  const { isSuperAdmin, isSupport, isLoading } = usePlatformStaff();
  const actingTenantId = useActingTenantId();
  if (isLoading) return undefined;
  if (isSupport) return false;
  if (!isSuperAdmin) return true;
  return actingTenantId === undefined ? undefined : actingTenantId !== null;
}
