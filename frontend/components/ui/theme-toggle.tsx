"use client";

import { Moon, Sun, Monitor, Palette, Check } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useThemeStore } from "@/lib/stores/theme-store";
import { AppTooltip } from "../data-table/data-table-toolbar";

export function ThemeToggle() {
  const { theme, setTheme } = useThemeStore();

  const themes = [
    { id: "ethiopian", label: "ET theme", icon: Palette },
    { id: "light", label: "Light", icon: Sun },
    { id: "dark", label: "Dark", icon: Moon },
    { id: "system", label: "System", icon: Monitor },
  ] as const;

  return (
    <DropdownMenu>
      <AppTooltip content="Change theme">
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="sm">
            {/* Button icon reflecting current theme */}
            <Sun
              className={`h-4 w-4 transition-all ${
                theme === "light" ? "scale-100" : "scale-0"
              } dark:scale-0`}
            />
            <Moon
              className={`absolute h-4 w-4 transition-all ${
                theme === "dark" ? "scale-100" : "scale-0"
              }`}
            />
            <Palette
              className={`absolute h-4 w-4 transition-all ${
                theme === "ethiopian"
                  ? "scale-100 text-[var(--sidebar-et-primary)]"
                  : "scale-0"
              }`}
            />
            <Monitor
              className={`absolute h-4 w-4 transition-all ${
                theme === "system" ? "scale-100" : "scale-0"
              }`}
            />
            <span className="sr-only">Toggle theme</span>
          </Button>
        </DropdownMenuTrigger>
      </AppTooltip>

      <DropdownMenuContent align="end" className="w-44">
        {themes.map((t) => {
          const Icon = t.icon;
          const isActive = theme === t.id;

          return (
            <DropdownMenuItem
              key={t.id}
              onClick={() => setTheme(t.id)}
              className={`flex items-center justify-between ${
                isActive
                  ? "bg-accent/20 text-accent-foreground font-semibold"
                  : ""
              }`}
            >
              <div className="flex items-center gap-2">
                <Icon
                  className={`h-4 w-4 ${
                    t.id === "ethiopian"
                      ? "text-[var(--sidebar-et-primary)]"
                      : ""
                  }`}
                />
                <span>{t.label}</span>
              </div>
              {isActive && <Check className="h-4 w-4 text-accent-foreground" />}
            </DropdownMenuItem>
          );
        })}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
