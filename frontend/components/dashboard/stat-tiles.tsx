"use client";

import Link from "next/link";
import { Users, UserPlus, Banknote, AlertTriangle, Clock } from "lucide-react";
import type { DashboardMetrics } from "@/lib/api/dashboard";
import { cn } from "@/lib/utils";

const ETB = new Intl.NumberFormat("en-US", {
  maximumFractionDigits: 0,
});

type Tile = {
  key: string;
  label: string;
  value: string;
  icon: React.ElementType;
  href: string;
  accent: string;
  hint?: string;
};

export function StatTiles({
  metrics,
  isLoading,
}: {
  metrics?: DashboardMetrics;
  isLoading?: boolean;
}) {
  const tiles: Tile[] = [
    {
      key: "active",
      label: "Active candidates",
      value: metrics ? String(metrics.activeCandidates) : "—",
      icon: Users,
      href: "/candidates",
      accent: "text-sky-600 dark:text-sky-400 bg-sky-500/10",
    },
    {
      key: "new",
      label: "New this month",
      value: metrics ? String(metrics.newThisMonth) : "—",
      icon: UserPlus,
      href: "/candidates",
      accent: "text-emerald-600 dark:text-emerald-400 bg-emerald-500/10",
    },
    {
      key: "owed",
      label: "Commission owed (ETB)",
      value: metrics ? ETB.format(metrics.commissionsOwed) : "—",
      icon: Banknote,
      href: "/workflow/commissions",
      accent: "text-amber-600 dark:text-amber-400 bg-amber-500/10",
    },
    {
      key: "overdue",
      label: "Overdue candidates",
      value: metrics ? String(metrics.overdueCandidates) : "—",
      icon: Clock,
      href: "/my-work",
      accent: "text-orange-600 dark:text-orange-400 bg-orange-500/10",
    },
    {
      key: "exceptions",
      label: "Open exceptions",
      value: metrics ? String(metrics.openExceptions) : "—",
      icon: AlertTriangle,
      href: "/workflow/exceptions",
      accent: "text-rose-600 dark:text-rose-400 bg-rose-500/10",
    },
  ];

  return (
    <div className="grid grid-cols-2 gap-4 lg:grid-cols-5">
      {tiles.map((t) => {
        const Icon = t.icon;
        return (
          <Link
            key={t.key}
            href={t.href}
            className="group rounded-xl border bg-card p-4 shadow-sm transition hover:shadow-md hover:border-primary/40"
          >
            <div className="flex items-center justify-between">
              <span
                className={cn(
                  "inline-flex h-9 w-9 items-center justify-center rounded-lg",
                  t.accent
                )}
              >
                <Icon className="h-4.5 w-4.5" />
              </span>
            </div>
            <p
              className={cn(
                "mt-3 text-2xl font-bold tabular-nums tracking-tight",
                isLoading && "animate-pulse text-muted-foreground"
              )}
            >
              {t.value}
            </p>
            <p className="text-xs text-muted-foreground">{t.label}</p>
          </Link>
        );
      })}
    </div>
  );
}
