"use client";

import * as React from "react";
import { Calendar, CalendarDayButton } from "./calendar";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { Button } from "@/components/ui/button";
import { Calendar as CalendarIcon } from "lucide-react";
import { format } from "date-fns";
import { cn } from "@/lib/utils";
import {
  normalizeRange,
  isSameDay,
  getRangeBounds,
  isRangeStart,
  isRangeEnd,
  isRangeMiddle,
  isBeforeDay,
  isAfterDay,
} from "@/lib/utils/date-range";
import type { DateRange } from "react-day-picker";

const TRANSITION_MS = 200;

export interface DateRangePickerProps {
  value?: { from?: Date; to?: Date } | undefined;
  onChange?: (range: { from?: Date; to?: Date } | undefined) => void;
  /** Called when the popover opens or closes. Use to clear pending local state when closed. */
  onOpenChange?: (open: boolean) => void;
  placeholder?: string;
  disabled?: boolean;
  className?: string;
  /** Earliest selectable date (overrides disablePastDates if both set) */
  minDate?: Date;
  /** Latest selectable date */
  maxDate?: Date;
  /** When true, disables all dates before today. Ignored if minDate is set. */
  disablePastDates?: boolean;
}

function getEffectiveMinDate(
  minDate: Date | undefined,
  disablePastDates: boolean
): Date | undefined {
  if (minDate) return minDate;
  if (disablePastDates) {
    const today = new Date();
    return new Date(today.getFullYear(), today.getMonth(), today.getDate());
  }
  return undefined;
}

export function DateRangePicker({
  value,
  onChange,
  onOpenChange: onOpenChangeProp,
  placeholder = "Select date range",
  disabled = false,
  className,
  minDate: minDateProp,
  maxDate,
  disablePastDates = false,
}: DateRangePickerProps) {
  const [open, setOpen] = React.useState(false);
  /** Local selection: first click sets from, second sets to. Only propagated to parent when complete. */
  const [internalRange, setInternalRange] = React.useState<
    { from?: Date; to?: Date } | undefined
  >(undefined);
  const [hoveredDate, setHoveredDate] = React.useState<Date | undefined>(
    undefined
  );
  const isSelectingEndRef = React.useRef(false);

  const minDate = React.useMemo(
    () => getEffectiveMinDate(minDateProp, disablePastDates),
    [minDateProp, disablePastDates]
  );

  const fromYear = minDate?.getFullYear() ?? 1900;
  const toYear = maxDate?.getFullYear() ?? new Date().getFullYear() + 50;

  const isSelectingEnd = Boolean(internalRange?.from && !internalRange?.to);

  /** Sync internal range from prop when popover opens */
  React.useEffect(() => {
    if (open) {
      const initial = value?.from
        ? { from: value.from, to: value.to }
        : undefined;
      setInternalRange(initial);
      setHoveredDate(undefined);
      isSelectingEndRef.current = Boolean(initial?.from && !initial?.to);
    }
  }, [open, value?.from, value?.to]);

  /** Calendar selection: normalized internal range (from <= to) */
  const committedRange: DateRange | undefined = React.useMemo(() => {
    const normalized = normalizeRange({
      from: internalRange?.from,
      to: internalRange?.to,
    });
    if (!normalized?.from) return undefined;
    return { from: normalized.from, to: normalized.to };
  }, [internalRange?.from, internalRange?.to]);

  const disabledMatcher = React.useCallback(
    (date: Date) => {
      if (minDate && isBeforeDay(date, minDate)) return true;
      if (maxDate && isAfterDay(date, maxDate)) return true;
      return false;
    },
    [minDate, maxDate]
  );

  const previewRangeModifiers = React.useMemo(() => {
    const mods: Record<string, (date: Date) => boolean> = {};
    if (hoveredDate) {
      mods.preview_hovered = (date: Date) => isSameDay(date, hoveredDate);
    }
    if (internalRange?.from && !internalRange?.to && hoveredDate) {
      const [low, high] = getRangeBounds(internalRange.from, hoveredDate);
      mods.preview_range_start = (date: Date) => isRangeStart(date, low, high);
      mods.preview_range_end = (date: Date) => isRangeEnd(date, low, high);
      mods.preview_range_middle = (date: Date) =>
        isRangeMiddle(date, low, high);
    }
    return mods;
  }, [internalRange?.from, internalRange?.to, hoveredDate]);

  const previewRangeClassNames = React.useMemo(
    () => ({
      preview_range_start:
        "rdp-range-start rounded-l-full bg-primary/20 text-primary-foreground shadow-sm shadow-primary/20",
      preview_range_middle:
        "rdp-range-middle rounded-none bg-primary/10 text-foreground",
      preview_range_end:
        "rdp-range-end rounded-r-full bg-primary/20 text-primary-foreground shadow-sm shadow-primary/20",
      preview_hovered:
        "rdp-preview-hovered rounded-full ring-2 ring-primary/50 ring-offset-2 ring-offset-background bg-primary/20 text-primary-foreground font-medium",
    }),
    []
  );

  /** Trigger label: when open, show internal range (incl. partial); when closed, show committed value only */
  const displayValue = React.useMemo(() => {
    const source = open ? internalRange : value;
    if (!source?.from) return placeholder;
    if (source.from && source.to) {
      return `${format(source.from, "MMM d, yyyy")} – ${format(source.to, "MMM d, yyyy")}`;
    }
    return format(source.from, "MMM d, yyyy");
  }, [open, internalRange, value, placeholder]);

  const handleOpenChange = React.useCallback(
    (newOpen: boolean) => {
      if (newOpen) {
        isSelectingEndRef.current = false;
        setOpen(true);
        setHoveredDate(undefined);
        onOpenChangeProp?.(true);
        return;
      }
      if (isSelectingEndRef.current || (internalRange?.from && !internalRange?.to))
        return;
      setOpen(false);
      setHoveredDate(undefined);
      setInternalRange(undefined);
      onOpenChangeProp?.(false);
    },
    [internalRange?.from, internalRange?.to, onOpenChangeProp]
  );

  const handleSelect = React.useCallback(
    (range: DateRange | undefined) => {
      const wasSelectingEnd = isSelectingEndRef.current;

      if (!range || (range.from === undefined && range.to === undefined)) {
        isSelectingEndRef.current = false;
        setInternalRange(undefined);
        onChange?.(undefined);
        return;
      }

      if (range.from !== undefined && range.to === undefined) {
        isSelectingEndRef.current = true;
        setInternalRange({ from: range.from, to: undefined });
        return;
      }

      if (range.from && range.to) {
        if (isSameDay(range.from, range.to) && !wasSelectingEnd) {
          isSelectingEndRef.current = true;
          setInternalRange({ from: range.from, to: undefined });
          return;
        }
        const normalized = normalizeRange({
          from: range.from,
          to: range.to,
        });
        if (normalized) {
          isSelectingEndRef.current = false;
          setInternalRange({ from: normalized.from, to: normalized.to });
          onChange?.({ from: normalized.from, to: normalized.to });
          setTimeout(() => {
            setOpen(false);
            setHoveredDate(undefined);
            setInternalRange(undefined);
            onOpenChangeProp?.(false);
          }, TRANSITION_MS);
        }
      }
    },
    [onChange, onOpenChangeProp]
  );

  const handleDayMouseEnter = React.useCallback((date: Date) => {
    if (isSelectingEnd) setHoveredDate(date);
  }, [isSelectingEnd]);

  const handleDayMouseLeave = React.useCallback(() => {
    setHoveredDate(undefined);
  }, []);

  const DayButtonWithHover = React.useMemo(
    () =>
      function DayButtonWithHover(
        props: React.ComponentProps<typeof CalendarDayButton>
      ) {
        return (
          <span
            className="contents"
            onMouseEnter={() => handleDayMouseEnter(props.day.date)}
            onMouseLeave={handleDayMouseLeave}
          >
            <CalendarDayButton {...props} />
          </span>
        );
      },
    [handleDayMouseEnter, handleDayMouseLeave]
  );

  return (
    <Popover open={open} onOpenChange={handleOpenChange} modal={true}>
      <PopoverTrigger asChild>
        <Button
          variant="outline"
          className={cn(
            "w-full justify-between text-left font-normal pl-3 pr-3 rounded-lg border border-input bg-background text-sm",
            "hover:bg-accent/50 hover:border-accent-foreground/20",
            "focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2",
            "transition-colors duration-200 ease-in-out",
            className
          )}
          disabled={disabled}
          type="button"
          aria-haspopup="dialog"
          aria-expanded={open}
        >
          <span className="truncate">{displayValue}</span>
          <CalendarIcon className="ml-2 h-4 w-4 shrink-0 opacity-60" />
        </Button>
      </PopoverTrigger>
      <PopoverContent
        side="bottom"
        align="start"
        sideOffset={8}
        collisionPadding={12}
        className="p-0 w-auto rounded-xl border border-border/80 bg-background shadow-lg shadow-black/5"
        onInteractOutside={(e) => {
          if (isSelectingEndRef.current || (internalRange?.from && !internalRange?.to))
            e.preventDefault();
        }}
        onEscapeKeyDown={(e) => {
          if (isSelectingEndRef.current || (internalRange?.from && !internalRange?.to))
            e.preventDefault();
        }}
      >
        <Calendar
          mode="range"
          captionLayout="dropdown"
          selected={committedRange}
          onSelect={handleSelect}
          autoFocus
          startMonth={minDate}
          endMonth={maxDate}
          fromYear={fromYear}
          toYear={toYear}
          numberOfMonths={2}
          defaultMonth={internalRange?.from || internalRange?.to || value?.from || value?.to || new Date()}
          disabled={disabledMatcher}
          components={{ DayButton: DayButtonWithHover }}
          modifiers={previewRangeModifiers}
          modifiersClassNames={previewRangeClassNames}
        />
      </PopoverContent>
    </Popover>
  );
}
