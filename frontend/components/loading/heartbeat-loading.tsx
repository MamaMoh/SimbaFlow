"use client";

import { cn } from "@/lib/utils";

interface HeartbeatLoadingProps {
  size?: "sm" | "md" | "lg";
  className?: string;
}

/** Unified loading indicator (circle spinner). Kept name for backward compatibility. */
export function HeartbeatLoading({ size = "md", className = "" }: HeartbeatLoadingProps) {
  const sizeClasses = {
    sm: "h-4 w-4 border-2",
    md: "h-6 w-6 border-2",
    lg: "h-8 w-8 border-2",
  };
  return (
    <div
      role="status"
      aria-label="Loading"
      className={cn(
        "inline-flex shrink-0 animate-spin rounded-full border-primary border-t-transparent",
        sizeClasses[size],
        className
      )}
    />
  );
}

