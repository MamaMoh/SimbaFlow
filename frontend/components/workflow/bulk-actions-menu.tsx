"use client";

import { useMemo, useState } from "react";
import { toast } from "sonner";
import { ChevronDown, ListChecks, Loader2, X, type LucideIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import type { BulkActionResult } from "@/lib/api/bulk-actions";

/** Something the action needs from the person, asked once for the whole group. */
export type BulkActionField = {
  name: string;
  label: string;
  type: "date" | "select" | "textarea";
  options?: { value: string; label: string }[];
  required?: boolean;
};

/** One action the Actions menu offers for the ticked candidates. */
export type BulkAction<Row> = {
  key: string;
  /** What it does, as the menu and the confirm button say it: "Book medical". */
  label: string;
  icon: LucideIcon;
  /** What has happened, for the summary: "Medical booked". */
  done: string;
  /** Null when this candidate may have it done; otherwise why not — see board-action-rules. */
  block: (row: Row) => string | null;
  fields?: BulkActionField[];
  run: (candidateIds: string[], values: Record<string, string>) => Promise<BulkActionResult>;
};

type Candidate = { id: string; fullName: string };

const plural = (n: number) => `${n} candidate${n === 1 ? "" : "s"}`;

/**
 * The board's Actions menu, beside Export: the row menu's actions, for every ticked candidate at
 * once.
 *
 * Each item says how many of the ticked candidates it applies to, so the answer to "can I do this
 * to all of them" is on the menu before anything is clicked. Picking one opens a check that names
 * who will have it done and who will not, with the reason, and asks once for anything the action
 * needs — a Tasheer date, a medical result. Only the candidates that pass are sent, and the server
 * checks each of them again.
 *
 * When it is done, the candidates it was done to are unticked and the rest stay ticked, so what is
 * left to deal with is what is still selected.
 */
export function BulkActionsMenu<Row extends Candidate>({
  selected,
  actions,
  onClear,
  onDone,
}: {
  /** The ticked candidates, as the board holds them. */
  selected: Row[];
  actions: BulkAction<Row>[];
  onClear: () => void;
  /** Called with the candidates the action was done to, after the board should refresh. */
  onDone: (succeededIds: string[]) => void;
}) {
  const [open, setOpen] = useState<BulkAction<Row> | null>(null);
  const count = selected.length;

  if (actions.length === 0) return null;

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button type="button" variant="outline" size="sm" className="h-8">
            <ListChecks className="mr-2 h-4 w-4" />
            Actions
            {count > 0 ? (
              <span className="ml-1.5 rounded-full bg-green-800 px-1.5 text-[11px] font-semibold leading-5 text-white">
                {count}
              </span>
            ) : null}
            <ChevronDown className="ml-1.5 h-3.5 w-3.5 opacity-60" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-64">
          <DropdownMenuLabel className="text-xs font-normal text-muted-foreground">
            {count === 0 ? "Tick candidates to act on several at once" : `${plural(count)} selected`}
          </DropdownMenuLabel>
          <DropdownMenuSeparator />
          {actions.map((action) => {
            const allowed = selected.filter((row) => action.block(row) === null).length;
            return (
              <DropdownMenuItem
                key={action.key}
                disabled={allowed === 0}
                onSelect={() => setOpen(action)}
              >
                <action.icon className="mr-2 h-4 w-4" />
                <span className="flex-1">{action.label}</span>
                {count > 0 ? (
                  <span className="ml-3 text-xs tabular-nums text-muted-foreground">
                    {allowed === count ? count : `${allowed} of ${count}`}
                  </span>
                ) : null}
              </DropdownMenuItem>
            );
          })}
          <DropdownMenuSeparator />
          <DropdownMenuItem disabled={count === 0} onSelect={onClear}>
            <X className="mr-2 h-4 w-4" />
            Clear selection
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      {open ? (
        <BulkActionDialog
          action={open}
          selected={selected}
          onClose={() => setOpen(null)}
          onDone={(ids) => {
            setOpen(null);
            onDone(ids);
          }}
        />
      ) : null}
    </>
  );
}

function BulkActionDialog<Row extends Candidate>({
  action,
  selected,
  onClose,
  onDone,
}: {
  action: BulkAction<Row>;
  selected: Row[];
  onClose: () => void;
  onDone: (succeededIds: string[]) => void;
}) {
  const [values, setValues] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);

  // Worked out once, when the check opens: these are the lists the person reads and agrees to.
  const { allowed, blocked } = useMemo(() => {
    const allowed: Row[] = [];
    const blocked: { row: Row; reason: string }[] = [];
    for (const row of selected) {
      const reason = action.block(row);
      if (reason === null) allowed.push(row);
      else blocked.push({ row, reason });
    }
    return { allowed, blocked };
  }, [action, selected]);

  const missing = (action.fields ?? []).some((f) => f.required && !values[f.name]?.trim());

  const confirm = async () => {
    setBusy(true);
    try {
      const result = await action.run(allowed.map((r) => r.id), values);
      const names = new Map(selected.map((r) => [r.id, r.fullName]));
      const refused = result.failed.map((f) => `${names.get(f.candidateId) ?? "A candidate"} (${f.error})`);

      if (result.succeeded.length > 0) {
        toast.success(`${action.done} for ${plural(result.succeeded.length)}.`, {
          description:
            refused.length > 0
              ? `${refused.length} could not be done: ${refused.join("; ")}`
              : blocked.length > 0
                ? `${blocked.length} not allowed and left as they are.`
                : undefined,
          duration: refused.length > 0 ? 12_000 : 5_000,
        });
      } else {
        toast.error(`Nothing was changed.`, {
          description: refused.join("; ") || undefined,
          duration: 12_000,
        });
      }
      onDone(result.succeeded);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : "The action could not be done");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open onOpenChange={(o) => { if (!o && !busy) onClose(); }}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>
            {action.label} for {plural(allowed.length)}?
          </DialogTitle>
          <DialogDescription>
            {blocked.length > 0
              ? `You selected ${selected.length}. The same rules apply as on each row's menu.`
              : "Every candidate you selected can have this done."}
          </DialogDescription>
        </DialogHeader>

        <div className="max-h-[42vh] space-y-4 overflow-y-auto pr-1 text-sm">
          <div>
            <p className="mb-1 font-medium text-green-800">✓ Will be done ({allowed.length})</p>
            <ul className="list-disc space-y-0.5 pl-5">
              {allowed.map((row) => (
                <li key={row.id}>{row.fullName}</li>
              ))}
            </ul>
          </div>
          {blocked.length > 0 ? (
            <div>
              <p className="mb-1 font-medium text-red-700">✕ Not allowed — left as they are ({blocked.length})</p>
              <ul className="list-disc space-y-0.5 pl-5">
                {blocked.map(({ row, reason }) => (
                  <li key={row.id}>
                    {row.fullName} <span className="text-muted-foreground">— {reason}</span>
                  </li>
                ))}
              </ul>
            </div>
          ) : null}
        </div>

        {(action.fields ?? []).length > 0 ? (
          <div className="space-y-3 border-t pt-4">
            <p className="text-xs text-muted-foreground">Applies to everyone above.</p>
            {(action.fields ?? []).map((field) => (
              <div key={field.name} className="space-y-1.5">
                <Label htmlFor={`bulk-${field.name}`}>
                  {field.label}
                  {field.required ? <span className="text-red-600"> *</span> : null}
                </Label>
                {field.type === "select" ? (
                  <Select
                    value={values[field.name] ?? ""}
                    onValueChange={(v) => setValues((s) => ({ ...s, [field.name]: v }))}
                  >
                    <SelectTrigger id={`bulk-${field.name}`}>
                      <SelectValue placeholder="Choose…" />
                    </SelectTrigger>
                    <SelectContent>
                      {(field.options ?? []).map((o) => (
                        <SelectItem key={o.value} value={o.value}>
                          {o.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : field.type === "date" ? (
                  <Input
                    id={`bulk-${field.name}`}
                    type="date"
                    value={values[field.name] ?? ""}
                    onChange={(e) => setValues((s) => ({ ...s, [field.name]: e.target.value }))}
                  />
                ) : (
                  <Textarea
                    id={`bulk-${field.name}`}
                    rows={2}
                    value={values[field.name] ?? ""}
                    onChange={(e) => setValues((s) => ({ ...s, [field.name]: e.target.value }))}
                  />
                )}
              </div>
            ))}
          </div>
        ) : null}

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={busy}>
            Cancel
          </Button>
          <Button
            className="bg-green-800 text-white hover:bg-green-900"
            disabled={busy || allowed.length === 0 || missing}
            onClick={() => void confirm()}
          >
            {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
            {action.label} for {allowed.length}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
