"use client";

import { useEffect } from "react";
import { Button } from "@/components/ui/button";

/**
 * Route-level error boundary.
 *
 * A crashed page used to leave the user staring at a blank screen while nobody found out. This
 * shows them a way forward and reports the failure so it appears under Errors without anyone
 * having to phone it in.
 */
export default function RouteError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    fetch("/api/proxy/diagnostics/client-error", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        message: error.message || "Unknown client error",
        stack: error.stack ?? error.digest ?? null,
        path: typeof window !== "undefined" ? window.location.pathname : null,
      }),
      // Reporting is best-effort; it must never surface a second error to the user.
    }).catch(() => {});
  }, [error]);

  return (
    <div className="flex min-h-[60vh] flex-col items-center justify-center gap-4 p-8 text-center">
      <h1 className="text-xl font-semibold">This page didn&apos;t load</h1>
      <p className="max-w-md text-sm text-muted-foreground">
        Something went wrong on our side. The problem has been reported automatically — you can try
        again, and your work so far is not affected.
      </p>
      <div className="flex gap-3">
        <Button onClick={reset} className="bg-green-800 hover:bg-green-900">
          Try again
        </Button>
        <Button variant="outline" onClick={() => (window.location.href = "/overview")}>
          Back to dashboard
        </Button>
      </div>
    </div>
  );
}
