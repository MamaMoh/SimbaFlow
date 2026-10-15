"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";
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
import { PhoneInputField } from "@/components/ui/phone-input";
import { ClipboardPaste, Loader2, TriangleAlert } from "lucide-react";
import { setVisaDetails, type VisaFormGap } from "@/lib/api/candidates";
import { PasteFromPortal } from "@/components/site-bridge/paste-from-portal";
import type { FieldKey } from "@/lib/site-bridge/manifest";

/** The boxes that are dates rather than text, so the browser offers a picker. */
const DATE_FIELDS = new Set(["passportIssueDate", "passportExpiryDate"]);

type Answers = Record<string, Record<string, string>>;

/**
 * Filling in what the enjaze form is still waiting for, at the moment someone asks for it.
 *
 * Printing used to refuse and name the gaps: "Almaz Kebede and Hanan Ahmed have
 * visa details still missing." Correct, and useless — the desk then opened each candidate, found
 * the field, typed it, went back to the board, re-ticked twenty rows and pressed print again. The
 * refusal knew exactly what was wanted and made somebody go elsewhere to supply it.
 *
 * So it is asked for here instead. One block per candidate, named, holding only that candidate's
 * gaps — two people missing different things get different boxes, and nobody is asked to retype
 * a field that is already on file. The sponsor's phone and address ride along with a sponsor
 * name, because whoever has the name in front of them has the rest of the sponsor too.
 *
 * Every box is required: the form cannot print without them, which is the only reason this
 * dialog is open. What is saved sticks — these go onto the candidate's record, not into the
 * print — so a run abandoned half way has still saved the typing.
 */
export function VisaDetailsDialog({
  open,
  gaps,
  requireAll = true,
  onOpenChange,
  onFilled,
}: {
  open: boolean;
  /**
   * Whether every box has to be filled before it will save. See the note on the host's `Ask`:
   * true when a form is waiting on these, false when somebody opened the dialog themselves.
   */
  requireAll?: boolean;
  /** One entry per candidate who is missing something. Candidates with nothing missing are not here. */
  gaps: VisaFormGap[];
  onOpenChange: (open: boolean) => void;
  /**
   * Everything saved. `print` says which button did it: true carries on with whatever wanted
   * the form, false means the typing was banked and nothing else should happen.
   */
  onFilled: (print: boolean) => void;
}) {
  const [answers, setAnswers] = useState<Answers>({});
  const [busy, setBusy] = useState<"save" | "print" | null>(null);
  /** Whose block the contract page is being pasted into, or null when nothing is being pasted. */
  const [pasteFor, setPasteFor] = useState<string | null>(null);

  // Cleared whenever a new set arrives, so a second run does not start pre-filled with the
  // previous one's answers against candidates it is not about.
  useEffect(() => {
    if (open) setAnswers({});
  }, [open, gaps]);

  const set = (candidateId: string, key: string) => (value: string) =>
    setAnswers((a) => ({ ...a, [candidateId]: { ...a[candidateId], [key]: value } }));

  const valueOf = (candidateId: string, key: string) => answers[candidateId]?.[key] ?? "";

  const many = gaps.length > 1;

  /**
   * The employer block off the portal, into the boxes that are asking for it.
   *
   * These four — the employer's name, the address, the visa number and the mobile — are
   * printed together on the contract page and are exactly what this dialog is open about, and
   * the desk was retyping them out of another tab. Only the boxes this candidate is actually
   * missing are filled: a value for a field already on file would be writing over a record
   * from a page nobody asked us to trust that far.
   *
   * What the page carried and this candidate was not asked for is counted rather than
   * discarded silently, so a block pasted against the wrong person reads as "nothing to fill"
   * instead of looking like it worked.
   */
  const pasteInto = (candidateId: string, values: Partial<Record<FieldKey, string>>) => {
    const wanted = gaps.find((g) => g.id === candidateId)?.fields ?? [];
    const filled: Record<string, string> = {};
    for (const field of wanted) {
      const value = values[field.key as FieldKey];
      if (value) filled[field.key] = value;
    }

    const count = Object.keys(filled).length;
    if (count === 0) {
      toast.warning("Nothing on that page matches what this candidate is missing");
      return;
    }

    setAnswers((a) => ({ ...a, [candidateId]: { ...a[candidateId], ...filled } }));
    toast.success(`Filled ${count} field${count === 1 ? "" : "s"} — check them before saving`);
  };

  const blanks = gaps.flatMap((g) =>
    g.fields.filter((f) => !valueOf(g.id, f.key).trim()).map((f) => `${g.fullName}: ${f.label}`),
  );

  const save = async (print: boolean) => {
    if (requireAll && blanks.length > 0) {
      toast.error(
        blanks.length === 1
          ? `Still needed — ${blanks[0]}`
          : `Still needed for ${blanks.length} fields`,
      );
      return;
    }

    setBusy(print ? "print" : "save");
    try {
      // One save per candidate, in sequence. A batch endpoint would be fewer round trips, but
      // these are a handful of records and a sequence means a failure halfway has still written
      // everything before it rather than rolling the typing back.
      for (const gap of gaps) {
        // An empty box is a box nobody typed in, not an instruction to clear what is on file.
        const typed = Object.fromEntries(
          Object.entries(answers[gap.id] ?? {}).filter(([, v]) => v.trim().length > 0),
        );
        if (Object.keys(typed).length === 0) continue;
        await setVisaDetails(gap.id, typed);
      }
      if (!print) toast.success(many ? "Details saved" : `Saved for ${gaps[0]?.fullName}`);
      onFilled(print);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not save the details");
    } finally {
      setBusy(null);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(next) => !busy && onOpenChange(next)}>
      {/* Once anything is typed into it, a stray click on the board behind stops closing
          it — that used to be a screen of typing gone. Untouched, it closes like any other. */}
      <DialogContent dismissible={false} className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>
            {!requireAll
              ? "Contract parties"
              : many
                ? `Visa details for ${gaps.length} candidates`
                : "Visa details"}
          </DialogTitle>
          <DialogDescription>
            {!requireAll
              ? "Paste the contract parties block from the portal, or type what you have. Blank boxes are left as they are."
              : many
                ? "The enjaze form cannot print without these. Everything typed here is saved onto the candidate."
                : `The enjaze form for ${gaps[0]?.fullName ?? "this candidate"} still needs these.`}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-5 py-2">
          {gaps.map((gap) => (
            <div key={gap.id} className="space-y-3">
              {/* The name, always — on a single candidate it confirms who is being edited, and
                  on a batch it is the only thing separating one set of boxes from the next. */}
              <div className="flex items-center gap-2 border-b pb-1.5">
                <TriangleAlert className="h-3.5 w-3.5 shrink-0 text-amber-600" />
                <span className="text-sm font-semibold">{gap.fullName}</span>
                <span className="text-xs text-muted-foreground">
                  {requireAll ? `${gap.fields.length} missing` : "from the contract page"}
                </span>
                {/* Per candidate, not per dialog: on a batch there is one contract page per
                    person, and a single button would have to ask which of them it was for. */}
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="ml-auto h-7 gap-1.5 text-xs"
                  onClick={() => setPasteFor(gap.id)}
                >
                  <ClipboardPaste className="h-3.5 w-3.5" />
                  Paste from the portal
                </Button>
              </div>

              <div className="grid gap-3 sm:grid-cols-2">
                {gap.fields.map((field) => (
                  <div
                    key={field.key}
                    className={
                      "space-y-1.5" + (field.key === "sponsorName" ? " sm:col-span-2" : "")
                    }
                  >
                    <Label htmlFor={`${gap.id}-${field.key}`}>{field.label}</Label>
                    {field.key === "sponsorPhone" ? (
                      <PhoneInputField
                        country="sa"
                        value={valueOf(gap.id, field.key)}
                        onChange={set(gap.id, field.key)}
                      />
                    ) : (
                      <Input
                        id={`${gap.id}-${field.key}`}
                        type={DATE_FIELDS.has(field.key) ? "date" : "text"}
                        value={valueOf(gap.id, field.key)}
                        onChange={(e) => set(gap.id, field.key)(e.target.value)}
                      />
                    )}
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>

        <DialogFooter>
          <Button
            type="button"
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={busy !== null}
          >
            Cancel
          </Button>
          {/* Saving without printing. Somebody taking the numbers down over the phone wants them
              on the record now and the form later, and the only way to do that was to print a
              copy nobody needed. Both buttons write the same thing; only one of them goes on to
              produce a document. */}
          <Button
            type="button"
            variant={requireAll ? "outline" : "default"}
            onClick={() => void save(false)}
            disabled={busy !== null}
            className={
              "gap-1.5" + (requireAll ? "" : " bg-green-800 text-white hover:bg-green-900")
            }
          >
            {busy === "save" ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
            {busy === "save" ? "Saving…" : "Save"}
          </Button>
          {/* Only when something is waiting on these. Opened by hand there is nothing to print,
              and a button offering to produce a document nobody asked for is a document
              nobody wanted. */}
          {requireAll ? (
            <Button
              type="button"
              onClick={() => void save(true)}
              disabled={busy !== null}
              className="gap-1.5 bg-green-800 text-white hover:bg-green-900"
            >
              {busy === "print" ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
              {busy === "print" ? "Saving…" : "Save and print"}
            </Button>
          ) : null}
        </DialogFooter>
      </DialogContent>

      {/* Outside the content, so closing it returns to this dialog rather than taking it with
          it. The employer block is the page it expects; a candidate page pasted here fills
          nothing, which it says. */}
      <PasteFromPortal
        open={pasteFor !== null}
        onOpenChange={(next) => {
          if (!next) setPasteFor(null);
        }}
        onApply={(values) => {
          if (pasteFor) pasteInto(pasteFor, values);
          setPasteFor(null);
        }}
      />
    </Dialog>
  );
}
