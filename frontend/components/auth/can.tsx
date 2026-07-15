"use client";

import { useAuth } from "@/hooks/use-auth";

export type CanMode = "any" | "all";

interface CanProps {
  /** Single permission or list (any = user needs one of; all = user needs every). */
  permission: string | string[];
  /** When permission is array: "any" = show if user has any; "all" = show if user has all. Default "any". */
  mode?: CanMode;
  /** Rendered when permission is granted. */
  children: React.ReactNode;
  /** Rendered when permission is denied. Default null (hide). */
  fallback?: React.ReactNode;
}

/**
 * Renders children only when the current user has the required permission(s).
 * Super admin (system.admin) is treated as having all permissions.
 */
export function Can({ permission, mode = "any", children, fallback = null }: CanProps) {
  const { hasPermission, hasAnyPermission, hasAllPermissions, isSuperAdmin } = useAuth();

  const allowed = isSuperAdmin()
    ? true
    : Array.isArray(permission)
      ? mode === "all"
        ? hasAllPermissions(permission)
        : hasAnyPermission(permission)
      : hasPermission(permission);

  return allowed ? <>{children}</> : <>{fallback}</>;
}
