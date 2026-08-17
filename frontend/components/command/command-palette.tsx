"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
  CommandDialog,
  CommandInput,
  CommandList,
  CommandEmpty,
  CommandGroup,
  CommandItem,
  CommandSeparator,
} from "@/components/ui/command";
import { navigation, filterNavigationByClaims, type NavItem } from "@/components/layout/nav-items";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { useCommandPalette } from "@/lib/stores/command-store";
import { UserPlus, Search, User, ArrowRight } from "lucide-react";

type Dest = { name: string; href: string; icon?: React.ElementType };

type CandidateHit = {
  id: string;
  fullName: string;
  passportNumber: string;
  currentStageName?: string | null;
};

function flattenNav(items: NavItem[]): Dest[] {
  const out: Dest[] = [];
  for (const item of items) {
    if (item.isSeparator) continue;
    if (item.href) out.push({ name: item.name, href: item.href, icon: item.icon });
    if (item.children) out.push(...flattenNav(item.children));
  }
  return out;
}

export function CommandPalette() {
  const router = useRouter();
  const { open, setOpen, toggle } = useCommandPalette();
  const { permissions, isSuperAdmin, hasPermission } = usePermissions();
  const [query, setQuery] = useState("");
  const [hits, setHits] = useState<CandidateHit[]>([]);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Global ⌘K / Ctrl+K shortcut
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        toggle();
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [toggle]);

  const destinations = useMemo(
    () => flattenNav(filterNavigationByClaims(navigation, permissions ?? [], !!isSuperAdmin)),
    [permissions, isSuperAdmin]
  );

  const canReadCandidates =
    hasPermission("candidate.read") || hasPermission("system.admin");

  // Debounced candidate search
  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    const q = query.trim();
    if (!open || !canReadCandidates || q.length < 2) {
      setHits([]);
      return;
    }
    debounceRef.current = setTimeout(async () => {
      try {
        const res = await fetch(
          `/api/proxy/candidates?search=${encodeURIComponent(q)}&page=1&pageSize=6`
        );
        const body = await res.json().catch(() => ({}));
        const items = body?.data?.items ?? body?.data?.data ?? [];
        setHits(Array.isArray(items) ? items : []);
      } catch {
        setHits([]);
      }
    }, 220);
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
  }, [query, open, canReadCandidates]);

  // Reset query when closing
  useEffect(() => {
    if (!open) setQuery("");
  }, [open]);

  const go = (href: string) => {
    setOpen(false);
    router.push(href);
  };

  return (
    <CommandDialog
      open={open}
      onOpenChange={setOpen}
      title="Search & navigate"
      description="Jump to a page or find a candidate"
    >
      <CommandInput
        placeholder="Search candidates, pages, actions…"
        value={query}
        onValueChange={setQuery}
      />
      <CommandList>
        <CommandEmpty>No results found.</CommandEmpty>

        {canReadCandidates && (
          <CommandItem
            value="Register new candidate"
            onSelect={() => go("/candidates/register")}
          >
            <UserPlus />
            <span>Register new candidate</span>
          </CommandItem>
        )}

        {hits.length > 0 && (
          <>
            <CommandSeparator />
            <CommandGroup heading="Candidates">
              {hits.map((c) => (
                <CommandItem
                  key={c.id}
                  value={`${c.fullName} ${c.passportNumber} ${c.id}`}
                  onSelect={() => go(`/candidates/${c.id}`)}
                >
                  <User />
                  <div className="flex min-w-0 flex-col">
                    <span className="truncate">{c.fullName}</span>
                    <span className="truncate text-xs text-muted-foreground">
                      {c.passportNumber}
                      {c.currentStageName ? ` · ${c.currentStageName}` : ""}
                    </span>
                  </div>
                </CommandItem>
              ))}
            </CommandGroup>
          </>
        )}

        {destinations.length > 0 && (
          <>
            <CommandSeparator />
            <CommandGroup heading="Go to">
              {destinations.map((d) => {
                const Icon = d.icon ?? ArrowRight;
                return (
                  <CommandItem
                    key={d.href}
                    value={`${d.name} ${d.href}`}
                    onSelect={() => go(d.href)}
                  >
                    {Icon ? <Icon /> : <Search />}
                    <span>{d.name}</span>
                  </CommandItem>
                );
              })}
            </CommandGroup>
          </>
        )}
      </CommandList>
    </CommandDialog>
  );
}
