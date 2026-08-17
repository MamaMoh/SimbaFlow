"use client";

import { useMemo, useState } from "react";
import useSWR from "swr";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from "@/components/ui/sheet";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { toast } from "sonner";

const fetcher = (url: string) => fetch(url).then((r) => r.json());

type CatalogPartner = {
  id: string;
  name: string;
  country: string;
  countryCode: string;
  capacityTier: string;
};

export function LinkPartnerSheet({
  open,
  onOpenChange,
  onLinked,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onLinked: () => void;
}) {
  const { data } = useSWR(open ? "/api/proxy/partners" : null, fetcher, {
    revalidateOnFocus: false,
  });
  const partners: CatalogPartner[] = data?.data || [];
  const [partnerId, setPartnerId] = useState("");
  const [start, setStart] = useState(() => new Date().toISOString().slice(0, 10));
  const [end, setEnd] = useState(() => {
    const d = new Date();
    d.setFullYear(d.getFullYear() + 2);
    return d.toISOString().slice(0, 10);
  });
  const [busy, setBusy] = useState(false);

  const selected = useMemo(
    () => partners.find((p) => p.id === partnerId),
    [partners, partnerId]
  );

  const onSubmit = async () => {
    if (!partnerId) {
      toast.error("Select a partner agency");
      return;
    }
    setBusy(true);
    try {
      const res = await fetch("/api/proxy/partners/links", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          partnerAgencyId: partnerId,
          agreementStart: start,
          agreementEnd: end,
        }),
      });
      const body = await res.json();
      if (!body.isSuccess) {
        toast.error(body.error || "Failed to link partner");
        return;
      }
      toast.success(`Linked ${selected?.name ?? "partner"}`);
      onOpenChange(false);
      onLinked();
    } catch {
      toast.error("Failed to link partner");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-[480px] sm:max-w-[480px] flex flex-col px-6">
        <SheetHeader>
          <SheetTitle>Link partner to my agency</SheetTitle>
          <SheetDescription>
            Creates a ትስስር agreement. Enforces your MoLS level caps and Art. 40 partner capacity.
          </SheetDescription>
        </SheetHeader>
        <div className="mt-4 flex flex-1 flex-col gap-4">
          <div className="space-y-1.5">
            <Label>Foreign partner *</Label>
            <Select value={partnerId || undefined} onValueChange={setPartnerId}>
              <SelectTrigger>
                <SelectValue placeholder="Select from catalog" />
              </SelectTrigger>
              <SelectContent position="popper" className="z-[200]">
                {partners.map((p) => (
                  <SelectItem key={p.id} value={p.id}>
                    {p.name} · {p.country} ({p.capacityTier})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Agreement start</Label>
              <Input type="date" value={start} onChange={(e) => setStart(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label>Agreement end</Label>
              <Input type="date" value={end} onChange={(e) => setEnd(e.target.value)} />
            </div>
          </div>
          <div className="mt-auto flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button
              type="button"
              disabled={busy}
              onClick={() => void onSubmit()}
              className="bg-green-800 text-white hover:bg-green-900"
            >
              {busy ? "Linking…" : "Link partner"}
            </Button>
          </div>
        </div>
      </SheetContent>
    </Sheet>
  );
}
