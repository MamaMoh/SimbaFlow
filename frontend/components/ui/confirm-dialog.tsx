import React, { startTransition } from "react";
import { Trash } from "lucide-react";
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogCancel,
  AlertDialogAction,
} from "@/components/ui/alert-dialog";
import { Icons } from "./icons";

interface ConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title?: string;
  description?: string;
  actionLabel?: string;
  isPending?: boolean;
  onConfirm: () => void | Promise<void>;
  cancelLabel?: string;
  className?: string;
}

export const ConfirmDialog: React.FC<ConfirmDialogProps> = ({
  open,
  onOpenChange,
  title = "Are you sure?",
  description = "This action cannot be undone.",
  actionLabel = "Confirm",
  cancelLabel = "Cancel",
  isPending,
  onConfirm,
  className,
}) => {
  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent className={className}>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>{description}</AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>{cancelLabel}</AlertDialogCancel>
          <AlertDialogAction
            className="bg-red-600 focus:ring-red-600"
            onClick={() => {
              startTransition(() => onConfirm());
            }}
            disabled={isPending}
          >
            {isPending ? (
              <div className="mr-2 h-4 w-4 bg-current/20 rounded animate-pulse" />
            ) : (
              <Trash className="mr-2 h-4 w-4" />
            )}
            <span>{actionLabel}</span>
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
};
