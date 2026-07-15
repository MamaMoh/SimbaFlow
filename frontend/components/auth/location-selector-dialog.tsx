"use client";

import { useState, useEffect } from "react";
import { MapPin, Building2, Check, Loader2 } from "lucide-react";
import { toast } from "sonner";

interface LocationOption {
  id: string;
  name: string;
  code: string;
  type: string;
  isPrimary: boolean;
}

interface LocationSelectorDialogProps {
  open: boolean;
  onLocationSelected: (locationId: string, locationName: string) => void;
  accessToken: string;
}

export function LocationSelectorDialog({ open, onLocationSelected, accessToken }: LocationSelectorDialogProps) {
  const [locations, setLocations] = useState<LocationOption[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (!open || !accessToken) return;

    async function fetchLocations() {
      try {
        const res = await fetch("/api/proxy/auth/my-locations", {
          headers: { "Content-Type": "application/json" },
        });
        if (res.ok) {
          const data = await res.json();
          const locs = data?.data || data || [];
          setLocations(locs);
          const primary = locs.find((l: LocationOption) => l.isPrimary);
          if (primary) setSelectedId(primary.id);
          else if (locs.length === 1) setSelectedId(locs[0].id);
        }
      } catch (error) {
        console.error("Failed to fetch locations");
      } finally {
        setIsLoading(false);
      }
    }

    fetchLocations();
  }, [open, accessToken]);

  const handleConfirm = async () => {
    if (!selectedId) {
      toast.error("Please select a location");
      return;
    }

    setIsSubmitting(true);
    try {
      const res = await fetch("/api/proxy/auth/set-location", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ locationId: selectedId }),
      });

      if (res.ok) {
        const selected = locations.find(l => l.id === selectedId);
        localStorage.setItem("simba_active_location_id", selectedId);
        localStorage.setItem("simba_active_location_name", selected?.name || "");
        onLocationSelected(selectedId, selected?.name || "");
        toast.success("Working at: " + (selected?.name || ""));
      } else {
        toast.error("Failed to set location");
      }
    } catch {
      toast.error("Failed to set location");
    } finally {
      setIsSubmitting(false);
    }
  };

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/60 backdrop-blur-sm">
      <div className="w-full max-w-lg mx-4 bg-card rounded-2xl shadow-2xl border overflow-hidden">
        <div className="p-6 border-b bg-gradient-to-r from-primary/5 to-transparent">
          <div className="flex items-center gap-3">
            <div className="rounded-lg bg-primary/10 p-2">
              <MapPin className="h-5 w-5 text-primary" />
            </div>
            <div>
              <h2 className="text-lg font-semibold">Select Working Location</h2>
              <p className="text-sm text-muted-foreground">Choose where you are working today</p>
            </div>
          </div>
        </div>

        <div className="p-6 max-h-[400px] overflow-y-auto">
          {isLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-6 w-6 animate-spin text-primary" />
              <span className="ml-2 text-muted-foreground">Loading locations...</span>
            </div>
          ) : locations.length === 0 ? (
            <div className="text-center py-12 text-muted-foreground">
              <Building2 className="h-12 w-12 mx-auto mb-3 opacity-30" />
              <p>No locations assigned to your profile.</p>
              <p className="text-xs mt-1">Contact your administrator.</p>
            </div>
          ) : (
            <div className="space-y-2">
              {locations.map((location) => {
                const isSelected = selectedId === location.id;
                return (
                  <button
                    key={location.id}
                    onClick={() => setSelectedId(location.id)}
                    className={
                      "w-full flex items-center gap-3 p-4 rounded-xl border transition-all text-left " +
                      (isSelected
                        ? "border-primary bg-primary/5 ring-2 ring-primary/20"
                        : "border-border hover:border-primary/30 hover:bg-accent/50")
                    }
                  >
                    <div className={"rounded-lg p-2 " + (isSelected ? "bg-primary/10" : "bg-muted")}>
                      <Building2 className={"h-4 w-4 " + (isSelected ? "text-primary" : "text-muted-foreground")} />
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="font-medium text-sm truncate">{location.name}</p>
                      <p className="text-xs text-muted-foreground">
                        {location.code} &bull; {location.type}
                        {location.isPrimary && " • Primary"}
                      </p>
                    </div>
                    {isSelected && (
                      <Check className="h-5 w-5 text-primary shrink-0" />
                    )}
                  </button>
                );
              })}
            </div>
          )}
        </div>

        <div className="p-6 border-t bg-muted/30">
          <button
            onClick={handleConfirm}
            disabled={!selectedId || isSubmitting}
            className="w-full h-11 rounded-lg bg-primary text-primary-foreground font-medium text-sm hover:bg-primary/90 transition-colors disabled:opacity-50 disabled:pointer-events-none inline-flex items-center justify-center"
          >
            {isSubmitting ? (
              <>
                <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                Setting location...
              </>
            ) : (
              "Continue"
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
