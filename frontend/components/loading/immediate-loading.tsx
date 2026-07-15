"use client";

import { useEffect, useRef, useState } from "react";
import { usePathname } from "next/navigation";
import { useNavigationLoadingStore } from "@/lib/stores/navigation-loading-store";
import { LoadingSpinner } from "@/components/loading/loading-components";

export function ImmediateLoading() {
  return null;
}

export function NavigationLoading() {
  const [isNavigating, setIsNavigating] = useState(false);
  const pathname = usePathname();

  useEffect(() => {
    setIsNavigating(true);
    
    // Use requestAnimationFrame to ensure immediate display
    const frame = requestAnimationFrame(() => {
      const timer = setTimeout(() => {
        setIsNavigating(false);
      }, 50);
      
      return () => clearTimeout(timer);
    });

    return () => {
      cancelAnimationFrame(frame);
    };
  }, [pathname]);

  if (!isNavigating) return null;

  return (
    <div className="fixed top-0 left-0 right-0 z-40 h-1 bg-primary/20">
      <div className="h-full bg-primary animate-pulse" style={{ width: "30%" }} />
    </div>
  );
}
