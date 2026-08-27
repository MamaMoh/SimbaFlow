import type React from "react";
import { Suspense } from "react";
import { Sidebar } from "@/components/layout/sidebar";
import { Header } from "@/components/layout/header";
import { LoadingSpinner } from "@/components/loading/loading-components";
import { ImmediateLoading } from "@/components/loading/immediate-loading";
import { RouteGuard } from "@/components/auth/route-guard";
import { CommandPalette } from "@/components/command/command-palette";
import { PointerEventsGuard } from "@/components/layout/pointer-events-guard";

export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="flex h-screen bg-gradient-to-br from-background to-muted/40">
      <CommandPalette />
      <PointerEventsGuard />
      <Sidebar className="bg-card/95 border-r shadow-xl" />
      <div className="flex-1 flex flex-col min-w-0 bg-background/90">
        <Header />
        <main className="flex-1 overflow-auto p-4 md:p-6 lg:p-8 bg-muted/60 dark:bg-background border-l border-border/40 shadow-inner min-h-0">
          <ImmediateLoading />
          <Suspense fallback={<div className="flex min-h-[50vh] items-center justify-center"><LoadingSpinner size="lg" text="Loading..." /></div>}>
            <RouteGuard>{children}</RouteGuard>
          </Suspense>
        </main>
      </div>
    </div>
  );
}
