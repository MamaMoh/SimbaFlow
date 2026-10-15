"use client";

import { useEffect, useState } from "react";
import { Power, Trash2 } from "lucide-react";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";

export interface OffboardTarget {
  id: string;
  name: string;
  slug: string;
}

/**
 * Offboarding stops an agency and keeps everything it has. It is the reversible half of leaving,
 * so the dialog says so: the person clicking needs to know nothing is lost yet.
 */
export function OffboardDialog({
  target,
  onOpenChange,
  onConfirm,
  isPending,
}: {
  target: OffboardTarget | null;
  onOpenChange: (open: boolean) => void;
  onConfirm: (reason: string) => void;
  isPending: boolean;
}) {
  const [reason, setReason] = useState("");
  useEffect(() => setReason(""), [target?.id]);

  return (
    <AlertDialog open={!!target} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Offboard {target?.name}</AlertDialogTitle>
          <AlertDialogDescription>
            Its staff are signed out and can no longer sign in. Its candidates, documents and
            accounts are all kept, so you can export them or reactivate the agency later. Nothing is
            deleted.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <div className="space-y-2">
          <Label htmlFor="offboard-reason">Reason (optional)</Label>
          <Textarea
            id="offboard-reason"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Contract ended, licence revoked, moved to another system…"
            rows={3}
          />
        </div>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isPending}>Cancel</AlertDialogCancel>
          <Button variant="destructive" disabled={isPending} onClick={() => onConfirm(reason)}>
            <Power className="h-4 w-4 mr-2" />
            {isPending ? "Offboarding…" : "Offboard"}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

/**
 * Purging is the one step nobody can take back. The button stays disabled until the agency's URL
 * identifier is typed exactly, as with deleting a repository: it proves the person is looking at
 * the agency they mean, not the row above it. The server checks the same thing.
 */
export function PurgeDialog({
  target,
  onOpenChange,
  onConfirm,
  isPending,
}: {
  target: OffboardTarget | null;
  onOpenChange: (open: boolean) => void;
  onConfirm: (confirmSlug: string) => void;
  isPending: boolean;
}) {
  const [typed, setTyped] = useState("");
  useEffect(() => setTyped(""), [target?.id]);
  const matches = !!target && typed.trim() === target.slug;

  return (
    <AlertDialog open={!!target} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Purge {target?.name}</AlertDialogTitle>
          <AlertDialogDescription>
            This permanently deletes the agency&apos;s database, every uploaded and generated
            document, and its staff accounts. It cannot be undone. Make sure you have kept the
            export.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <div className="space-y-2">
          <Label htmlFor="purge-confirm">
            Type <code className="text-xs bg-muted px-1.5 py-0.5 rounded">{target?.slug}</code> to confirm
          </Label>
          <Input
            id="purge-confirm"
            value={typed}
            onChange={(e) => setTyped(e.target.value)}
            autoComplete="off"
          />
        </div>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isPending}>Cancel</AlertDialogCancel>
          <Button
            variant="destructive"
            disabled={!matches || isPending}
            onClick={() => onConfirm(typed.trim())}
          >
            <Trash2 className="h-4 w-4 mr-2" />
            {isPending ? "Purging…" : "Purge permanently"}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
