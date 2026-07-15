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
      const target = e.target as HTMLElement;
      // Find the closest anchor tag
      const link = target.closest("a");
      
      if (link && link.href) {
        const href = link.getAttribute("href");
        // Only trigger for internal links (not external or special links)
        if (href && !href.startsWith("http") && !href.startsWith("mailto:") && !href.startsWith("#")) {
          // Normalize href to match pathname format (remove domain, ensure leading slash)
          const normalizedHref = href.replace(window.location.origin, "").split("?")[0].split("#")[0];
          const normalizedPathname = pathname || "";
          
          // Only show loading if navigating to a different page
          if (normalizedHref !== normalizedPathname) {
            // Show loading IMMEDIATELY on click - set synchronously for instant feedback
            setLoading(true);
          }
        }
      }
    };

    // Add click listener to document with capture phase for immediate detection
    document.addEventListener("click", handleClick, true);

    return () => {
      document.removeEventListener("click", handleClick, true);
    };
  }, [setLoading, pathname]);

  return null; // This component doesn't render anything
}

