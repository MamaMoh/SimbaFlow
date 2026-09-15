"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import useSWR, { mutate } from "swr";
import { Building2, Check, ChevronsUpDown, Shield } from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Button } from "@/components/ui/button";
import { toast } from "sonner";
import {
  getActingTenantId,
  getActingTenantName,
  setActingTenant,
} from "@/lib/tenant/acting-tenant";

interface Agency {
  id: string;
  name: string;
}

const fetcher = (url: string) => fetch(url).then((r) => r.json());

/**
 * Lets a platform admin pick the agency they are working inside.
 *
 * Without this the platform admin sees whichever agency the backend picks by default, which is
 * not necessarily the one they are being asked about. Switching re-reads every cached query,
 * because otherwise the page keeps showing the previous agency's candidates under the new name.
 */
export function AgencySwitcher() {
  const router = useRouter();
  const { data } = useSWR("/api/proxy/tenants", fetcher, { revalidateOnFocus: false });
  const [activeId, setActiveId] = useState<string | null>(null);
  const [activeName, setActiveName] = useState<string | null>(null);

  useEffect(() => {
    setActiveId(getActingTenantId());
    setActiveName(getActingTenantName());
  }, []);

  const agencies: Agency[] = Array.isArray(data)
    ? data
    : Array.isArray(data?.items)
      ? data.items
      : Array.isArray(data?.data)
        ? data.data
        : [];

  const choose = (agency: Agency | null) => {
    setActingTenant(agency?.id ?? null, agency?.name ?? null);
    setActiveId(agency?.id ?? null);
    setActiveName(agency?.name ?? null);
    // Drop every cached response — they belong to the agency we just left.
    mutate(() => true, undefined, { revalidate: true });
    router.refresh();
    toast.success(agency ? `Now working in ${agency.name}` : "Back to all agencies");
  };

  const label = activeName ?? "All agencies";

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          size="sm"
          className="h-7 gap-1.5 px-2.5 text-xs font-medium"
          aria-label="Switch agency"
        >
          {activeId ? (
            <Building2 className="h-3.5 w-3.5 text-green-700" />
          ) : (
            <Shield className="h-3.5 w-3.5 text-amber-600" />
          )}
          <span className="max-w-[12rem] truncate">{label}</span>
          <ChevronsUpDown className="h-3 w-3 opacity-50" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-64">
        <DropdownMenuLabel className="text-xs font-normal text-muted-foreground">
          Work inside an agency
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem onSelect={() => choose(null)} className="gap-2 text-sm">
          <Shield className="h-3.5 w-3.5 text-amber-600" />
          <span className="flex-1">All agencies</span>
          {!activeId && <Check className="h-3.5 w-3.5" />}
        </DropdownMenuItem>
        {agencies.length > 0 && <DropdownMenuSeparator />}
        {agencies.map((agency) => (
          <DropdownMenuItem
            key={agency.id}
            onSelect={() => choose(agency)}
            className="gap-2 text-sm"
          >
            <Building2 className="h-3.5 w-3.5 text-green-700" />
            <span className="flex-1 truncate">{agency.name}</span>
            {activeId === agency.id && <Check className="h-3.5 w-3.5" />}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
