import { type Table } from "@tanstack/react-table";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useState } from "react";

interface DataTablePaginationProps<TData> {
  table: Table<TData>;
  pageSizeOptions?: number[];
  className?: string;
  labelRowsPerPage?: string;
  labelPage?: string;
  labelOf?: string;
  onPageChange?: (pageIndex: number) => void;
  onPageSizeChange?: (pageSize: number) => void;
  hideRowsPerPage?: boolean;
  hidePageIndicator?: boolean;
  hideFirstLast?: boolean;
  hidePrevNext?: boolean;
  showJumpToPage?: boolean;
  style?: React.CSSProperties;
}

export function DataTablePagination<TData>({
  table,
  pageSizeOptions = [10, 20, 30, 40, 50],
  className = "",
  labelRowsPerPage = "Rows per page",
  labelPage = "Page",
  labelOf = "of",
  onPageChange,
  onPageSizeChange,
  hideRowsPerPage = false,
  hidePageIndicator = false,
  hideFirstLast = false,
  hidePrevNext = false,
  showJumpToPage = false,
  style,
}: DataTablePaginationProps<TData>) {
  const pageIndex = table.getState().pagination.pageIndex;
  const pageSize = table.getState().pagination.pageSize;
  const pageCount = table.getPageCount();

  const [jumpValue, setJumpValue] = useState(pageIndex + 1);

  const handlePageChange = (newPage: number) => {
    const safePage = Math.max(0, Math.min(newPage, pageCount - 1));
    table.setPageIndex(safePage);
    setJumpValue(safePage + 1);
    onPageChange?.(safePage);
  };

  const handlePageSizeChange = (newSize: number) => {
    table.setPageSize(newSize);
    onPageSizeChange?.(newSize);
  };

  const getPageNumbers = (current: number, total: number) => {
    const pages: (number | string)[] = [];
    if (total <= 7) {
      for (let i = 1; i <= total; i++) pages.push(i);
    } else {
      if (current <= 4) {
        pages.push(1, 2, 3, 4, 5, "...", total);
      } else if (current >= total - 3) {
        pages.push(1, "...", total - 4, total - 3, total - 2, total - 1, total);
      } else {
        pages.push(1, "...", current - 1, current, current + 1, "...", total);
      }
    }
    return pages;
  };

  return (
    <div
      className={`flex w-full flex-col items-center justify-between gap-4 overflow-auto px-2 py-1 sm:flex-row sm:gap-8 ${className}`}
      style={style}
    >
      <div className="flex w-full flex-col items-center gap-4 sm:flex-row sm:gap-6 lg:gap-8">
        {!hidePageIndicator && (
          <div className="flex w-[100px] items-center justify-center text-sm font-medium text-muted-foreground">
            {labelPage} {pageIndex + 1} {labelOf} {pageCount}
          </div>
        )}

        {!hideRowsPerPage && (
          <div className="flex items-center space-x-2 ml-auto">
            <p className="whitespace-nowrap text-sm font-medium">
              {labelRowsPerPage}
            </p>
            <Select
              value={`${pageSize}`}
              onValueChange={(value) => handlePageSizeChange(Number(value))}
            >
              <SelectTrigger className="h-8 w-[70px]">
                <SelectValue placeholder={pageSize} />
              </SelectTrigger>
              <SelectContent side="top">
                {pageSizeOptions.map((size) => (
                  <SelectItem key={size} value={`${size}`}>
                    {size}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        )}

        <div className="flex items-center gap-1">
          {!hideFirstLast && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={pageIndex === 0}
              onClick={() => handlePageChange(0)}
            >
              First
            </Button>
          )}

          {!hidePrevNext && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={pageIndex === 0}
              onClick={() => handlePageChange(pageIndex - 1)}
            >
              Previous
            </Button>
          )}

          {getPageNumbers(pageIndex + 1, pageCount).map((n, i) =>
            typeof n === "number" ? (
              <Button
                type="button"
                key={n}
                variant={n === pageIndex + 1 ? "default" : "outline"}
                size="sm"
                className={n === pageIndex + 1 ? "font-bold" : ""}
                onClick={() => handlePageChange(n - 1)}
                aria-current={n === pageIndex + 1 ? "page" : undefined}
              >
                {n}
              </Button>
            ) : (
              <span
                key={`ellipsis-${i}`}
                className="px-1 text-xs text-muted-foreground"
              >
                ...
              </span>
            )
          )}

          {!hidePrevNext && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={pageIndex === pageCount - 1}
              onClick={() => handlePageChange(pageIndex + 1)}
            >
              Next
            </Button>
          )}

          {!hideFirstLast && (
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={pageIndex === pageCount - 1}
              onClick={() => handlePageChange(pageCount - 1)}
            >
              Last
            </Button>
          )}

          {!showJumpToPage && (
            <div className="flex items-center gap-2 ml-2">
              <label
                htmlFor="jump-to-page"
                className="text-sm font-medium text-muted-foreground"
              >
                Jump to:
              </label>
              <input
                type="number"
                min={1}
                max={pageCount}
                value={jumpValue}
                onChange={(e) => setJumpValue(Number(e.target.value))}
                onKeyDown={(e) => {
                  if (e.key === "Enter") handlePageChange(jumpValue - 1);
                }}
                className="ml-2 w-16 rounded border border-muted-foreground/50 px-2 py-1 text-sm text-center"
              />
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
