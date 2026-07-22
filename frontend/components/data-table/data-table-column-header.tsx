"use client";

import { type Column } from "@tanstack/react-table";
import { ArrowDownIcon, ArrowUpIcon, ArrowUpDownIcon } from "lucide-react";

import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";

interface DataTableColumnHeaderProps<TData, TValue>
  extends React.HTMLAttributes<HTMLDivElement> {
  column: Column<TData, TValue>;
  title: string;
}

export function DataTableColumnHeader<TData, TValue>({
  column,
  title,
  className,
}: DataTableColumnHeaderProps<TData, TValue>) {
  if (!column.getCanSort()) {
    return <div className={cn("text-[11px] font-semibold uppercase tracking-wide", className)}>{title}</div>;
  }

  const sorted = column.getIsSorted();

  return (
    <Button
      variant="ghost"
      size="sm"
      className={cn(
        "-ml-2 h-8 gap-1 px-2 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground hover:text-foreground",
        className,
      )}
      onClick={() => {
        if (!sorted) column.toggleSorting(false);
        else if (sorted === "asc") column.toggleSorting(true);
        else column.clearSorting();
      }}
    >
      <span>{title}</span>
      {sorted === "desc" ? (
        <ArrowDownIcon className="h-3.5 w-3.5" />
      ) : sorted === "asc" ? (
        <ArrowUpIcon className="h-3.5 w-3.5" />
      ) : (
        <ArrowUpDownIcon className="h-3.5 w-3.5 opacity-40" />
      )}
    </Button>
  );
}
