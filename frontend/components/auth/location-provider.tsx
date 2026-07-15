"use client";

import { createContext, useContext, useState, useEffect, useCallback } from "react";
import { useSession } from "next-auth/react";
import { LocationSelectorDialog } from "./location-selector-dialog";

interface LocationContextType {
  activeLocationId: string | null;
  activeLocationName: string | null;
  showLocationSelector: () => void;
}

const LocationContext = createContext<LocationContextType>({
  activeLocationId: null,
  activeLocationName: null,
  showLocationSelector: () => {},
});

export function useLocation() {
  return useContext(LocationContext);
}

export function LocationProvider({ children }: { children: React.ReactNode }) {
  const { data: session, status } = useSession();
  const [activeLocationId, setActiveLocationId] = useState<string | null>(null);
  const [activeLocationName, setActiveLocationName] = useState<string | null>(null);
  const [showSelector, setShowSelector] = useState(false);
  const [hasChecked, setHasChecked] = useState(false);

  // On mount, check localStorage for stored location
  useEffect(() => {
    if (status !== "authenticated") return;

    const storedId = localStorage.getItem("simba_active_location_id");
    const storedName = localStorage.getItem("simba_active_location_name");

    if (storedId && storedName) {
      setActiveLocationId(storedId);
      setActiveLocationName(storedName);
      setHasChecked(true);
    } else {
      // No location set - show selector
      setShowSelector(true);
      setHasChecked(true);
    }
  }, [status]);

  const handleLocationSelected = useCallback((locationId: string, locationName: string) => {
    setActiveLocationId(locationId);
    setActiveLocationName(locationName);
    setShowSelector(false);
  }, []);

  const showLocationSelector = useCallback(() => {
    setShowSelector(true);
  }, []);

  const accessToken = (session?.user as any)?.accessToken || "";

  return (
    <LocationContext.Provider value={{ activeLocationId, activeLocationName, showLocationSelector }}>
      {children}
      {hasChecked && (
        <LocationSelectorDialog
          open={showSelector}
          onLocationSelected={handleLocationSelected}
          accessToken={accessToken}
        />
      )}
    </LocationContext.Provider>
  );
}
