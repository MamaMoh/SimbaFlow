"use client";

import { usePathname } from "next/navigation";
import Link from "next/link";
import { cn } from "@/lib/utils";
import { Calendar } from "lucide-react";
import { Button } from "@/components/ui/button";

interface SettingsSidebarProps {
  className?: string;
}

export function SettingsSidebar({ className }: SettingsSidebarProps) {
  const pathname = usePathname();

  const settingTypes = [
    {
      name: "Appointments",
      href: "/settings",
      icon: Calendar,
      isActive: pathname === "/settings" || pathname === "/settings/appointments",
    },
  ];

  return (
    <div className={cn("flex flex-col gap-1 w-full sm:w-64", className)}>
      <div className="px-3 py-2">
        <h2 className="mb-2 px-4 text-sm font-semibold tracking-tight text-muted-foreground uppercase">
          General Settings
        </h2>
        <div className="space-y-1">
          {settingTypes.map((type) => (
            <Link key={type.name} href={type.href}>
              <Button
                variant={type.isActive ? "secondary" : "ghost"}
                className={cn(
                  "w-full justify-start gap-3 rounded-lg px-4 py-2 text-sm font-medium transition-all duration-200",
                  type.isActive
                    ? "bg-primary/10 text-primary hover:bg-primary/20"
                    : "text-muted-foreground hover:bg-muted/50 hover:text-foreground"
                )}
              >
                <type.icon className={cn("h-4 w-4", type.isActive ? "text-primary" : "text-muted-foreground")} />
                {type.name}
              </Button>
            </Link>
          ))}
        </div>
      </div>
    </div>
  );
}
