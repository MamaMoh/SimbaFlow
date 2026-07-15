"use client";

import { useEffect, useState } from "react";

interface ProgressLoadingProps {
  message?: string;
  showProgress?: boolean;
}

export function ProgressLoading({ 
  message = "Loading...", 
  showProgress = true 
}: ProgressLoadingProps) {
  const [progress, setProgress] = useState(0);

  useEffect(() => {
    if (!showProgress) return;

    const interval = setInterval(() => {
      setProgress((prev) => {
        if (prev >= 90) return prev; // Stop at 90% until actual loading completes
        return prev + Math.random() * 15;
      });
    }, 200);

    return () => clearInterval(interval);
  }, [showProgress]);

  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-background">
      <div className="w-full max-w-md space-y-6 p-8">
        {/* Logo/Brand */}
        <div className="text-center">
          <div className="h-12 w-12 mx-auto mb-4 bg-primary rounded-lg flex items-center justify-center">
            <span className="text-primary-foreground font-bold text-xl">EA</span>
          </div>
          <h2 className="text-2xl font-bold text-foreground">SimbaFlow</h2>
          <p className="text-sm text-muted-foreground">Labour Export Agency</p>
        </div>

        {/* Loading Animation */}
        <div className="space-y-4">
          <div className="flex justify-center">
            <div className="h-8 w-8 bg-primary/20 rounded-lg animate-pulse" />
          </div>
          
          <p className="text-center text-sm text-muted-foreground">{message}</p>
          
          {/* Progress Bar */}
          {showProgress && (
            <div className="w-full bg-muted rounded-full h-2">
              <div 
                className="bg-primary h-2 rounded-full transition-all duration-300 ease-out"
                style={{ width: `${progress}%` }}
              />
            </div>
          )}
        </div>

        {/* Loading Tips */}
        <div className="text-center space-y-2">
          <p className="text-xs text-muted-foreground">
            💡 <strong>Tip:</strong> First-time loading may take longer due to compilation
          </p>
          <p className="text-xs text-muted-foreground">
            Subsequent visits will be much faster!
          </p>
        </div>
      </div>
    </div>
  );
}

export function CompilationLoading() {
  const [tips] = useState([
    "Compiling TypeScript...",
    "Bundling components...",
    "Optimizing assets...",
    "Preparing routes...",
    "Almost ready!"
  ]);
  
  const [currentTip, setCurrentTip] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => {
      setCurrentTip((prev) => (prev + 1) % tips.length);
    }, 2000);

    return () => clearInterval(interval);
  }, [tips.length]);

  return (
    <ProgressLoading 
      message={tips[currentTip]}
      showProgress={true}
    />
  );
}
