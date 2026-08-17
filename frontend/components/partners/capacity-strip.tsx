"use client";

import { usePartnerCapacity } from "@/lib/api/partners";
import { cn } from "@/lib/utils";

/**
 * Shows how many partner slots the agency's MoLS level allows and how many are
 * used per destination country — the cap is enforced on link create, so staff
 * need to see it before they run out.
 */
export function CapacityStrip({ enabled = true }: { enabled?: boolean }) {
  const { data, error } = usePartnerCapacity(enabled);
  if (error || !data) return null;

  const countryLimit = data.maxLicensedCountries;
  const countriesOver =
    countryLimit != null && data.licensedCountriesUsed > countryLimit;

  return (
    <div className="rounded-xl border bg-card p-4 shadow-sm">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-sm font-semibold">Partner capacity</h2>
        <span className="text-xs text-muted-foreground">{data.levelDescription}</span>
      </div>

      {data.byCountry.length === 0 ? (
        <p className="mt-2 text-sm text-muted-foreground">
          No partners linked yet — you may link up to {data.maxPartnersPerCountry} per country.
        </p>
      ) : (
        <div className="mt-3 flex flex-wrap gap-2">
          {data.byCountry.map((c) => {
            const full = c.remaining === 0;
            return (
              <span
                key={c.country}
                className={cn(
                  "inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-xs",
                  full
                    ? "border-amber-200 bg-amber-50 text-amber-900 dark:border-amber-900/60 dark:bg-amber-950/40 dark:text-amber-300"
                    : "bg-muted/50",
                )}
                title={
                  full
                    ? `Limit reached for ${c.country}`
                    : `${c.remaining} more allowed for ${c.country}`
                }
              >
                <span className="font-medium">{c.country}</span>
                <span className="tabular-nums">
                  {c.used}/{c.max}
                </span>
              </span>
            );
          })}
        </div>
      )}

      {countryLimit != null && (
        <p
          className={cn(
            "mt-3 text-xs",
            countriesOver ? "font-medium text-destructive" : "text-muted-foreground",
          )}
        >
          Licensed countries: {data.licensedCountriesUsed}/{countryLimit}
          {countriesOver ? " — over the limit for this agency level" : ""}
        </p>
      )}
    </div>
  );
}
