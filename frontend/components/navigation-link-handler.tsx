"use client";

import { useEffect } from "react";
import { usePathname } from "next/navigation";
import { useNavigationLoadingStore } from "@/lib/stores/navigation-loading-store";

/**
 * Global handler that detects link clicks and shows loading immediately
 * This provides instant feedback when users click navigation links
 */
export function NavigationLinkHandler() {
  const { setLoading } = useNavigationLoadingStore();
  const pathname = usePathname();

  useEffect(() => {
    const handleClick = (e: MouseEvent) => {
      if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) {
        return;
      }

      const target = e.target as HTMLElement;
      const link = target.closest("a");

      if (!link || !link.href || link.target === "_blank" || link.hasAttribute("download")) {
        return;
      }

      const href = link.getAttribute("href");
      if (!href || href.startsWith("http") || href.startsWith("mailto:") || href.startsWith("#")) {
        return;
      }

      const normalizedHref = href.replace(window.location.origin, "").split("?")[0].split("#")[0];
      const normalizedPathname = pathname || "";

      if (normalizedHref !== normalizedPathname) {
        setLoading(true);
      }
    };

    document.addEventListener("click", handleClick, true);

    return () => {
      document.removeEventListener("click", handleClick, true);
    };
  }, [setLoading, pathname]);

  return null;
}
