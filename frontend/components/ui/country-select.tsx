"use client";

import * as React from "react";
import { COUNTRY_DATA } from "@/lib/data/countries";
import { Combobox } from "@/components/ui/combobox";

/**
 * Searchable country picker.
 *
 * The caller stores the ISO 3166-1 alpha-2 code; the display name is derived, so a
 * form never has to keep code and name in sync by hand.
 *
 * Data comes from a pre-generated 9 KB list rather than the full `world-countries`
 * package — see lib/data/countries.ts for why.
 */

export interface CountryOption {
  code: string;
  name: string;
  flag: string;
}

/** Already alphabetical in the generated file. */
export const COUNTRIES: CountryOption[] = COUNTRY_DATA.map(([code, name, flag]) => ({
  code,
  name,
  flag,
}));

const BY_CODE = new Map(COUNTRIES.map((c) => [c.code, c]));

/** Destinations Ethiopian agencies actually deploy to — floated to the top of the list. */
const COMMON_DESTINATIONS = ["SA", "AE", "KW", "QA", "BH", "OM", "JO", "LB"];

export function countryName(code: string | null | undefined): string {
  if (!code) return "";
  return BY_CODE.get(code.toUpperCase())?.name ?? code;
}

export function CountrySelect({
  value,
  onChange,
  placeholder = "Select country",
  disabled,
  className,
}: {
  value: string;
  onChange: (code: string, name: string) => void;
  placeholder?: string;
  disabled?: boolean;
  className?: string;
}) {
  const items = React.useMemo(() => {
    const common = COMMON_DESTINATIONS.map((code) => BY_CODE.get(code)).filter(
      (c): c is CountryOption => !!c
    );
    const rest = COUNTRIES.filter((c) => !COMMON_DESTINATIONS.includes(c.code));
    return [...common, ...rest].map((c) => ({
      value: c.code,
      label: `${c.flag} ${c.name}`,
    }));
  }, []);

  return (
    <Combobox
      items={items}
      value={value?.toUpperCase() ?? ""}
      onValueChange={(code) => onChange(code, countryName(code))}
      placeholder={placeholder}
      disabled={disabled}
      className={className}
    />
  );
}
