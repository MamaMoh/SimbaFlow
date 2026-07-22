"use client";

import { Spinner } from "@/components/ui/spinner";
import { cn } from "@/lib/utils";

interface LoadingSpinnerProps {
  size?: "sm" | "md" | "lg";
  text?: string;
  className?: string;
}

/** Shared spinner used across pages, tables, and route transitions. */
export function LoadingSpinner({ size = "md", text = "Loading...", className }: LoadingSpinnerProps) {
  const spinnerSize = size === "sm" ? "sm" : size === "lg" ? "lg" : "md";

  return (
    <div className={cn("flex flex-col items-center justify-center gap-3", className)}>
      <Spinner size={spinnerSize === "md" ? 32 : spinnerSize} label={text || "Loading..."} />
      {text ? <p className="text-sm text-muted-foreground">{text}</p> : null}
    </div>
  );
}

/** Centered content loader for page sections and data tables. */
export function ContentLoading({ text = "Loading...", className }: { text?: string; className?: string }) {
  return (
    <div className={cn("flex min-h-[220px] w-full items-center justify-center p-10", className)}>
      <LoadingSpinner size="lg" text={text} />
    </div>
  );
}

export function PageLoading() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-background">
      <LoadingSpinner size="lg" text="Loading application…" />
    </div>
  );
}

export function DashboardLoading() {
  return (
    <div className="flex h-screen bg-gradient-to-br from-background to-muted/40">
      {/* Sidebar skeleton */}
      <div className="w-72 bg-card/95 border-r shadow-xl p-4">
        <div className="space-y-4">
          {/* Logo skeleton */}
          <div className="h-10 bg-muted animate-pulse rounded-lg mb-6" />
          
          {/* Navigation items skeleton */}
          <div className="space-y-2">
            {[...Array(8)].map((_, i) => (
              <div key={i} className="h-8 bg-muted animate-pulse rounded-xl" />
            ))}
          </div>
          
          {/* User info skeleton */}
          <div className="mt-auto pt-4 border-t">
            <div className="h-12 bg-muted animate-pulse rounded-xl" />
          </div>
        </div>
      </div>
      
      {/* Main content skeleton */}
      <div className="flex-1 flex flex-col">
        {/* Header skeleton */}
        <div className="h-18 border-b bg-card/80 p-4">
          <div className="flex items-center justify-between">
            <div className="h-6 w-48 bg-muted animate-pulse rounded" />
            <div className="flex space-x-2">
              <div className="h-9 w-9 bg-muted animate-pulse rounded-full" />
              <div className="h-9 w-9 bg-muted animate-pulse rounded-full" />
            </div>
          </div>
        </div>
        
        {/* Content skeleton */}
        <main className="flex-1 p-8">
          <div className="space-y-6">
            {/* Page title skeleton */}
            <div className="space-y-2">
              <div className="h-8 w-64 bg-muted animate-pulse rounded" />
              <div className="h-4 w-96 bg-muted animate-pulse rounded" />
            </div>
            
            {/* Cards skeleton */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
              {[...Array(4)].map((_, i) => (
                <div key={i} className="bg-card border rounded-lg p-6">
                  <div className="space-y-3">
                    <div className="h-4 w-24 bg-muted animate-pulse rounded" />
                    <div className="h-8 w-16 bg-muted animate-pulse rounded" />
                    <div className="h-3 w-20 bg-muted animate-pulse rounded" />
                  </div>
                </div>
              ))}
            </div>
            
            {/* Table skeleton */}
            <div className="bg-card border rounded-lg p-6">
              <div className="space-y-4">
                <div className="h-6 w-32 bg-muted animate-pulse rounded" />
                <div className="space-y-3">
                  {[...Array(6)].map((_, i) => (
                    <div key={i} className="h-12 bg-muted animate-pulse rounded" />
                  ))}
                </div>
              </div>
            </div>
          </div>
        </main>
      </div>
    </div>
  );
}

export function CalendarLoading() {
  return (
    <div className="space-y-6">
      {/* Page header skeleton */}
      <div className="space-y-2">
        <div className="h-8 w-64 bg-muted animate-pulse rounded" />
        <div className="h-4 w-96 bg-muted animate-pulse rounded" />
      </div>
      
      {/* Calendar skeleton */}
      <div className="bg-card border rounded-lg shadow-lg p-6">
        {/* Calendar header skeleton */}
        <div className="flex items-center justify-between mb-6">
          <div className="h-8 w-48 bg-muted animate-pulse rounded" />
          <div className="flex space-x-2">
            <div className="h-9 w-9 bg-muted animate-pulse rounded" />
            <div className="h-9 w-9 bg-muted animate-pulse rounded" />
            <div className="h-9 w-24 bg-muted animate-pulse rounded" />
          </div>
        </div>
        
        {/* Calendar grid skeleton */}
        <div className="space-y-2">
          {/* Day headers */}
          <div className="grid grid-cols-7 gap-2 mb-2">
            {[...Array(7)].map((_, i) => (
              <div key={i} className="h-8 bg-muted/50 animate-pulse rounded" />
            ))}
          </div>
          
          {/* Calendar days */}
          <div className="grid grid-cols-7 gap-2">
            {[...Array(35)].map((_, i) => (
              <div key={i} className="h-20 bg-muted/30 animate-pulse rounded" />
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

export function TableLoading() {
  return (
    <div className="space-y-6">
      {/* Page header skeleton */}
      <div className="space-y-2">
        <div className="h-8 w-64 bg-muted animate-pulse rounded" />
        <div className="h-4 w-96 bg-muted animate-pulse rounded" />
      </div>
      
      {/* Table skeleton */}
      <div className="bg-card border rounded-lg shadow-lg">
        {/* Table header skeleton */}
        <div className="border-b p-6">
          <div className="flex items-center justify-between">
            <div className="h-6 w-32 bg-muted animate-pulse rounded" />
            <div className="flex space-x-2">
              <div className="h-9 w-24 bg-muted animate-pulse rounded" />
              <div className="h-9 w-9 bg-muted animate-pulse rounded" />
            </div>
          </div>
        </div>
        
        {/* Table content skeleton */}
        <div className="p-6">
          <div className="space-y-4">
            {/* Table rows skeleton */}
            {[...Array(8)].map((_, i) => (
              <div key={i} className="flex items-center space-x-4">
                <div className="h-4 w-8 bg-muted animate-pulse rounded" />
                <div className="h-4 w-32 bg-muted animate-pulse rounded flex-1" />
                <div className="h-4 w-24 bg-muted animate-pulse rounded" />
                <div className="h-4 w-20 bg-muted animate-pulse rounded" />
                <div className="h-8 w-16 bg-muted animate-pulse rounded" />
              </div>
            ))}
          </div>
          
          {/* Pagination skeleton */}
          <div className="mt-6 flex items-center justify-between">
            <div className="h-4 w-32 bg-muted animate-pulse rounded" />
            <div className="flex space-x-2">
              {[...Array(5)].map((_, i) => (
                <div key={i} className="h-8 w-8 bg-muted animate-pulse rounded" />
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
