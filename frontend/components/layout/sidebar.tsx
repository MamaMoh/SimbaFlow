"use client";

import React from "react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { X, LogOut, Globe, ChevronRight } from "lucide-react";
import { useSidebarStore } from "@/lib/stores/sidebar-store";
import { useAuth } from "@/hooks/use-auth";
import Link from "next/link";
import { usePathname } from "next/navigation";
// import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
// Avoid strict coupling to backend User type; use relaxed typing
import {
  navigation as baseNavigation,
  type NavItem,
  filterNavigationByClaims,
} from "@/components/layout/nav-items";
import { useState, useRef, useEffect, useMemo } from "react";

/** Returns the top-level parent nav item name whose subtree contains the current path (for opening on expand). */
function getParentNameForActivePath(
  pathname: string,
  items: NavItem[],
): string | null {
  for (const item of items) {
    if (!item.children?.length) continue;
    const pathMatchesInSubtree = (nav: NavItem): boolean => {
      if (nav.isSeparator) return false;
      if (
        nav.href &&
        (pathname === nav.href || pathname.startsWith(nav.href + "/"))
      )
        return true;
      if (typeof nav.isActive === "function" && nav.isActive(pathname))
        return true;
      if (nav.children) return nav.children.some(pathMatchesInSubtree);
      return false;
    };
    if (item.children.some(pathMatchesInSubtree)) return item.name;
  }
  return null;
}

interface SidebarProps {
  className?: string;
}

function UserInfo() {
  const { getDisplayName, getEmail, getUsername } = useAuth();
  const [isHydrated, setIsHydrated] = useState(false);

  useEffect(() => {
    setIsHydrated(true);
  }, []);

  const displayName = isHydrated ? getDisplayName() : "User";
  const email = isHydrated ? getEmail() : "";
  const username = isHydrated ? getUsername() : "";

  return (
    <div className="flex flex-col space-y-0.5 max-w-full">
      <p className="text-xs sm:text-sm font-semibold text-gray-900 dark:text-gray-100 truncate">
        {displayName}
      </p>
      <p className="text-[10px] sm:text-xs text-gray-500 dark:text-gray-400 truncate">
        {email}
      </p>
      {username && (
        <p className="text-[10px] sm:text-xs font-medium text-primary">
          @{username}
        </p>
      )}
    </div>
  );
}

function NavItemWithCollapse({
  userClaims,
  item,
  isCollapsed,
  pathname,
  closeMobile,
  isChild = false,
  openParentName,
  onParentToggle,
}: {
  userClaims: string[];
  item: NavItem;
  isCollapsed: boolean;
  pathname: string;
  closeMobile: () => void;
  isChild?: boolean;
  openParentName?: string | null;
  onParentToggle?: (name: string) => void;
}) {
  // MOVE ALL HOOKS BEFORE THE EARLY RETURN
  // For top-level parents (not children), use controlled state
  // For nested parents (children), use local state
  const isTopLevelParent =
    !isChild && item.children && item.children.length > 0;

  // Local state for nested parents
  const [localIsOpen, setLocalIsOpen] = useState(false);
  const [isMounted, setIsMounted] = useState(false);

  // Determine if this item should be open
  const actualIsOpen = isTopLevelParent
    ? openParentName === item.name
    : localIsOpen;

  const contentRef = useRef<HTMLDivElement>(null);
  const [height, setHeight] = useState("0px");

  // Ensure client-side hydration matches server
  useEffect(() => {
    setIsMounted(true);
  }, []);

  // When sidebar is collapsed, close this submenu so it stays closed when expanded again
  useEffect(() => {
    if (isCollapsed) setLocalIsOpen(false);
  }, [isCollapsed]);

  // Pre-process children to filter out separators that shouldn't be shown
  const processedChildren = useMemo(() => {
    if (!item.children) return [];
    return item.children
      .map((child, index) => {
        if (child.isSeparator) {
          // Check if there are any visible items after this separator
          const hasItemsAfter = item.children
            ?.slice(index + 1)
            .some(
              (nextChild) =>
                !nextChild.isSeparator &&
                (!nextChild.claims ||
                  nextChild.claims.length === 0 ||
                  nextChild.claims.some((c) => userClaims.includes(c))),
            );
          return hasItemsAfter ? child : null;
        }
        return child;
      })
      .filter((child) => child !== null) as NavItem[];
  }, [item.children, userClaims]);

  useEffect(() => {
    if (contentRef.current && isMounted) {
      setHeight(actualIsOpen ? `${contentRef.current.scrollHeight}px` : "0px");
    }
  }, [actualIsOpen, processedChildren, isMounted]);

  // NOW CHECK IF USER HAS ACCESS (AFTER ALL HOOKS)
  if (item.isSeparator) {
    if (isCollapsed) return null;
    return (
      <div className="pt-4 pb-1 px-2">
        {item.sectionLabel && (
          <p className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground/70">
            {item.sectionLabel}
          </p>
        )}
        <div className="h-px bg-gradient-to-r from-transparent via-border/40 to-transparent mt-1" />
      </div>
    );
  }

  if (
    item.claims &&
    item.claims.length &&
    !item.claims.some((c) => userClaims.includes(c))
  )
    return null;

  if (item.children && item.children.length) {
    const handleToggle = () => {
      if (isTopLevelParent && onParentToggle) {
        // Toggle: if already open, close it (pass empty string); otherwise open it (closes others automatically)
        onParentToggle(actualIsOpen ? "" : item.name);
      } else {
        // For nested parents, use local state
        setLocalIsOpen(!localIsOpen);
      }
    };

    return (
      <div key={item.name} className={cn(isChild && "ml-6")}>
        {/* Parent nav item button (has children and toggles collapse) */}
        <Tooltip>
          <TooltipTrigger asChild>
            <Button
              variant="ghost"
              className={cn(
                "group relative w-full rounded-xl transition-all duration-300 cursor-pointer",
                "hover:bg-primary/5 dark:hover:bg-primary/10 border border-transparent hover:border-white/20",
                actualIsOpen && "bg-white/50 dark:bg-white/10 border-white/20",
                isCollapsed
                  ? "md:justify-center md:px-2"
                  : "justify-between px-3",
              )}
              onClick={handleToggle}
            >
              {/* Active indicator bar (visible on hover/open, hidden when collapsed) */}
              {!isCollapsed && (
                <span
                  aria-hidden
                  className={cn(
                    "absolute top-1/2 -translate-y-1/2 h-6 w-1 rounded-full bg-primary/80 transition-all opacity-0 scale-0 group-hover:opacity-100 group-hover:scale-100",
                    actualIsOpen && "opacity-100 scale-100",
                    isChild ? "left-1" : "left-1",
                  )}
                />
              )}
              {/* Left group: icon + label */}
              <div className="flex items-center gap-2">
                {/* Icon wrapper for parent item */}
                <span
                  className={cn(
                    " flex h-8 w-8 items-center justify-center rounded-lg transition-colors duration-300 transition-transform group-hover:translate-x-0.5",
                    actualIsOpen
                      ? "bg-primary/10 text-primary"
                      : "bg-white/40 dark:bg-white/5 text-foreground/80",
                    isCollapsed && "md:mr-0",
                  )}
                >
                  <item.icon className="h-4 w-4" />
                </span>
                {/* Item label (hidden in collapsed sidebar) */}
                {!isCollapsed && (
                  <span className="transition-all duration-300 truncate text-sm font-medium group-hover:translate-x-0.5 flex-1 text-left">
                    {item.name}
                  </span>
                )}
              </div>
              {/* Right chevron toggle for children (hidden in collapsed sidebar) */}
              {!isCollapsed && (
                <div
                  className={cn(
                    "transition-transform duration-300",
                    actualIsOpen ? "rotate-90" : "rotate-0",
                  )}
                >
                  <ChevronRight className="h-4 w-4" />
                </div>
              )}
            </Button>
          </TooltipTrigger>
          <TooltipContent side="right" className="z-[100]">
            <p>{item.name}</p>
          </TooltipContent>
        </Tooltip>

        {/* Collapsible children container */}
        <div
          ref={contentRef}
          style={{
            height: isMounted ? height : "0px",
          }}
          className="overflow-hidden transition-[height] duration-300 ease-in-out  flex flex-col"
        >
          {processedChildren.map((child, index) => {
            // Render separator
            if (child.isSeparator) {
              return (
                <div
                  key={`separator-${index}`}
                  className={cn(
                    "px-3 py-2 mt-2 mb-1 text-xs font-semibold text-muted-foreground uppercase tracking-wider",
                    isCollapsed && "hidden",
                  )}
                >
                  {child.name}
                </div>
              );
            }
            // Render regular nav item
            return (
              <NavItemWithCollapse
                key={child.name}
                userClaims={userClaims}
                item={child}
                isCollapsed={isCollapsed}
                pathname={pathname}
                closeMobile={closeMobile}
                isChild={true}
                openParentName={openParentName}
                onParentToggle={onParentToggle}
              />
            );
          })}
        </div>
      </div>
    );
  }

  // Exact match only: prevents parent "Lab orders" from being active when on a child route (e.g. sample-collection, technician-assigning).
  // Optional isActive() lets e.g. "Service limits" be active only on policy detail page, not on the list (same href as "Insurance Policies").
  const isActive =
    typeof item.isActive === "function"
      ? item.isActive(pathname)
      : pathname === item.href;

  return (
    <div key={item.name} className={cn(isChild && "ml-6")}>
      {/* Leaf nav item (no children) */}
      <Tooltip>
        <TooltipTrigger asChild>
          <Link href={item.href || "#"} onClick={closeMobile}>
            <Button
              variant="ghost"
              className={cn(
                "group relative w-full rounded-xl transition-all duration-300 cursor-pointer",
                "hover:bg-primary/5 dark:hover:bg-primary/10 border border-transparent hover:border-white/20",
                isCollapsed
                  ? "md:justify-center md:px-2"
                  : "justify-start px-3",
                isActive && "bg-white/50 dark:bg-white/10 border-white/20",
              )}
            >
              {/* Active indicator for leaf item */}
              {!isCollapsed && (
                <span
                  aria-hidden
                  className={cn(
                    "absolute top-1/2 -translate-y-1/2 h-6 w-1 rounded-full bg-primary/80 transition-all opacity-0 scale-0 group-hover:opacity-100 group-hover:scale-100",
                    isActive && "opacity-100 scale-100",
                    isChild ? "left-1" : "left-1",
                  )}
                />
              )}
              {/* Icon wrapper for leaf item */}
              <span
                className={cn(
                  " flex h-8 w-8 items-center justify-center rounded-lg transition-colors duration-300 transition-transform group-hover:translate-x-0.5",
                  isActive
                    ? "bg-primary/10 text-primary"
                    : "bg-white/40 dark:bg-white/5 text-foreground/80",
                  isCollapsed && "md:mr-0",
                )}
              >
                <item.icon className="h-4 w-4" />
              </span>
              {/* Leaf label (hidden in collapsed sidebar) */}
              {!isCollapsed && (
                <span className="transition-all duration-300 truncate text-sm font-medium group-hover:translate-x-0.5 flex-1 text-left">
                  {item.name}
                </span>
              )}
            </Button>
          </Link>
        </TooltipTrigger>
        <TooltipContent side="right" className="z-[100]">
          <p>{item.name}</p>
        </TooltipContent>
      </Tooltip>
    </div>
  );
}

export function Sidebar({ className }: SidebarProps) {
  const { isCollapsed, isMobileOpen, closeMobile } = useSidebarStore();
  const { user, logout, getPermissions, isSuperAdmin, isLoggingOut } =
    useAuth();
  const pathname = usePathname();

  const userClaims = getPermissions();
  const navItems = filterNavigationByClaims(
    baseNavigation,
    userClaims as any,
    isSuperAdmin(),
  );

  // Accordion state: track which top-level parent is open
  const [openParentName, setOpenParentName] = useState<string | null>(null);
  const prevCollapsedRef = useRef(isCollapsed);

  // When sidebar is collapsed, close all submenus; when expanded again, open the parent that contains the active route
  useEffect(() => {
    if (isCollapsed) {
      setOpenParentName(null);
    } else if (prevCollapsedRef.current) {
      const parentName = getParentNameForActivePath(pathname, navItems);
      if (parentName) setOpenParentName(parentName);
    }
    prevCollapsedRef.current = isCollapsed;
  }, [isCollapsed, pathname, navItems]);

  const handleParentToggle = (name: string) => {
    // If clicking the same parent, close it. Otherwise, open the new one (closes others automatically)
    setOpenParentName(name === "" || openParentName === name ? null : name);
  };

  const handleLogout = () => {
    logout();
  };

  return (
    <>
      {isMobileOpen && (
        <div
          className="fixed inset-0 z-40 bg-black/50 md:hidden backdrop-blur-sm"
          onClick={closeMobile}
          aria-hidden="true"
        />
      )}

      <div
        className={cn(
          "fixed left-0 top-0 z-50 h-screen md:relative md:z-auto",
          // Modern glassmorphism with gradient
          "bg-gradient-to-br from-white/80 via-white/60 to-white/40 dark:from-card/90 dark:via-card/80 dark:to-background/70",
          "backdrop-blur-2xl border-r border-white/20 dark:border-border/30 shadow-xl shadow-black/5",
          // Enhanced shadows
          "shadow-2xl shadow-black/5 dark:shadow-black/20",
          // Smooth transitions
          "transition-all duration-500 ease-out",
          // Slide-in on mobile
          isMobileOpen ? "translate-x-0" : "-translate-x-full md:translate-x-0",
          // Collapse widths with better spacing
          isCollapsed ? "md:w-20" : "md:w-64",
          // Mobile widthN
          "w-[85vw] max-w-[20rem] sm:w-64",
          className,
        )}
      >
        <div className="flex h-full flex-col">
          <div className="flex items-center justify-between p-4 md:hidden h-18">
            <Link
              href="/overview"
              className="flex items-center gap-2"
              onClick={closeMobile}
            >
              <Globe
                className="h-9 w-9 text-[var(--sidebar-et-primary)]"
                aria-label="SimbaFlow Logo"
              />
              <h2 className="text-2xl font-bold text-[var(--sidebar-et-primary-foreground)]">
                SimbaFlow
              </h2>
            </Link>
            <Button
              variant="ghost"
              size="sm"
              onClick={closeMobile}
              aria-label="Close menu"
            >
              <X className="h-4 w-4" />
            </Button>
          </div>
          <hr className="border-gray-200 dark:border-gray-700" />

          <div className="hidden md:flex h-18 items-center px-4 border-b border-white/20 dark:border-border/30">
            <div
              className={cn(
                "flex items-center transition-all duration-300",
                isCollapsed
                  ? "justify-center opacity-100 scale-100"
                  : "justify-start opacity-100 scale-100",
              )}
            >
              {isCollapsed ? (
                <Globe
                  className="h-10 w-10 text-[var(--sidebar-et-primary)] drop-shadow-[0_4px_12px_rgba(59,130,246,0.6)]"
                  aria-label="SimbaFlow Logo"
                />
              ) : (
                <Link href="/overview" className="flex items-center gap-3 logo">
                  <Globe
                    className="h-10 w-10 text-[var(--sidebar-et-primary)] drop-shadow-[0_4px_12px_rgba(59,130,246,0.6)]"
                    aria-label="SimbaFlow Logo"
                  />
                  <h2 className="text-2xl font-bold text-[var(--sidebar-et-primary-foreground)] tracking-tight">
                    SimbaFlow
                  </h2>
                </Link>
              )}
            </div>
          </div>
          <div className="h-px bg-gradient-to-r from-transparent via-white/40 dark:via-border/40 to-transparent shadow-sm" />

          <nav className="flex-1 space-y-1 p-4 overflow-y-auto modern-scrollbar">
            {navItems.map((item) => (
              <NavItemWithCollapse
                key={item.name}
                userClaims={userClaims}
                item={item}
                isCollapsed={isCollapsed}
                pathname={pathname}
                closeMobile={closeMobile}
                openParentName={openParentName}
                onParentToggle={handleParentToggle}
              />
            ))}
          </nav>

          <div className="mt-auto border-t border-white/20 dark:border-border/30 p-4 flex items-center gap-3 bg-gradient-to-r from-white/50 to-white/30 dark:from-card/70 dark:to-background/50 backdrop-blur-xl shadow-inner">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <div className="flex items-center gap-3 cursor-pointer w-full hover:bg-white/50 dark:hover:bg-white/10 rounded-xl transition-all duration-300 p-3 border border-transparent hover:border-white/20">
                  {!isCollapsed && (
                    <div className="flex flex-col space-y-1 max-w-[calc(100%-4rem)] sm:max-w-[180px]">
                      <UserInfo />
                    </div>
                  )}
                </div>
              </DropdownMenuTrigger>
              <DropdownMenuContent
                className="w-[calc(100vw-2rem)] max-w-[22rem] sm:w-72 bg-white/90 dark:bg-card/90 backdrop-blur-2xl rounded-2xl shadow-2xl p-3 border border-white/20 dark:border-border/30"
                align="end"
                forceMount
              >
                <DropdownMenuLabel className="font-normal p-2">
                  <div className="flex flex-col space-y-1">
                    <UserInfo />
                  </div>
                </DropdownMenuLabel>
                <DropdownMenuSeparator className="bg-gray-200 dark:bg-gray-700" />
                <DropdownMenuItem
                  onClick={handleLogout}
                  className="flex items-center gap-3 p-3 text-destructive hover:bg-red-50 dark:hover:bg-red-900/20 rounded-xl transition-all duration-200 cursor-pointer text-sm"
                  disabled={isLoggingOut}
                >
                  {isLoggingOut ? (
                    <>
                      <div className="h-4 w-4 bg-destructive/20 rounded animate-pulse" />
                      <span>Logging out...</span>
                    </>
                  ) : (
                    <>
                      <LogOut className="h-4 w-4 text-destructive" />
                      <span>Log out</span>
                    </>
                  )}
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
      </div>
    </>
  );
}
