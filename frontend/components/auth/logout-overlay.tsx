"use client";

import { useEffect, useState } from "react";
import { useAuth } from "@/hooks/use-auth";

export function LogoutOverlay() {
  const { isLoggingOut } = useAuth();
  const [showOverlay, setShowOverlay] = useState(false);

  // Debug logging
useEffect(() => {
    if (isLoggingOut) {
      // Show overlay immediately for testing
      setShowOverlay(true);
} else {
      setShowOverlay(false);
}
  }, [isLoggingOut]);

  if (!showOverlay) return null;

  return (
    <div className="fixed inset-0 z-50 bg-background/80 backdrop-blur-sm flex items-center justify-center">
      <div className="bg-card border rounded-lg p-8 shadow-lg max-w-md mx-4">
        <div className="text-center space-y-4">
          <div className="h-12 w-12 bg-primary/20 rounded-lg animate-pulse mx-auto" />
          <div>
            <h3 className="text-lg font-semibold">Logging Out</h3>
            <p className="text-sm text-muted-foreground mt-2">
              Please wait while we securely log you out...
            </p>
          </div>
          <div className="w-full bg-muted rounded-full h-2">
            <div className="bg-primary h-2 rounded-full animate-pulse" style={{ width: "60%" }} />
          </div>
        </div>
      </div>
    </div>
  );
}
