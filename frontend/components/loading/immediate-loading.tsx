"use client";

import { useEffect } from "react";
import { usePathname } from "next/navigation";
import { useNavigationLoadingStore } from "@/lib/stores/navigation-loading-store";
import { LoadingSpinner } from "@/components/loading/loading-components";

export function ImmediateLoading() {
  const isLoading = useNavigationLoadingStore((s) => s.isLoading);

  if (!isLoading) return null;

  return (
    <div className="pointer-events-none absolute inset-0 z-30 flex items-center justify-center bg-background/70 backdrop-blur-[2px]">
      <div className="rounded-2xl border bg-card/95 px-6 py-5 shadow-xl">
        <LoadingSpinner size="lg" text="Opening page…" />
      </div>
    </div>
  );
}

export function NavigationLoading() {
  const pathname = usePathname();
  const { isLoading, setLoading } = useNavigationLoadingStore();

  useEffect(() => {
    if (!isLoading) return;
    const timer = window.setTimeout(() => setLoading(false), 250);
    return () => window.clearTimeout(timer);
  }, [pathname, isLoading, setLoading]);

  if (!isLoading) return null;

  return (
    <div className="pointer-events-none fixed inset-x-0 top-0 z-50 h-1 overflow-hidden bg-primary/10">
      <div className="h-full w-1/3 animate-pulse rounded-r-full bg-primary shadow-[0_0_20px_rgba(0,0,0,0.08)]" />
    </div>
  );
}
