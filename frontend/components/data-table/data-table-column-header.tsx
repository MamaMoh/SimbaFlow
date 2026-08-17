import { type Column } from "@tanstack/react-table";

import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import {
  ArrowDownIcon,
  ArrowUpIcon,
  ArrowUpDownIcon,
} from "lucide-react";

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
  const sorted = column.getIsSorted() as false | "asc" | "desc";

  const handleSortToggle = () => {
    if (!sorted) {
      column.toggleSorting(false);
    } else if (sorted === "asc") {
      column.toggleSorting(true);
    } else {
      column.clearSorting();
    }
  };

  if (!column.getCanSort()) {
    return <div className={cn("flex items-center", className)}>{title}</div>;
  }

  return (
    <div className={cn("flex items-center space-x-1", className)}>
      <Button
        variant="ghost"
        size="sm"
        onClick={handleSortToggle}
        className="-ml-3 h-8 data-[state=open]:bg-accent"
        aria-label={
          sorted === "desc"
            ? `Sorted descending. Click to clear sorting.`
            : sorted === "asc"
            ? `Sorted ascending. Click to sort descending.`
            : `Not sorted. Click to sort ascending.`
        }
      >
        <span>{title}</span>
        {sorted === "desc" ? (
          <ArrowDownIcon className="ml-2 h-4 w-4" aria-hidden="true" />
        ) : sorted === "asc" ? (
          <ArrowUpIcon className="ml-2 h-4 w-4" aria-hidden="true" />
        ) : (
          <ArrowUpDownIcon className="ml-2 h-4 w-4" aria-hidden="true" />
        )}
      </Button>
    </div>
  );
}
