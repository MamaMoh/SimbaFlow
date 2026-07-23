"use client";

import { useMemo, useState } from "react";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Check, ChevronsUpDown } from "lucide-react";
import { cn } from "@/lib/utils";
import { COUNTRIES, DESTINATION_COUNTRIES, type CountryOption } from "@/lib/countries";

interface CountrySelectProps {
  value?: string;
  onChange: (value: string) => void;
  placeholder?: string;
  /** Limit to labour-export destinations when true */
  destinationsOnly?: boolean;
}

function getFlagEmoji(countryCode: string): string {
  if (!countryCode || countryCode.length !== 2) return "🏳️";
  const codePoints = countryCode
    .toUpperCase()
    .split("")
    .map((char) => 127397 + char.charCodeAt(0));
  return String.fromCodePoint(...codePoints);
}

export function CountrySelect({
  value,
  onChange,
  placeholder = "Select country",
  destinationsOnly = false,
}: CountrySelectProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const options: CountryOption[] = destinationsOnly ? DESTINATION_COUNTRIES : COUNTRIES;

  const filtered = useMemo(() => {
    if (!search) return options;
    const lower = search.toLowerCase();
    return options.filter(
      (c) => c.label.toLowerCase().includes(lower) || c.value.toLowerCase().includes(lower),
    );
  }, [options, search]);

  const selected = options.find((c) => c.label === value || c.value === value);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          role="combobox"
          aria-expanded={open}
          className="h-9 w-full justify-between font-normal"
        >
          {selected ? (
            <span className="flex items-center gap-2">
              <span className="text-lg">{getFlagEmoji(selected.value)}</span>
              {selected.label}
            </span>
          ) : value ? (
            <span>{value}</span>
          ) : (
            <span className="text-muted-foreground">{placeholder}</span>
          )}
          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[--radix-popover-trigger-width] p-0" align="start">
        <div className="border-b p-2">
          <Input
            placeholder="Search country..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="h-8"
          />
        </div>
        <ScrollArea className="h-[250px]">
          <div className="p-1">
            {filtered.map((country) => (
              <button
                key={country.value}
                type="button"
                className={cn(
                  "flex w-full cursor-pointer items-center gap-2 rounded-sm px-2 py-1.5 text-sm hover:bg-accent",
                  (value === country.label || value === country.value) && "bg-accent",
                )}
                onClick={() => {
                  onChange(country.label);
                  setOpen(false);
                  setSearch("");
                }}
              >
                <span className="text-lg">{getFlagEmoji(country.value)}</span>
                <span className="flex-1 text-left">{country.label}</span>
                {(value === country.label || value === country.value) && (
                  <Check className="h-4 w-4 text-primary" />
                )}
              </button>
            ))}
            {!filtered.length && (
              <p className="px-2 py-4 text-center text-xs text-muted-foreground">No countries found</p>
            )}
          </div>
        </ScrollArea>
      </PopoverContent>
    </Popover>
  );
}
