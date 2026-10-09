"use client";

import { useState } from "react";
import { toast } from "sonner";
import { DropdownMenuItem } from "@/components/ui/dropdown-menu";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { CornerUpLeft, Loader2, TriangleAlert } from "lucide-react";
import { moveBackStage, previewMoveBack, type MoveBackPreview } from "@/lib/api/workflow";
import { usePermissions } from "@/lib/tenant/tenant-provider";

/**
 * Putting a candidate back in the stage they came from, for the mis-click.
 *
 * It asks first, because it is not free: the statuses entered against the stage being left are
 * cleared, and nothing restores them. So the dialog does not say "are you sure" — it names the
 * stage they would go back to and lists every value that would be lost, which is the only form of
 * the question somebody can actually answer.
 *
 * The list is fetched when the dialog opens rather than guessed at here. It comes from the same
 * code that performs the move, so what is promised and what happens cannot drift apart.
 */
export function MoveBackStageItem({
  candidateId,
  candidateName,
  stageId,
  isMirror = false,
  onDone,
}: {
  candidateId: string;
  candidateName?: string;
  /**
   * The board this menu belongs to. Sent with the request so the server can refuse a move asked
   * for from a board the candidate is only mirrored onto. Omitted by the candidates list and the
   * candidate's own page, which are about the record rather than about a stage.
   */
  stageId?: string;
  /** This row is a mirror — the candidate is standing somewhere else, so this board cannot move them. */
  isMirror?: boolean;
  onDone?: () => void;
}) {
  const { hasPermission } = usePermissions();
  const [open, setOpen] = useState(false);
  const [preview, setPreview] = useState<MoveBackPreview | null>(null);
  const [blocked, setBlocked] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);

  if (!hasPermission("workflow.execute")) return null;
  // Offered only where the candidate actually is. A mirror row shows them without holding them,
  // and moving them from there walks them out of a stage this board is not looking at.
  if (isMirror) return null;

  const openDialog = async () => {
    setOpen(true);
    setPreview(null);
    setBlocked(null);
    setReason("");
    setLoading(true);
    try {
      setPreview(await previewMoveBack(candidateId, stageId));
    } catch (err) {
      setBlocked(err instanceof Error ? err.message : "Could not work out where they would go back to");
    } finally {
      setLoading(false);
    }
  };

  const confirm = async () => {
    setBusy(true);
    try {
      const done = await moveBackStage(candidateId, reason.trim() || undefined, stageId);
      toast.success(`Back in ${done.toStageName}`);
      setOpen(false);
      onDone?.();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not move the candidate back");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <DropdownMenuItem
        onSelect={(e) => {
          // The menu closing would unmount the dialog with it.
          e.preventDefault();
          void openDialog();
        }}
      >
        <CornerUpLeft className="mr-2 h-4 w-4 shrink-0" />
        Move back a stage
      </DropdownMenuItem>

      <Dialog open={open} onOpenChange={(next) => !busy && setOpen(next)}>
        {/* Not dismissible: this one destroys something, so it closes on Cancel or the X. */}
        <DialogContent dismissible={false} className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Move back a stage</DialogTitle>
            <DialogDescription>
              {preview
                ? `${candidateName ?? "This candidate"} goes from ${preview.fromStageName} back to ${preview.toStageName}.`
                : "Working out where they came from…"}
            </DialogDescription>
          </DialogHeader>

          {loading ? (
            <div className="flex items-center gap-2 py-4 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Checking…
            </div>
          ) : blocked ? (
            <p className="py-2 text-sm text-muted-foreground">{blocked}</p>
          ) : preview ? (
            <div className="space-y-4 py-1">
              {preview.clears.length > 0 ? (
                <div className="space-y-2 rounded-lg border border-amber-300 bg-amber-50 p-3 dark:border-amber-900 dark:bg-amber-950/40">
                  <p className="flex items-start gap-2 text-sm font-medium">
                    <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />
                    This clears what was entered in {preview.fromStageName}:
                  </p>
                  <ul className="ml-6 list-disc space-y-0.5 text-sm">
                    {preview.clears.map((c) => (
                      <li key={c.track}>
                        <span className="capitalize">{c.track}</span>
                        {" — "}
                        <span className="font-medium">{c.value}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">
                  Nothing was entered in {preview.fromStageName}, so nothing is lost.
                </p>
              )}

              <div className="space-y-1.5">
                <Label htmlFor="move-back-reason">Reason (optional)</Label>
                <Input
                  id="move-back-reason"
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  placeholder="Moved by mistake"
                />
              </div>
            </div>
          ) : null}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button
              type="button"
              variant="destructive"
              disabled={busy || loading || !preview}
              onClick={() => void confirm()}
              className="gap-1.5"
            >
              {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
              {preview ? `Move back to ${preview.toStageName}` : "Move back"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
