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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "sonner";
import {
  getActingTenantId,
  getActingTenantName,
  setActingTenant,
} from "@/lib/tenant/acting-tenant";
import { AgencyMark } from "@/components/layout/agency-mark";

interface Agency {
  id: string;
  name: string;
}

const fetcher = (url: string) => fetch(url).then((r) => r.json());

type Grant = { tenantId: string; reason: string; expiresAt: string };

const HOURS = [1, 2, 4, 8, 12];

function untilTime(iso: string) {
  return new Date(iso).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
}

/**
 * Lets a platform admin pick the agency they are working inside.
 *
 * Entering an agency asks why and for how long. The server only honours the switch while that
 * reason is live, records it on the agency's audit trail, and lets the agency's owner see it; when
 * it runs out the switcher drops back to all agencies.
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
  const [asking, setAsking] = useState<Agency | null>(null);
  const [reason, setReason] = useState("");
  const [hours, setHours] = useState(4);
  const [entering, setEntering] = useState(false);

  // The live reasons this administrator has on record, checked every minute.
  const { data: grantsData, mutate: mutateGrants } = useSWR(
    "/api/proxy/platform/access/mine",
    fetcher,
    { refreshInterval: 60_000, revalidateOnFocus: true },
  );
  const grants: Grant[] = Array.isArray(grantsData?.data) ? grantsData.data : [];
  const activeGrant = grants.find((g) => g.tenantId === activeId) ?? null;

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

  // The choice lives in this browser, so it outlives the agency it names. After an agency was
  // deleted the switcher still read its name back out of localStorage and showed it, while every
  // request carried its id in X-Tenant-Id — the admin was working inside an agency that no longer
  // existed, and the only clue was a list that had nothing in it.
  //
  // Waits for a loaded list: `data` is undefined while the request is in flight, and treating
  // that as "not found" would drop a valid choice on every page load.
  useEffect(() => {
    if (!data || !activeId) return;
    if (agencies.some((a) => a.id === activeId)) return;

    setActingTenant(null);
    setActiveId(null);
    setActiveName(null);
    mutate(() => true, undefined, { revalidate: true });
    toast.info("That agency no longer exists — showing all agencies.");
  }, [data, agencies, activeId]);

  // An agency entered without a live reason (it ran out, or it was chosen before this existed)
  // is left: every request inside it would be refused anyway.
  useEffect(() => {
    if (!grantsData || !activeId) return;
    if (!Array.isArray(grantsData?.data)) return;
    if (grants.some((g) => g.tenantId === activeId)) return;
    const name = activeName;
    switchTo(null);
    toast.info(`Your access to ${name ?? "that agency"} has ended. Choose it again to continue.`);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [grantsData, activeId]);

  const switchTo = (agency: Agency | null) => {
    setActingTenant(agency?.id ?? null, agency?.name ?? null);
    setActiveId(agency?.id ?? null);
    setActiveName(agency?.name ?? null);
    // Drop every cached response — they belong to the agency we just left.
    mutate(() => true, undefined, { revalidate: true });
    router.refresh();
  };

  const choose = async (agency: Agency | null) => {
    if (agency) {
      if (agency.id === activeId && activeGrant) return;
      setReason("");
      setAsking(agency);
      return;
    }
    if (activeId) {
      await fetch("/api/proxy/platform/access/end", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ tenantId: activeId }),
      }).catch(() => undefined);
    }
    switchTo(null);
    void mutateGrants();
    toast.success("Back to all agencies");
  };

  const enter = async () => {
    if (!asking) return;
    setEntering(true);
    try {
      const res = await fetch("/api/proxy/platform/access", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ tenantId: asking.id, reason, hours }),
      });
      const body = await res.json().catch(() => ({}));
      if (!res.ok || !body?.isSuccess) {
        toast.error(body?.error || "Could not enter the agency");
        return;
      }
      await mutateGrants();
      switchTo(asking);
      toast.success(`Now working in ${asking.name} until ${untilTime(body.data.expiresAt)}`);
      setAsking(null);
    } finally {
      setEntering(false);
    }
  };

  const label = activeName ?? "All agencies";

  return (
    <>
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          size="sm"
          className="h-7 gap-1.5 px-2.5 text-xs font-medium"
          aria-label="Switch agency"
        >
          {activeId ? (
            // The agency's own logo once one is chosen — the picker names the agency, and this
            // says which one without reading it.
            <AgencyMark fallback={<Building2 className="h-3.5 w-3.5 text-green-700" />} />
          ) : (
            <Shield className="h-3.5 w-3.5 text-amber-600" />
          )}
          <span className="max-w-[12rem] truncate">{label}</span>
          {activeGrant ? (
            <span className="text-[10px] text-muted-foreground">until {untilTime(activeGrant.expiresAt)}</span>
          ) : null}
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

    <Dialog open={!!asking} onOpenChange={(open) => { if (!open) setAsking(null); }}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Enter {asking?.name}</DialogTitle>
          <DialogDescription>
            Say why you need to work inside this agency. The reason goes on its audit trail, and its
            owner can see it.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="access-reason">Reason</Label>
            <Textarea
              id="access-reason"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="e.g. The owner asked for help fixing a candidate's stage"
              rows={3}
            />
          </div>
          <div className="space-y-1.5">
            <Label>For how long</Label>
            <div className="flex flex-wrap gap-1.5">
              {HOURS.map((h) => (
                <Button
                  key={h}
                  type="button"
                  size="sm"
                  variant={h === hours ? "default" : "outline"}
                  className="h-7"
                  onClick={() => setHours(h)}
                >
                  {h} {h === 1 ? "hour" : "hours"}
                </Button>
              ))}
            </div>
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => setAsking(null)}>
            Cancel
          </Button>
          <Button
            onClick={enter}
            disabled={entering || reason.trim().length < 5}
            className="bg-green-800 hover:bg-green-900"
          >
            Enter agency
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
    </>
  );
}
