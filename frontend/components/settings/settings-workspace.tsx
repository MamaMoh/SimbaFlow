"use client";

import { cn } from "@/lib/utils";
import { SettingsSidebar } from "./settings-sidebar";

interface SettingsWorkspaceProps {
  children: React.ReactNode;
  className?: string;
}

export function SettingsWorkspace({ children, className }: SettingsWorkspaceProps) {
  return (
    <div className={cn("grid grid-cols-1 lg:grid-cols-[250px_1fr] gap-6", className)}>
      {/* Sidebar Section */}
      <aside className="lg:sticky lg:top-4 lg:self-start bg-card/60 backdrop-blur-md rounded-2xl border border-white/20 p-2 shadow-sm">
        <SettingsSidebar />
      </aside>

      {/* Main Content Area */}
      <main className="min-w-0 min-h-[60vh] bg-card/40 backdrop-blur-sm rounded-2xl border border-white/10 p-6 sm:p-8 shadow-sm animate-in fade-in slide-in-from-bottom-2 duration-500">
        <div className="max-w-4xl mx-auto">
          {children}
        </div>
      </main>
    </div>
  );
}
