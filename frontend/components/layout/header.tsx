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
  Search,
} from "lucide-react";
import { useCommandPalette } from "@/lib/stores/command-store";
import { ThemeToggle } from "@/components/ui/theme-toggle";
import { useSidebarStore } from "@/lib/stores/sidebar-store";
import { useAuth } from "@/hooks/use-auth";
import { AppTooltip } from "../data-table/data-table-toolbar";
import { useEffect, useState } from "react";
import { ChangePasswordForm } from "@/components/forms/change-password-form";
import { Key } from "lucide-react";

export function Header() {
  const { isCollapsed, toggleCollapsed, toggleMobile } = useSidebarStore();
  const openCommand = useCommandPalette((s) => s.setOpen);
  const { user, logout, getDisplayName, getEmail, getUsername, isLoggingOut } = useAuth();
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
            <button
              type="button"
              onClick={() => openCommand(true)}
              className="group flex h-9 w-full items-center gap-2 rounded-lg border bg-background/60 px-3 text-sm text-muted-foreground shadow-sm transition hover:border-primary/40 hover:text-foreground md:w-64"
              aria-label="Search and navigate"
            >
              <Search className="h-4 w-4" />
              <span className="flex-1 text-left">Search…</span>
              <kbd className="pointer-events-none hidden items-center gap-0.5 rounded border bg-muted px-1.5 font-mono text-[10px] font-medium text-muted-foreground sm:inline-flex">
                ⌘K
              </kbd>
            </button>
          </div>

          <nav className="flex items-center space-x-2">
            <ThemeToggle />
            <AppTooltip content="Notifications">
              <Button
                variant="ghost"
                size="sm"
                className="h-9 w-9"
                aria-label="Notifications"
              >
                <Bell className="h-4 w-4" />
              </Button>
            </AppTooltip>

            <DropdownMenu>
              <AppTooltip content="Profile">
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
                  <span>Change Password</span>
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
                      <span>Logging out...</span>
                    </>
                  ) : (
                    <>
                      <LogOut className="mr-2 h-4 w-4" />
                      <span>Log out</span>
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
