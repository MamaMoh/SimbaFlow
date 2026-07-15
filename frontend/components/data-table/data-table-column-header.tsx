import { type Column } from "@tanstack/react-table";

import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  ArrowDownIcon,
  ArrowUpIcon,
  ArrowUpDownIcon,
  EyeClosedIcon,
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

  return (
    <div className={cn("flex items-center space-x-2", className)}>
      <span>{title}</span>

      {column.getCanSort() && (
        <Button
          variant="ghost"
          size="sm"
          onClick={handleSortToggle}
          aria-label={
            sorted === "desc"
              ? "Sorted descending. Click to clear sorting."
              : sorted === "asc"
              ? "Sorted ascending. Click to sort descending."
              : "Not sorted. Click to sort ascending."
          }
          className="h-8 px-2"
        >
          {sorted === "desc" ? (
            <ArrowDownIcon className="h-4 w-4" aria-hidden="true" />
          ) : sorted === "asc" ? (
            <ArrowUpIcon className="h-4 w-4" aria-hidden="true" />
          ) : (
            <ArrowUpDownIcon className="h-4 w-4" aria-hidden="true" />
          )}
        </Button>
      )}

      {/* Dropdown menu */}
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            size="sm"
            className="h-8 px-2"
            aria-label="Column options"
          >
            ⋮
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          {column.getCanSort() && (
            <>
              <DropdownMenuItem onClick={() => column.toggleSorting(false)}>
                <ArrowUpIcon className="mr-2 h-3.5 w-3.5 text-muted-foreground/70" />
                Sort Asc
              </DropdownMenuItem>
              <DropdownMenuItem onClick={() => column.toggleSorting(true)}>
                <ArrowDownIcon className="mr-2 h-3.5 w-3.5 text-muted-foreground/70" />
                Sort Desc
              </DropdownMenuItem>
              <DropdownMenuItem onClick={() => column.clearSorting()}>
                <ArrowUpDownIcon className="mr-2 h-3.5 w-3.5 text-muted-foreground/70" />
                Clear Sort
              </DropdownMenuItem>
              <DropdownMenuSeparator />
            </>
          )}

          <DropdownMenuItem onClick={() => column.toggleVisibility(false)}>
            <EyeClosedIcon className="mr-2 h-3.5 w-3.5 text-muted-foreground/70" />
            Hide Column
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
