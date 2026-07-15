"use client";

import * as React from "react";
import { Calendar } from "./calendar"; // import your updated Calendar component
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { Button } from "@/components/ui/button";
import { Calendar as CalendarIcon } from "lucide-react";
import { Input } from "@/components/ui/input";
import { format } from "date-fns";
import { cn } from "@/lib/utils";

interface DatePickerProps {
  value?: Date | undefined;
  onChange?: (date: Date) => void;
  placeholder?: string;
  disabled?: boolean;
  className?: string;
  minDate?: Date;
  maxDate?: Date;
}

export function DatePicker({
  value,
  onChange,
  placeholder = "Select date",
  disabled = false,
  className,
  minDate,
  maxDate,
}: DatePickerProps) {
  const [open, setOpen] = React.useState(false);

  const fromYear = minDate?.getFullYear() ?? 1900;
  const toYear = maxDate?.getFullYear() ?? new Date().getFullYear() + 50;

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          className={cn(
            "w-full justify-between text-left font-normal pl-3 pr-3",
            className
          )}
          disabled={disabled}
        >
          {value ? format(value, "yyyy-MM-dd") : placeholder}
          <CalendarIcon className="ml-2 h-4 w-4 opacity-60" />
        </Button>
      </PopoverTrigger>
      <PopoverContent
        side="bottom"
        align="start"
        sideOffset={4}
        collisionPadding={8}
        className="p-0 w-auto"
      >
        <Calendar
          mode="single"
          captionLayout="dropdown"
          selected={value}
          onSelect={(date) => {
            onChange?.(date as Date);
            setOpen(false);
          }}
          initialFocus
          fromDate={minDate}
          toDate={maxDate}
          fromYear={fromYear}
          toYear={toYear}
        />
      </PopoverContent>
    </Popover>
  );
}
