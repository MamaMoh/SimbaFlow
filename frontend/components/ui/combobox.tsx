"use client";

import * as React from "react";
import { Check, ChevronsUpDown } from "lucide-react";

import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import {
  Command,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from "@/components/ui/command";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";

interface ComboboxItem {
  value: string;
  label: string;
}

interface ComboboxProps {
  items: ComboboxItem[]; // initial items or fallback
  value: string;
  onValueChange: (value: string) => void;
  placeholder?: string;
  className?: string;
  disabled?: boolean;
  icon?: React.ReactNode;
}
export function Combobox({
  items,
  value,
  onValueChange,
  placeholder = "Select...",
  className,
  disabled = false,
  icon,
}: ComboboxProps) {
  const [open, setOpen] = React.useState(false);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <div className="relative w-full">
          {icon && (
            <span className="absolute left-3 top-1/2 -translate-y-1/2 z-10 pointer-events-none text-muted-foreground flex items-center justify-center">
              {icon}
            </span>
          )}
          <Button
            type="button"
            variant="outline"
            role="combobox"
            aria-expanded={open}
            disabled={disabled}
            className={cn(
              "w-full min-w-[280px] justify-start gap-0 text-left font-normal h-10 relative",
              !value && "text-muted-foreground",
              icon ? "pl-10 pr-10" : "pl-3 pr-10",
              className
            )}
          >
            <span className="truncate flex-1 min-w-0 pr-2 text-left">
              {value ? items.find((i) => i.value === value)?.label : placeholder}
            </span>
            <ChevronsUpDown className="h-4 w-4 opacity-50 shrink-0 absolute right-2 top-1/2 -translate-y-1/2 pointer-events-none" />
          </Button>
        </div>
      </PopoverTrigger>

      <PopoverContent
        side="bottom"
        align="start"
        sideOffset={4}
        collisionPadding={8}
        className="p-0 w-[var(--radix-popover-trigger-width)]"
      >
        <Command
          className="rounded-lg border-0"
          filter={(value, search, keywords = []) => {
            const searchLower = search.toLowerCase().trim();
            if (!searchLower) return 1;
            const valueLower = value.toLowerCase();
            const keywordsLower = keywords.join(" ").toLowerCase();
            const matchText = `${valueLower} ${keywordsLower}`;
            return matchText.includes(searchLower) ? 1 : 0;
          }}
        >
          <CommandInput
            placeholder={placeholder.toLowerCase().startsWith("search") ? placeholder : `Search ${placeholder.toLowerCase()}...`}
            wrapperClassName="border-0 rounded-t-lg px-3 bg-transparent"
            className="rounded-t-lg h-10 border-0 shadow-none outline-none ring-0 focus-visible:ring-0 focus-visible:ring-offset-0 bg-transparent pl-10"
          />
          <CommandList
            className="max-h-[300px] overflow-y-auto overscroll-contain"
            onWheel={(e) => {
              e.stopPropagation();
            }}
          >
            <CommandEmpty className="py-6 text-center text-sm text-muted-foreground">
              No results found.
            </CommandEmpty>
            <CommandGroup className="p-1">
              {items.map((item) => (
                <CommandItem
                  key={item.value}
                  value={item.value}
                  keywords={[item.label]}
                  onSelect={(currentValue) => {
                    // CommandItem's onSelect receives the item.value, not the current form value
                    const newValue = currentValue === value ? "" : currentValue;
                    onValueChange(newValue);
                    setOpen(false);
                  }}
                  onClick={(e) => {
                    // Prevent form submission when clicking dropdown items
                    e.stopPropagation();
                  }}
                  className="cursor-pointer rounded-md px-2 py-1.5 aria-selected:bg-accent aria-selected:text-accent-foreground"
                >
                  <Check
                    className={cn(
                      "mr-2 h-4 w-4 shrink-0",
                      value === item.value ? "opacity-100" : "opacity-0"
                    )}
                  />
                  <span className="flex-1">{item.label}</span>
                </CommandItem>
              ))}
            </CommandGroup>
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
