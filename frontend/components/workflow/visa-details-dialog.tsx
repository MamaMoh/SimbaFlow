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
import { Loader2, TriangleAlert } from "lucide-react";
import { setVisaDetails, type VisaFormGap } from "@/lib/api/candidates";

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
  onOpenChange,
  onFilled,
}: {
  open: boolean;
  /** One entry per candidate who is missing something. Candidates with nothing missing are not here. */
  gaps: VisaFormGap[];
  onOpenChange: (open: boolean) => void;
  /** Everything saved — carry on with whatever wanted the form. */
  onFilled: () => void;
}) {
  const [answers, setAnswers] = useState<Answers>({});
  const [busy, setBusy] = useState(false);

  // Cleared whenever a new set arrives, so a second run does not start pre-filled with the
  // previous one's answers against candidates it is not about.
  useEffect(() => {
    if (open) setAnswers({});
  }, [open, gaps]);

  const set = (candidateId: string, key: string) => (value: string) =>
    setAnswers((a) => ({ ...a, [candidateId]: { ...a[candidateId], [key]: value } }));

  const valueOf = (candidateId: string, key: string) => answers[candidateId]?.[key] ?? "";

  const blanks = gaps.flatMap((g) =>
    g.fields.filter((f) => !valueOf(g.id, f.key).trim()).map((f) => `${g.fullName}: ${f.label}`),
  );

  const save = async () => {
    if (blanks.length > 0) {
      toast.error(
        blanks.length === 1
          ? `Still needed — ${blanks[0]}`
          : `Still needed for ${blanks.length} fields`,
      );
      return;
    }

    setBusy(true);
    try {
      // One save per candidate, in sequence. A batch endpoint would be fewer round trips, but
      // these are a handful of records and a sequence means a failure halfway has still written
      // everything before it rather than rolling the typing back.
      for (const gap of gaps) await setVisaDetails(gap.id, answers[gap.id] ?? {});
      onOpenChange(false);
      onFilled();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not save the details");
    } finally {
      setBusy(false);
    }
  };

  const many = gaps.length > 1;

  return (
    <Dialog open={open} onOpenChange={(next) => !busy && onOpenChange(next)}>
      {/* Not dismissible: a stray click on the board behind used to throw away a screen of
          typing. Cancel and the X still close it. */}
      <DialogContent dismissible={false} className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>
            {many ? `Visa details for ${gaps.length} candidates` : "Visa details"}
          </DialogTitle>
          <DialogDescription>
            {many
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
                  {gap.fields.length} missing
                </span>
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
            disabled={busy}
          >
            Cancel
          </Button>
          <Button
            type="button"
            onClick={() => void save()}
            disabled={busy}
            className="gap-1.5 bg-green-800 text-white hover:bg-green-900"
          >
            {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
            {busy ? "Saving…" : "Save and print"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
