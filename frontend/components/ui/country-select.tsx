"use client";

import { useState, useMemo, useRef, useEffect } from "react";
import countryList from "react-select-country-list";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Check, ChevronsUpDown } from "lucide-react";
import { cn } from "@/lib/utils";

interface CountrySelectProps {
  value?: string;
  onChange: (value: string) => void;
  placeholder?: string;
}

function getFlagEmoji(countryCode: string): string {
  const codePoints = countryCode
    .toUpperCase()
    .split("")
    .map((char) => 127397 + char.charCodeAt(0));
  return String.fromCodePoint(...codePoints);
}

export function CountrySelect({ value, onChange, placeholder = "Select country" }: CountrySelectProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const searchRef = useRef<HTMLInputElement>(null);
  const options = useMemo(() => countryList().getData(), []);

  const filtered = useMemo(() => {
    if (!search.trim()) return options;
    const lower = search.trim().toLowerCase();
    return options.filter(
      (c) =>
        c.label.toLowerCase().includes(lower) ||
        c.value.toLowerCase().includes(lower)
    );
  }, [options, search]);

  const selected = options.find((c) => c.label === value || c.value === value);

  useEffect(() => {
    if (!open) {
      setSearch("");
      return;
    }
    // Focus search after popover mounts (Sheet focus trap otherwise steals keys)
    const id = window.setTimeout(() => searchRef.current?.focus(), 0);
    return () => window.clearTimeout(id);
  }, [open]);

  return (
    <Popover
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) setSearch("");
      }}
      modal
    >
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          className="w-full h-9 justify-between font-normal"
        >
          {selected ? (
            <span className="flex items-center gap-2 truncate">
              <span className="text-lg shrink-0">{getFlagEmoji(selected.value)}</span>
              {selected.label}
            </span>
          ) : (
            <span className="text-muted-foreground">{placeholder}</span>
          )}
          <ChevronsUpDown className="ml-2 h-4 w-4 shrink-0 opacity-50" />
        </Button>
      </PopoverTrigger>
      <PopoverContent
        className="w-[--radix-popover-trigger-width] p-0 z-[200]"
        align="start"
        onOpenAutoFocus={(e) => {
          e.preventDefault();
          searchRef.current?.focus();
        }}
        onCloseAutoFocus={(e) => e.preventDefault()}
      >
        <div className="p-2 border-b">
          <Input
            ref={searchRef}
            placeholder="Search country..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            onKeyDown={(e) => e.stopPropagation()}
            className="h-8"
            autoComplete="off"
          />
        </div>
        <div className="h-[250px] overflow-y-auto p-1">
          {filtered.length === 0 ? (
            <p className="px-2 py-3 text-sm text-muted-foreground">No country found.</p>
          ) : (
            filtered.map((country) => (
              <button
                key={country.value}
                type="button"
                className={cn(
                  "flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-sm hover:bg-accent cursor-pointer",
                  value === country.label && "bg-accent"
                )}
                onClick={() => {
                  onChange(country.label);
                  setOpen(false);
                  setSearch("");
                }}
              >
                <span className="text-lg">{getFlagEmoji(country.value)}</span>
                <span className="flex-1 text-left">{country.label}</span>
                {value === country.label && <Check className="h-4 w-4 text-primary" />}
              </button>
            ))
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}
