"use client";

import { useState } from "react";
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
import { ClipboardPaste, FileText, Loader2, Upload } from "lucide-react";
import {
  generateCandidateContract,
  generateCandidateVisaForm,
  readContractDetails,
  setVisaDetails,
  uploadCandidateDocument,
} from "@/lib/api/candidates";
import { updateWorkflowStatus } from "@/lib/api/workflow";
import { PasteFromPortal } from "@/components/site-bridge/paste-from-portal";
import type { FieldKey } from "@/lib/site-bridge/manifest";
import { toDestinationPhone } from "@/lib/site-bridge/read";

/** Saudi placements run on a contract the parties sign; elsewhere we produce one. */
function isSaudi(country?: string | null) {
  return (country ?? "").toLowerCase().includes("saudi");
}

type Props = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  candidateId: string;
  candidateName: string;
  countryOfTravel?: string | null;
  onDone: () => void;
};

/** What the sponsor's identifier is called depends on who the sponsor is. */
type SponsorKind = "unknown" | "individual" | "company";

const blank = {
  contractNo: "",
  visaNumber: "",
  sponsorName: "",
  sponsorIdNumber: "",
  sponsorPhone: "",
  sponsorAddress: "",
};

/**
 * Marking a candidate Ready is the point the visa file starts, so it collects what the embassy
 * desk needs rather than flipping a status and leaving them to find the gaps later:
 *
 *  - Saudi Arabia: the signed contract is uploaded, because one already exists between the parties.
 *  - Anywhere else: the contract is generated here from the candidate, partner and agency details.
 *
 * Either way the contract, visa and sponsor details are captured, the visa track opens at Ready,
 * and the enjaze form is produced — the same document the bot sends out.
 *
 * The Saudi contract is read as soon as it is chosen and the fields it yields are filled in. That
 * is six long digit strings and an Arabic address that were previously copied by eye out of the
 * very PDF being attached. What is read is only ever offered: every field stays editable, nothing
 * already typed is overwritten, and a scan the reader cannot see into simply fills nothing.
 *
 * The E number is not asked for here, and nor is the agent. The E number comes off the consular
 * application rather than the contract, which is usually days later than this — asking at this
 * moment got a blank from someone who did not have it yet. The enjaze form asks for it when
 * somebody tries to print one, which is the moment it is both needed and known.
 */
export function MarkReadyDialog({
  open,
  onOpenChange,
  candidateId,
  candidateName,
  countryOfTravel,
  onDone,
}: Props) {
  const saudi = isSaudi(countryOfTravel);
  const [form, setForm] = useState(blank);
  const [contractFile, setContractFile] = useState<File | null>(null);
  const [sponsorKind, setSponsorKind] = useState<SponsorKind>("unknown");
  const [reading, setReading] = useState(false);
  /** Whether the portal paste is open. Nothing is being read from a file while it is. */
  const [pasting, setPasting] = useState(false);
  const [busy, setBusy] = useState(false);
  const [step, setStep] = useState<string | null>(null);

  const set = (field: keyof typeof blank) => (value: string) =>
    setForm((f) => ({ ...f, [field]: value }));

  const reset = () => {
    setForm(blank);
    setContractFile(null);
    setSponsorKind("unknown");
    setPasting(false);
    setStep(null);
  };

  /**
   * The same details off the portal, for the contract nobody has yet.
   *
   * The signed contract is what this dialog would rather read, but it arrives days after the
   * employer and the visa are known — and until it does, the desk had the contract parties page
   * open in another tab and was retyping an Arabic address out of it. Pasting that page fills
   * the same boxes the contract would have.
   *
   * Filled the same way as the contract reader: only boxes still empty, nothing typed is
   * overwritten, and every box stays editable afterwards. The contract number is not on that
   * block, so it stays for whoever has it.
   */
  const fillFromPortal = (values: Partial<Record<FieldKey, string>>) => {
    const found = [
      ["visaNumber", values.visaNumber],
      ["sponsorName", values.sponsorName],
      ["sponsorIdNumber", values.sponsorIdNumber],
      // The portal prints the sponsor's mobile the way Saudi Arabia writes it — a trunk zero
      // and no country code — and the box below is already set to Saudi Arabia.
      ["sponsorPhone", values.sponsorPhone ? toDestinationPhone(values.sponsorPhone) : undefined],
      ["sponsorAddress", values.sponsorAddress],
    ] as const;

    let filled = 0;
    setForm((f) => {
      const next = { ...f };
      for (const [field, value] of found) {
        if (value && !next[field]) {
          next[field] = value;
          filled += 1;
        }
      }
      return next;
    });

    // Counted from what the page carried rather than from what went in: a page pasted against
    // a form already filled in by hand reads as "nothing to fill" instead of looking like it
    // failed.
    const carried = found.filter(([, v]) => v).length;
    if (carried === 0) {
      toast.warning("Nothing on that page matches these boxes — copy the contract parties block");
      return;
    }
    if (filled === 0) {
      toast.info("Everything that page carries is already filled in");
      return;
    }
    toast.success(`Filled ${filled} ${filled === 1 ? "field" : "fields"} — check them before saving`);
  };

  const close = (next: boolean) => {
    if (!next) reset();
    onOpenChange(next);
  };

  /** Fills the fields the document knows, leaving anything already typed alone. */
  const readContract = async (file: File) => {
    setReading(true);
    try {
      const d = await readContractDetails(file);
      const found = [
        ["contractNo", d.contractNumber],
        ["visaNumber", d.visaNumber],
        ["sponsorName", d.sponsorName],
        ["sponsorIdNumber", d.sponsorIdNumber],
        ["sponsorPhone", d.sponsorPhone],
        ["sponsorAddress", d.sponsorAddress],
      ] as const;

      setForm((f) => {
        const next = { ...f };
        for (const [field, value] of found) {
          if (value && !next[field]) next[field] = value;
        }
        return next;
      });

      const count = found.filter(([, v]) => v).length;
      if (count === 0) {
        toast.info("Nothing could be read from that file — fill the details in below");
        return;
      }
      setSponsorKind(d.sponsorIsCompany ? "company" : "individual");
      toast.success(`Read ${count} ${count === 1 ? "detail" : "details"} from the contract`);
    } catch (err) {
      // The contract still gets filed and the details still get typed; this only ever saved
      // keystrokes, so a reader that fails is not a reason to stop.
      toast.warning(err instanceof Error ? err.message : "Could not read the contract");
    } finally {
      setReading(false);
    }
  };

  const choose = (file: File | null) => {
    setContractFile(file);
    setSponsorKind("unknown");
    if (file) void readContract(file);
  };

  const submit = async () => {
    if (saudi && !contractFile) {
      toast.error("Attach the signed contract before marking Ready");
      return;
    }

    // Checked here rather than left to the enjaze step at the end: by then the candidate is
    // already Ready and the visa track is open, so a refusal reads as a failure of the whole
    // thing when in fact everything but the last document went through.
    const required: [string, string][] = [
      ["Visa number", form.visaNumber],
      ["Sponsor name", form.sponsorName],
      ["Sponsor ID", form.sponsorIdNumber],
    ];
    const blankFields = required.filter(([, v]) => !v.trim()).map(([label]) => label);
    if (blankFields.length > 0) {
      toast.error(`Still needed: ${blankFields.join(", ")}`);
      return;
    }

    setBusy(true);
    try {
      if (saudi && contractFile) {
        setStep("Filing the contract…");
        await uploadCandidateDocument(candidateId, contractFile, 2);
      } else {
        setStep("Generating the contract…");
        await generateCandidateContract(candidateId);
      }

      setStep("Saving visa details…");
      await setVisaDetails(candidateId, form);

      setStep("Marking Ready…");
      await updateWorkflowStatus(candidateId, "status", "Ready");
      // The visa file is open from this moment; leaving the track blank made the embassy desk
      // set it by hand before they could do anything.
      await updateWorkflowStatus(candidateId, "visa", "Ready");

      setStep("Producing the enjaze form…");
      // The candidate is Ready from the step above whatever happens here. The E number is not
      // asked for on this form and usually arrives later, so the enjaze failing is the ordinary
      // case rather than an error — it is reported as the one thing still outstanding, and the
      // Print enjaze button asks for what it needs when somebody gets there.
      try {
        await generateCandidateVisaForm(candidateId);
        toast.success(`${candidateName} is Ready — contract and enjaze are on file`);
      } catch {
        toast.success(`${candidateName} is Ready — the enjaze prints once the E number is in`);
      }

      close(false);
      onDone();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not mark Ready");
    } finally {
      setBusy(false);
      setStep(null);
    }
  };

  const sponsorIdLabel =
    sponsorKind === "company" ? "Licence no" : sponsorKind === "individual" ? "National ID" : "Sponsor ID";

  return (
    <Dialog open={open} onOpenChange={close}>
      {/* A long form: once anything is typed into it, a click on the page behind stops
          closing it. Untouched, it closes like any other. */}
      <DialogContent dismissible={false} className="max-h-[90vh] overflow-y-auto sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Mark Ready</DialogTitle>
          <DialogDescription>
            {saudi
              ? "Saudi placement — attach the signed contract and the details are read from it."
              : `${countryOfTravel || "This destination"} — the contract is generated here.`}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-2">
          {saudi ? (
            <div className="space-y-1.5">
              <Label htmlFor="contract-file">Signed contract</Label>
              <div className="flex items-center gap-2">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => document.getElementById("contract-file")?.click()}
                  className="gap-1.5"
                  disabled={reading}
                >
                  {reading ? (
                    <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  ) : (
                    <Upload className="h-3.5 w-3.5" />
                  )}
                  Choose file
                </Button>
                <span className="truncate text-xs text-muted-foreground">
                  {reading
                    ? "Reading the contract…"
                    : contractFile
                      ? contractFile.name
                      : "PDF, JPG or PNG"}
                </span>
              </div>
              <input
                id="contract-file"
                type="file"
                accept=".pdf,.jpg,.jpeg,.png"
                className="hidden"
                onChange={(e) => choose(e.target.files?.[0] ?? null)}
              />

              {/* The contract is signed days after the employer and the visa are known, and
                  until then this dialog asked for details nobody could read off anything. The
                  portal has them in the meantime. */}
              <div className="mt-2 flex flex-wrap items-center gap-2 rounded-md border border-dashed p-2">
                <p className="min-w-0 flex-1 text-xs text-muted-foreground">
                  No signed contract yet? Copy the contract parties page on Musaned instead.
                </p>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="h-7 shrink-0 gap-1.5 text-xs"
                  onClick={() => setPasting(true)}
                  disabled={reading || busy}
                >
                  <ClipboardPaste className="h-3.5 w-3.5" />
                  Fill from Musaned
                </Button>
              </div>
            </div>
          ) : (
            <p className="flex items-center gap-2 text-sm">
              <FileText className="h-3.5 w-3.5 shrink-0" />
              The contract is generated here.
            </p>
          )}

          <div className="grid gap-3 sm:grid-cols-2">
            <Field id="contract-no" label="Contract no" value={form.contractNo} onChange={set("contractNo")} />
            <Field id="visa-no" label="Visa number" value={form.visaNumber} onChange={set("visaNumber")} />
          </div>

          <Field
            id="sponsor-name"
            label="Sponsor name"
            value={form.sponsorName}
            onChange={set("sponsorName")}
          />

          <div className="grid gap-3 sm:grid-cols-2">
            <Field
              id="sponsor-id"
              label={sponsorIdLabel}
              value={form.sponsorIdNumber}
              onChange={set("sponsorIdNumber")}
            />
            <div className="space-y-1.5">
              <Label htmlFor="sponsor-phone">Sponsor phone</Label>
              {/* The sponsor is in the destination country, so the box starts there rather than
                  on Ethiopia — a Saudi number typed after a +251 prefix cannot be called back. */}
              <PhoneInputField
                country="sa"
                value={form.sponsorPhone}
                onChange={set("sponsorPhone")}
              />
            </div>
          </div>

          <Field
            id="sponsor-address"
            label="Sponsor address"
            value={form.sponsorAddress}
            onChange={set("sponsorAddress")}
          />
        </div>

        <DialogFooter>
          <Button type="button" variant="outline" onClick={() => close(false)} disabled={busy}>
            Cancel
          </Button>
          <Button
            type="button"
            onClick={() => void submit()}
            disabled={busy}
            className="gap-1.5 bg-green-800 text-white hover:bg-green-900"
          >
            {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
            {busy ? step ?? "Working…" : "Mark Ready"}
          </Button>
        </DialogFooter>
      </DialogContent>

      {/* Outside the content, so closing it comes back to this form rather than taking it
          with it. A page that is not the employer block fills nothing, which it says. */}
      <PasteFromPortal
        open={pasting}
        onOpenChange={setPasting}
        onApply={(values) => {
          fillFromPortal(values);
          setPasting(false);
        }}
      />
    </Dialog>
  );
}

function Field({
  id,
  label,
  value,
  onChange,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input id={id} value={value} onChange={(e) => onChange(e.target.value)} />
    </div>
  );
}
