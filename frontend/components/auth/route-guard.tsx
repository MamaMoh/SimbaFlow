"use client";

import { usePathname } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";
import { navigation, type NavItem } from "@/components/layout/nav-items";
import { Button } from "@/components/ui/button";
import { ShieldAlert } from "lucide-react";
import Link from "next/link";

/**
 * Builds a route -> required-claims map from the navigation config so page
 * access always matches sidebar visibility. Longest-prefix match covers
 * detail routes (e.g. /workflow/[stageId] inherits /workflow claims).
 */
function collectRoutes(items: NavItem[], acc: Array<{ href: string; claims?: string[] }>) {
  for (const item of items) {
    if (item.href) acc.push({ href: item.href, claims: item.claims });
    if (item.children) collectRoutes(item.children, acc);
  }
  return acc;
}

const guardedRoutes = collectRoutes(navigation, []).sort(
  (a, b) => b.href.length - a.href.length,
);

export function RouteGuard({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const { isLoading, getPermissions, isSuperAdmin } = useAuth();

  // Wait for the session before deciding; avoids a false 403 flash.
  if (isLoading) return <>{children}</>;
  if (isSuperAdmin()) return <>{children}</>;

  const match = guardedRoutes.find(
    (r) => pathname === r.href || pathname.startsWith(`${r.href}/`),
  );

  // Routes not present in the navigation config are not claim-guarded here.
  if (!match?.claims?.length) return <>{children}</>;

  const claims = getPermissions();
  const allowed = match.claims.some((c: string) => claims.includes(c));

  if (!allowed) {
    return (
      <div className="flex min-h-[60vh] flex-col items-center justify-center gap-4 text-center">
        <ShieldAlert className="h-12 w-12 text-muted-foreground" />
        <div>
          <h2 className="text-lg font-semibold">Access denied</h2>
          <p className="text-sm text-muted-foreground max-w-sm">
            Your role does not have permission to view this page. Contact your
            administrator if you believe this is an error.
          </p>
        </div>
        <Button variant="outline" asChild>
          <Link href="/overview">Back to Dashboard</Link>
        </Button>
      </div>
    );
  }

  return <>{children}</>;
}
