import { type Table } from "@tanstack/react-table";
import { toSentenceCase } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Checkbox } from "@/components/ui/checkbox";
import {
  ChevronDown,
  ChevronUp,
  RotateCcw,
  SlidersHorizontalIcon,
} from "lucide-react";
import { Tooltip, TooltipContent, TooltipTrigger } from "../ui/tooltip";
import type { TableLayout } from "@/lib/data-table/use-table-layout";

interface DataTableViewOptionsProps<TData> {
  table: Table<TData>;
  /** When the board remembers an arrangement, each row also gets a place in the order. */
  layout?: TableLayout;
}

/**
 * Which columns this table shows, and in what order.
 *
 * The order is set here as well as by dragging the headings, and the two are the same setting.
 * Dragging is quicker and is what people try; these arrows are what works on a touch screen, on
 * a keyboard, and for a column currently scrolled off the side of the table — which on a board
 * fifteen columns wide is most of the reason somebody wants to move one in the first place.
 */
export function DataTableViewOptions<TData>({
  table,
  layout,
}: DataTableViewOptionsProps<TData>) {
  const hideable = table
    .getAllColumns()
    .filter((column) => typeof column.accessorFn !== "undefined" && column.getCanHide());

  const visibleColumnsCount = hideable.filter((col) => col.getIsVisible()).length;

  // Listed in the order they are drawn, so the menu reads as the table reads.
  const order = layout?.columnOrder ?? [];
  const listed = layout
    ? [...hideable].sort((a, b) => order.indexOf(a.id) - order.indexOf(b.id))
    : hideable;

  const movable = listed.filter((c) => !layout?.locked(c.id));

  return (
    <DropdownMenu>
      <Tooltip>
        <TooltipTrigger asChild>
          <DropdownMenuTrigger asChild>
            <Button
              aria-label="Toggle columns"
              variant="outline"
              size="sm"
              className="ml-auto hidden h-8 lg:flex"
            >
              <SlidersHorizontalIcon className="mr-2 h-4 w-4" />
              View
            </Button>
          </DropdownMenuTrigger>
        </TooltipTrigger>
        <TooltipContent sideOffset={2}>
          {layout ? "Choose and arrange columns" : "Toggle visible columns"}
        </TooltipContent>
      </Tooltip>

      <DropdownMenuContent align="end" className="w-[260px]">
        <DropdownMenuLabel>
          {layout ? "Columns" : "Toggle columns"}
        </DropdownMenuLabel>
        {layout && (
          <p className="text-muted-foreground px-2 pb-1.5 text-xs">
            Drag a heading on the table, or use the arrows. Kept on this computer.
          </p>
        )}
        <DropdownMenuSeparator />

        <div className="max-h-[320px] overflow-y-auto">
          {listed.map((column) => {
            const isOnlyVisible = column.getIsVisible() && visibleColumnsCount === 1;
            const place = movable.findIndex((c) => c.id === column.id);
            const canMove = layout && place >= 0;

            return (
              // Not a DropdownMenuCheckboxItem: that closes the menu on every click, and
              // arranging a table means several clicks in a row.
              <div
                key={column.id}
                className="hover:bg-accent/50 flex items-center gap-2 rounded-sm px-2 py-1.5"
              >
                <Checkbox
                  id={`col-${column.id}`}
                  checked={column.getIsVisible()}
                  disabled={isOnlyVisible}
                  onCheckedChange={(value) => column.toggleVisibility(!!value)}
                />
                <label
                  htmlFor={`col-${column.id}`}
                  className="flex-1 cursor-pointer truncate text-sm capitalize"
                >
                  {toSentenceCase(column.id)}
                </label>

                {canMove && (
                  <div className="flex shrink-0 items-center">
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      className="h-6 w-6"
                      aria-label={`Move ${toSentenceCase(column.id)} left`}
                      disabled={place === 0}
                      onClick={() => layout.moveColumn(column.id, -1)}
                    >
                      <ChevronUp className="h-3.5 w-3.5" />
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      className="h-6 w-6"
                      aria-label={`Move ${toSentenceCase(column.id)} right`}
                      disabled={place === movable.length - 1}
                      onClick={() => layout.moveColumn(column.id, 1)}
                    >
                      <ChevronDown className="h-3.5 w-3.5" />
                    </Button>
                  </div>
                )}
              </div>
            );
          })}
        </div>

        {layout?.arranged && (
          <>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => layout.reset()}>
              <RotateCcw className="mr-2 h-4 w-4" />
              Back to the standard columns
            </DropdownMenuItem>
          </>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
