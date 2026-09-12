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
import { FileText, Loader2, Upload } from "lucide-react";
import {
  generateCandidateContract,
  generateCandidateVisaForm,
  setVisaDetails,
  uploadCandidateDocument,
} from "@/lib/api/candidates";
import { updateWorkflowStatus } from "@/lib/api/workflow";

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

/**
 * Marking a candidate Ready is the point the visa file starts, so it collects what the embassy
 * desk needs rather than flipping a status and leaving them to find the gaps later:
 *
 *  - Saudi Arabia: the signed contract is uploaded, because one already exists between the parties.
 *  - Anywhere else: the contract is generated here from the candidate, partner and agency details.
 *
 * Either way the visa number and sponsor are captured, the visa track opens at Ready, and the
 * enjaze form is produced — the same document the bot sends out.
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
  const [visaNumber, setVisaNumber] = useState("");
  const [sponsorName, setSponsorName] = useState("");
  const [sponsorIdNumber, setSponsorIdNumber] = useState("");
  const [contractFile, setContractFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [step, setStep] = useState<string | null>(null);

  const reset = () => {
    setVisaNumber("");
    setSponsorName("");
    setSponsorIdNumber("");
    setContractFile(null);
    setStep(null);
  };

  const close = (next: boolean) => {
    if (!next) reset();
    onOpenChange(next);
  };

  const submit = async () => {
    if (saudi && !contractFile) {
      toast.error("Attach the signed contract before marking Ready");
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
      await setVisaDetails(candidateId, { visaNumber, sponsorName, sponsorIdNumber });

      setStep("Marking Ready…");
      await updateWorkflowStatus(candidateId, "status", "Ready");
      // The visa file is open from this moment; leaving the track blank made the embassy desk
      // set it by hand before they could do anything.
      await updateWorkflowStatus(candidateId, "visa", "Ready");

      setStep("Producing the enjaze form…");
      await generateCandidateVisaForm(candidateId);

      toast.success(`${candidateName} is Ready — contract and enjaze are on file`);
      close(false);
      onDone();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Could not mark Ready");
    } finally {
      setBusy(false);
      setStep(null);
    }
  };

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Mark Ready</DialogTitle>
          <DialogDescription>
            {saudi
              ? "Saudi placement — attach the signed contract and the visa details."
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
                >
                  <Upload className="h-3.5 w-3.5" />
                  Choose file
                </Button>
                <span className="truncate text-xs text-muted-foreground">
                  {contractFile ? contractFile.name : "PDF, JPG or PNG"}
                </span>
              </div>
              <input
                id="contract-file"
                type="file"
                accept=".pdf,.jpg,.jpeg,.png"
                className="hidden"
                onChange={(e) => setContractFile(e.target.files?.[0] ?? null)}
              />
            </div>
          ) : (
            <p className="flex items-start gap-2 rounded-lg border bg-muted/40 p-3 text-xs text-muted-foreground">
              <FileText className="mt-0.5 h-3.5 w-3.5 shrink-0" />
              The employment contract is built from this candidate, their partner agency and your
              agency's licence details, and filed against them.
            </p>
          )}

          <div className="space-y-1.5">
            <Label htmlFor="visa-no">Visa number</Label>
            <Input id="visa-no" value={visaNumber} onChange={(e) => setVisaNumber(e.target.value)} />
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label htmlFor="sponsor-name">Sponsor name</Label>
              <Input
                id="sponsor-name"
                value={sponsorName}
                onChange={(e) => setSponsorName(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="sponsor-id">Sponsor ID</Label>
              <Input
                id="sponsor-id"
                value={sponsorIdNumber}
                onChange={(e) => setSponsorIdNumber(e.target.value)}
              />
            </div>
          </div>
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
    </Dialog>
  );
}
