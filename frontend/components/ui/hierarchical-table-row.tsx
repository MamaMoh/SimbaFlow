"use client";

import * as React from "react";
import { ChevronRight, ChevronDown } from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";

export interface HierarchicalItem {
  id: string;
  name: string;
  data: any; // The original data object
  children?: HierarchicalItem[];
  level?: number;
}

interface HierarchicalTableRowProps {
  item: HierarchicalItem;
  isExpanded: boolean;
  onToggleExpand: (id: string) => void;
  renderCell: (item: HierarchicalItem, columnId: string) => React.ReactNode;
  columns: Array<{ id: string; header: string }>;
  selectedRowId?: string;
  onRowClick?: (item: HierarchicalItem) => void;
}

export function HierarchicalTableRow({
  item,
  isExpanded,
  onToggleExpand,
  renderCell,
  columns,
  selectedRowId,
  onRowClick,
}: HierarchicalTableRowProps) {
  const hasChildren = item.children && item.children.length > 0;
  const level = item.level || 0;
  const isSelected = selectedRowId === item.id;

  return (
    <>
      <tr
        className={cn(
          "border-b transition-colors hover:bg-muted/50",
          isSelected && "bg-muted"
        )}
        onClick={() => onRowClick?.(item)}
      >
        {columns.map((column, colIndex) => {
          if (colIndex === 0) {
            // First column: show expand/collapse and indentation
            return (
              <td
                key={column.id}
                className="px-4 py-2"
                style={{ paddingLeft: `${level * 24 + 16}px` }}
              >
                <div className="flex items-center gap-2">
                  {hasChildren ? (
                    <Button
                      variant="ghost"
                      size="sm"
                      className="h-6 w-6 p-0"
                      onClick={(e) => {
                        e.stopPropagation();
                        onToggleExpand(item.id);
                      }}
                    >
                      {isExpanded ? (
                        <ChevronDown className="h-4 w-4" />
                      ) : (
                        <ChevronRight className="h-4 w-4" />
                      )}
                    </Button>
                  ) : (
                    <div className="w-6" />
                  )}
                  {renderCell(item, column.id)}
                </div>
              </td>
            );
          }
          return (
            <td key={column.id} className="px-4 py-2">
              {renderCell(item, column.id)}
            </td>
          );
        })}
      </tr>
      {hasChildren && isExpanded && (
        <>
          {item.children!.map((child) => (
            <HierarchicalTableRow
              key={child.id}
              item={child}
              isExpanded={isExpanded}
              onToggleExpand={onToggleExpand}
              renderCell={renderCell}
              columns={columns}
              selectedRowId={selectedRowId}
              onRowClick={onRowClick}
            />
          ))}
        </>
      )}
    </>
  );
}
