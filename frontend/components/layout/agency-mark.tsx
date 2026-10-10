"use client";

import { useState, type ReactNode } from "react";
import { brandingMark, logoUrl, useAgencyBranding } from "@/lib/api/branding";

/**
 * The agency's own logo, in the top bar.
 *
 * Both the badge an agency user sees and the switcher a platform administrator uses showed a
 * generic building icon, so the one place where "which agency am I in?" has to be answered at a
 * glance answered it with the same picture for every agency. The branding request carries the
 * acting agency, so a platform administrator who has switched sees that agency's mark.
 *
 * Falls back to whatever the caller was drawing before: no logo uploaded, a logo that cannot be
 * served, or a platform administrator standing outside any agency.
 */
export function AgencyMark({
  fallback,
  enabled = true,
  className = "h-4 w-auto max-w-10 object-contain",
}: {
  fallback: ReactNode;
  enabled?: boolean;
  className?: string;
}) {
  const { branding } = useAgencyBranding(enabled);
  const [broken, setBroken] = useState(false);
  const path = enabled ? brandingMark(branding) : null;

  if (!path || broken) return <>{fallback}</>;

  return (
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={logoUrl(path)}
      alt=""
      onError={() => setBroken(true)}
      className={className}
    />
  );
}
