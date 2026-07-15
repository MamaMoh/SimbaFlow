"use client";

import * as React from "react";
import { ChevronRight, ChevronDown, Check } from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible";

export interface HierarchicalItem {
  id: string;
  name: string;
  children?: HierarchicalItem[];
}

interface HierarchicalSelectProps {
  items: HierarchicalItem[];
  value?: string | null;
  onValueChange: (value: string | null) => void;
  placeholder?: string;
  disabled?: boolean;
  excludeIds?: string[]; // IDs to exclude from the list (e.g., current item to prevent circular references)
}

export function HierarchicalSelect({
  items,
  value,
  onValueChange,
  placeholder = "Select an item",
  disabled = false,
  excludeIds = [],
}: HierarchicalSelectProps) {
  const [open, setOpen] = React.useState(false);
  const [expandedItems, setExpandedItems] = React.useState<Set<string>>(new Set());

  // Find the selected item to display its name
  const findItemById = (items: HierarchicalItem[], id: string): HierarchicalItem | null => {
    for (const item of items) {
      if (item.id === id) return item;
      if (item.children) {
        const found = findItemById(item.children, id);
        if (found) return found;
      }
    }
    return null;
  };

  const selectedItem = value ? findItemById(items, value) : null;

  const toggleExpanded = (id: string) => {
    setExpandedItems((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  const handleSelect = (id: string) => {
    if (id === "__none__") {
      onValueChange(null);
    } else {
      onValueChange(id);
    }
    setOpen(false);
  };

  const renderItem = (item: HierarchicalItem, level: number = 0): React.ReactNode => {
    if (excludeIds.includes(item.id)) {
      return null;
    }

    const hasChildren = item.children && item.children.length > 0;
    const isExpanded = expandedItems.has(item.id);
    const isSelected = value === item.id;

    return (
      <div key={item.id}>
        <div
          className={cn(
            "flex items-center gap-2 rounded-sm transition-colors",
            isSelected && "bg-accent"
          )}
          style={{ paddingLeft: `${level * 16 + 8}px` }}
        >
          <div className="flex items-center gap-2 flex-1 min-w-0">
            {hasChildren ? (
              <button
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  toggleExpanded(item.id);
                }}
                className="flex items-center justify-center w-4 h-4 hover:bg-accent rounded shrink-0"
              >
                {isExpanded ? (
                  <ChevronDown className="h-3 w-3" />
                ) : (
                  <ChevronRight className="h-3 w-3" />
                )}
              </button>
            ) : (
              <div className="w-4 shrink-0" /> // Spacer for alignment
            )}
            <div
              className="flex-1 flex items-center gap-2 px-2 py-1.5 cursor-pointer hover:bg-accent rounded-sm min-w-0"
              onClick={() => handleSelect(item.id)}
            >
              <span className="text-sm truncate">{item.name}</span>
              {isSelected && <Check className="h-4 w-4 text-primary shrink-0" />}
            </div>
          </div>
        </div>
        {hasChildren && isExpanded && (
          <div>
            {item.children!.map((child) => renderItem(child, level + 1))}
          </div>
        )}
      </div>
    );
  };

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          role="combobox"
          aria-expanded={open}
          disabled={disabled}
          className="w-full justify-between"
        >
          <span className={cn("truncate", !selectedItem && "text-muted-foreground")}>
            {selectedItem ? selectedItem.name : placeholder}
          </span>
          <ChevronRight className="ml-2 h-4 w-4 shrink-0 opacity-50 rotate-90" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[var(--radix-popover-trigger-width)] p-0" align="start">
        <div className="max-h-[300px] overflow-y-auto p-1">
          {items.length === 0 ? (
            <div className="px-2 py-4 text-sm text-muted-foreground text-center">
              No items available
            </div>
          ) : (
            <>
              <div
                className={cn(
                  "px-2 py-1.5 rounded-sm cursor-pointer hover:bg-accent transition-colors flex items-center gap-2",
                  !value && "bg-accent"
                )}
                onClick={() => handleSelect("__none__")}
              >
                <span className="text-sm">None</span>
                {!value && <Check className="h-4 w-4 text-primary shrink-0" />}
              </div>
              {items.map((item) => renderItem(item))}
            </>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}
