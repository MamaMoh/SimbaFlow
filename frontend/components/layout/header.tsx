"use client";

import { Button } from "@/components/ui/button";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { TenantBadge } from "@/components/layout/tenant-badge";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Bell,
  LogOut,
  Menu,
  PanelLeftClose,
  PanelLeft,
} from "lucide-react";
import { ThemeToggle } from "@/components/ui/theme-toggle";
import { LanguageSwitcher } from "@/components/i18n/language-switcher";
import { useSidebarStore } from "@/lib/stores/sidebar-store";
import { useAuth } from "@/hooks/use-auth";
import { useLocale } from "@/lib/i18n/locale-provider";
import { AppTooltip } from "../data-table/data-table-toolbar";
import { useEffect, useState } from "react";
import { ChangePasswordForm } from "@/components/forms/change-password-form";
import { Key } from "lucide-react";

export function Header() {
  const { isCollapsed, toggleCollapsed, toggleMobile } = useSidebarStore();
  const { user, logout, getDisplayName, getEmail, getUsername, isLoggingOut } = useAuth();
  const { chrome } = useLocale();
  const [isHydrated, setIsHydrated] = useState(false);
  const [showChangePassword, setShowChangePassword] = useState(false);

  useEffect(() => {
    setIsHydrated(true);
  }, []);

  const displayName = isHydrated ? getDisplayName() : "User";
  const email = isHydrated ? getEmail() : "";
  const username = isHydrated ? getUsername() : "";
  
  const initials = (() => {
    if (!isHydrated || !user) return "U";
    
    const profile = (user as any)?.userProfile;
    const fullName = profile?.fullName || displayName || "";
    const username = profile?.username || "";
    
    // Try to extract initials from full name (e.g., "John Smith" -> "JS")
    if (fullName) {
      const nameParts = fullName.trim().split(/\s+/);
      if (nameParts.length >= 2) {
        const first = nameParts[0].charAt(0);
        const last = nameParts[nameParts.length - 1].charAt(0);
        return `${first}${last}`.toUpperCase();
      } else if (nameParts.length === 1 && nameParts[0]) {
        return nameParts[0].substring(0, 2).toUpperCase();
      }
    }
    
    // Fallback to username initial
    return username ? username.charAt(0).toUpperCase() : "U";
  })();

  const handleLogout = () => {
    logout();
  };

  return (
    <header className="sticky top-0 z-40 border-b bg-card/80 shadow-md backdrop-blur supports-[backdrop-filter]:bg-card/60">
      <div className="flex h-18 items-center px-4 lg:px-6">
        <Button
          variant="ghost"
          size="sm"
          className="md:hidden mr-3"
          onClick={toggleMobile}
          aria-label="Toggle mobile menu"
        >
          <Menu className="h-5 w-5" />
        </Button>
        <AppTooltip content="Toggle collapse">
          <Button
            variant="ghost"
            size="sm"
            className="hidden md:flex mr-3"
            onClick={toggleCollapsed}
            aria-label={isCollapsed ? "Expand sidebar" : "Collapse sidebar"}
          >
            {isCollapsed ? (
              <PanelLeft className="h-5 w-5" />
            ) : (
              <PanelLeftClose className="h-5 w-5" />
            )}
          </Button>
        </AppTooltip>

        <div className="mr-6 flex items-center gap-3">
          <h1 className="font-semibold text-lg text-foreground">
            <span className="text-primary">
              SimbaFlow
            </span>
          </h1>
          <TenantBadge />
        </div>

        <div className="flex flex-1 items-center justify-between space-x-4 md:justify-end">
          <div className="w-full flex-1 md:w-auto md:flex-none">
            {/* Search functionality to be implemented */}
          </div>

          <nav className="flex items-center gap-2">
            <LanguageSwitcher variant="compact" />
            <ThemeToggle />
            <AppTooltip content={chrome.notifications}>
              <Button
                variant="ghost"
                size="sm"
                className="h-9 w-9"
                aria-label={chrome.notifications}
              >
                <Bell className="h-4 w-4" />
              </Button>
            </AppTooltip>

            <DropdownMenu>
              <AppTooltip content={chrome.profile}>
                <DropdownMenuTrigger asChild>
                  <Button
                    variant="ghost"
                    className="relative h-9 w-9 rounded-full p-0"
                    title={displayName}
                    aria-label={displayName}
                  >
                    <span className="inline-flex items-center justify-center h-9 w-9 rounded-full bg-primary text-primary-foreground text-sm font-semibold">
                      {initials}
                    </span>
                  </Button>
                </DropdownMenuTrigger>
              </AppTooltip>
              <DropdownMenuContent className="w-64" align="end" forceMount>
                <DropdownMenuLabel className="font-normal">
                  <div className="flex flex-col space-y-1">
                    <p className="text-sm font-medium leading-none">{displayName}</p>
                    <p className="text-xs leading-none text-muted-foreground">{email}</p>
                    {username && (
                      <p className="text-xs leading-none text-primary font-medium">@{username}</p>
                    )}
                  </div>
                </DropdownMenuLabel>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={() => setShowChangePassword(true)}>
                  <Key className="mr-2 h-4 w-4" />
                  <span>{chrome.changePassword}</span>
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem
                  onClick={handleLogout}
                  className="text-destructive focus:text-destructive"
                  disabled={isLoggingOut}
                >
                  {isLoggingOut ? (
                    <>
                      <div className="h-4 w-4 bg-destructive/20 rounded animate-pulse mr-2" />
                      <span>{chrome.loggingOut}</span>
                    </>
                  ) : (
                    <>
                      <LogOut className="mr-2 h-4 w-4" />
                      <span>{chrome.logOut}</span>
                    </>
                  )}
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </nav>
        </div>
      </div>
      <ChangePasswordForm open={showChangePassword} onOpenChange={setShowChangePassword} />
    </header>
  );
}
