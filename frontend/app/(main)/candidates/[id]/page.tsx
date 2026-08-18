"use client";

import * as React from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import {
  useCandidate,
  useCandidateDocuments,
  useCandidateTimeline,
  generateCandidateCv,
  generateCandidateVisaForm,
} from "@/lib/api/candidates";
import { useAvailableActions, useWorkflowState } from "@/lib/api/workflow";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Separator } from "@/components/ui/separator";
import { CandidateStatusBadge } from "@/components/workflow/candidate-status-badge";
import { ActionButtonBar } from "@/components/workflow/action-button-bar";
import { DocumentUploader } from "@/components/candidates/document-uploader";
import { DocumentList } from "@/components/candidates/document-list";
import { CandidateTimeline } from "@/components/candidates/candidate-timeline";
import {
  ArrowLeft,
  FileDown,
  FileText,
  Loader2,
  MoreHorizontal,
  Pencil,
  Stamp,
} from "lucide-react";
import { toast } from "sonner";
import { useState } from "react";

const GENDER_LABELS = ["Male", "Female", "Other"];

export default function CandidateDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { hasPermission } = usePermissions();
  const [generatingCv, setGeneratingCv] = useState(false);
  const [generatingVisa, setGeneratingVisa] = useState(false);
  const [showEmpty, setShowEmpty] = useState(false);

  const { candidate, isLoading, mutate: mutateCandidate } = useCandidate(id);
  const { documents, mutate: mutateDocs } = useCandidateDocuments(id);
  const { events, mutate: mutateTimeline } = useCandidateTimeline(id);
  const { actions, mutate: mutateActions } = useAvailableActions(id);
  const { state, mutate: mutateState } = useWorkflowState(id);

  if (!hasPermission("candidate.read")) {
    return (
      <div className="p-8 text-sm text-muted-foreground">
        You do not have permission to view candidates.
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-16 text-muted-foreground">
        <Loader2 className="h-5 w-5 animate-spin mr-2" />
        Loading candidate…
      </div>
    );
  }

  if (!candidate) {
    return (
      <div className="p-8 space-y-4">
        <p className="text-sm">Candidate not found.</p>
        <Button asChild variant="outline" size="sm">
          <Link href="/candidates">Back to list</Link>
        </Button>
      </div>
    );
  }

  const fullName =
    candidate.fullName ||
    [candidate.firstName, candidate.middleName, candidate.lastName]
      .filter(Boolean)
      .join(" ");

  const initials = [candidate.firstName, candidate.lastName]
    .filter(Boolean)
    .map((s) => s![0]?.toUpperCase())
    .join("")
    .slice(0, 2);

  const statusValues =
    state?.statusValues ?? candidate.currentStatusValues ?? {};

  const refreshAll = () => {
    mutateCandidate();
    mutateTimeline();
    mutateActions();
    mutateState();
  };

  const openPdfInNewTab = (blob: Blob) => {
    const pdfBlob = blob.type === "application/pdf"
      ? blob
      : new Blob([blob], { type: "application/pdf" });
    const url = URL.createObjectURL(pdfBlob);

    // Open via temporary anchor — more reliable than window.open(blob) for Chrome PDF viewer
    const a = document.createElement("a");
    a.href = url;
    a.target = "_blank";
    a.rel = "noopener noreferrer";
    document.body.appendChild(a);
    a.click();
    a.remove();

    setTimeout(() => URL.revokeObjectURL(url), 120_000);
  };

  const handleGenerateCv = async () => {
    setGeneratingCv(true);
    try {
      const blob = await generateCandidateCv(candidate.id);
      openPdfInNewTab(blob);
      mutateDocs();
      toast.success("CV generated");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "CV generation failed");
    } finally {
      setGeneratingCv(false);
    }
  };

  const handleGenerateVisa = async () => {
    setGeneratingVisa(true);
    try {
      const blob = await generateCandidateVisaForm(candidate.id);
      openPdfInNewTab(blob);
      mutateDocs();
      toast.success("Visa / Enjaz form generated");
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Visa form failed");
    } finally {
      setGeneratingVisa(false);
    }
  };

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
      <div>
        <Button
          asChild
          variant="ghost"
          size="sm"
          className="mb-4 h-8 -ml-2 px-2 text-muted-foreground hover:text-foreground"
        >
          <Link href="/candidates">
            <ArrowLeft className="h-4 w-4 mr-1" />
            Candidates
          </Link>
        </Button>

        <div className="flex flex-col gap-5 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex gap-4">
            <Avatar className="h-16 w-16 border shadow-sm">
              <AvatarFallback className="bg-slate-100 text-lg font-semibold text-slate-700">
                {initials || "?"}
              </AvatarFallback>
            </Avatar>
            <div className="space-y-2 min-w-0">
              {/* Names can be very long — clamp to two lines, full value on hover. */}
              <h1
                title={fullName}
                className="line-clamp-2 break-words text-xl font-semibold tracking-tight text-foreground md:text-2xl"
              >
                {fullName}
              </h1>
              <p className="text-sm text-muted-foreground">
                Passport{" "}
                <span className="font-medium text-foreground/80">
                  {candidate.passportNumber}
                </span>
                {candidate.labourId ? (
                  <>
                    {" · "}Labour ID{" "}
                    <span className="font-medium text-foreground/80">
                      {candidate.labourId}
                    </span>
                  </>
                ) : null}
              </p>
              <CandidateStatusBadge
                stageName={state?.stageName ?? candidate.currentStageName}
                statusValues={statusValues}
              />
            </div>
          </div>

          {/* One primary action + a ⋯ menu for the rest — same pattern as table rows. */}
          <div className="flex shrink-0 items-center gap-2 sm:justify-end">
            {hasPermission("candidate.write") || hasPermission("system.admin") ? (
              <Button asChild size="sm">
                <Link href={`/candidates/${id}/edit`}>
                  <Pencil className="mr-1.5 h-4 w-4" />
                  Edit
                </Link>
              </Button>
            ) : null}
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button
                  variant="outline"
                  size="sm"
                  className="h-8 w-8 p-0"
                  aria-label="More actions"
                >
                  <MoreHorizontal className="h-4 w-4" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-52">
                <DropdownMenuItem
                  disabled={generatingCv}
                  onSelect={(e) => {
                    e.preventDefault();
                    void handleGenerateCv();
                  }}
                >
                  {generatingCv ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <FileDown className="mr-2 h-4 w-4" />
                  )}
                  Generate CV
                </DropdownMenuItem>
                <DropdownMenuItem
                  disabled={generatingVisa}
                  onSelect={(e) => {
                    e.preventDefault();
                    void handleGenerateVisa();
                  }}
                >
                  {generatingVisa ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <Stamp className="mr-2 h-4 w-4" />
                  )}
                  Generate visa form
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>
        </div>
      </div>

      <Tabs defaultValue="profile" className="w-full">
        <TabsList className="h-10 w-full justify-start gap-1 bg-muted/60 p-1 sm:w-auto">
          <TabsTrigger value="profile" className="px-4">
            Profile
          </TabsTrigger>
          <TabsTrigger value="documents" className="px-4">
            Documents
            {documents.length > 0 && (
              <span className="ml-1.5 rounded-full bg-background px-1.5 text-[10px] font-medium text-muted-foreground">
                {documents.length}
              </span>
            )}
          </TabsTrigger>
          <TabsTrigger value="timeline" className="px-4">
            Timeline
          </TabsTrigger>
          <TabsTrigger value="actions" className="px-4">
            Actions
          </TabsTrigger>
        </TabsList>

        <TabsContent value="profile" className="mt-4 space-y-4">
          <div className="flex justify-end">
            <Button
              variant="ghost"
              size="sm"
              className="h-8 text-muted-foreground"
              onClick={() => setShowEmpty((v) => !v)}
            >
              {showEmpty ? "Hide empty fields" : "Show all fields"}
            </Button>
          </div>
        <ShowEmptyFields.Provider value={showEmpty}>
          <ProfileSection title="Identity">
            <Field
              label="Gender"
              value={GENDER_LABELS[candidate.gender] ?? String(candidate.gender)}
            />
            <Field label="Local name" value={candidate.localFullName} />
            <Field label="Labour ID" value={candidate.labourId} />
            <Field label="Biometric ID" value={candidate.biometricId} />
            <Field label="National ID" value={candidate.nationalId} />
          </ProfileSection>

          <ProfileSection title="Passport">
            <Field label="Passport type" value={candidate.passportType} />
            <Field label="Place of issue" value={candidate.passportPlaceOfIssue} />
            <Field label="Issue date" value={candidate.passportIssueDate} />
            <Field label="Expiry date" value={candidate.passportExpiryDate} />
          </ProfileSection>

          <ProfileSection title="Contact">
            <Field label="Phone" value={candidate.phoneNumber} />
            <Field label="Email" value={candidate.email} />
            <Field label="Address" value={candidate.address} />
            <Field label="Region" value={candidate.region} />
            <Field label="City" value={candidate.city} />
            <Field label="Subcity" value={candidate.subcity} />
            <Field label="Woreda" value={candidate.woreda} />
            <Field label="House No." value={candidate.houseNo} />
            <Field label="Country" value={candidate.country} />
          </ProfileSection>

          <ProfileSection title="Details of applicant">
            <Field label="Nationality" value={candidate.nationality} />
            <Field label="Religion" value={candidate.religion} />
            <Field label="Date of birth" value={candidate.dateOfBirth} />
            <Field label="Place of birth" value={candidate.placeOfBirth} />
            <Field label="Marital status" value={candidate.maritalStatus} />
            <Field
              label="No. of children"
              value={
                candidate.numberOfChildren != null
                  ? String(candidate.numberOfChildren)
                  : undefined
              }
            />
            <Field label="Height" value={candidate.height} />
            <Field label="Weight" value={candidate.weight} />
          </ProfileSection>

          <ProfileSection title="Languages & education">
            <Field label="English" value={candidate.englishLevel} />
            <Field label="Arabic" value={candidate.arabicLevel} />
            <Field label="Education" value={candidate.qualification} />
          </ProfileSection>

          <ProfileSection title="Work experience">
            <Field
              label="Period"
              value={
                candidate.experienceAbroadYears != null
                  ? `${candidate.experienceAbroadYears} Year(s)`
                  : undefined
              }
            />
            <Field label="Country" value={candidate.worksIn} />
            <Field label="Occupation" value={candidate.occupation} />
            <Field label="Salary" value={candidate.monthlySalary} />
            <Field label="Contract period" value={candidate.contractPeriod} />
          </ProfileSection>

          <ProfileSection title="Skills & experience">
            <Field label="Cooking level" value={candidate.cookingLevel} />
            <Field
              label="Skills"
              value={[
                candidate.skillCleaning && "Cleaning",
                candidate.skillWashing && "Washing",
                candidate.skillCooking && "Cooking",
                candidate.skillBabysitting && "Baby Sitting",
                candidate.skillChildCare && "Child care",
                candidate.skillIroning && "Ironing",
                candidate.skillSewing && "Sewing",
              ]
                .filter(Boolean)
                .join(", ") || undefined}
            />
            <Field label="Reference No." value={candidate.referenceNo} />
            <Field label="Remark" value={candidate.remark} />
          </ProfileSection>

          <ProfileSection title="Travel & contract">
            <Field label="Country of travel" value={candidate.countryOfTravel} />
            <Field label="Partner agency" value={candidate.partnerName} />
            <Field label="Contract date" value={candidate.contractDate} />
            <Field
              label="Registered"
              value={
                candidate.registeredAt
                  ? new Date(candidate.registeredAt).toLocaleString()
                  : undefined
              }
            />
          </ProfileSection>

          <ProfileSection title="Sponsor & visa">
            <Field label="Visa number" value={candidate.visaNumber} />
            <Field label="Visa type" value={candidate.visaType} />
            <Field label="Sponsor name" value={candidate.sponsorName} />
            <Field label="Sponsor ID" value={candidate.sponsorIdNumber} />
            <Field label="Sponsor phone" value={candidate.sponsorPhone} />
            <Field label="Sponsor address" value={candidate.sponsorAddress} />
            <Field label="Sponsor (Arabic)" value={candidate.sponsorArabicName} />
            <Field label="Agent" value={candidate.agentName} />
            <Field label="File No." value={candidate.fileNo} />
            <Field label="Wakala #" value={candidate.wakalaNo} />
            <Field label="Contract #" value={candidate.contractNo} />
            <Field label="Sticker visa #" value={candidate.stickerVisaNo} />
          </ProfileSection>

          <ProfileSection title="Relative">
            <Field label="Name" value={candidate.relativeName} />
            <Field label="Phone" value={candidate.relativePhone} />
            <Field label="Kinship" value={candidate.relativeKinship} />
            <Field label="City" value={candidate.relativeCity} />
          </ProfileSection>

          <ProfileSection title="COC / other">
            <Field label="Contact (2nd)" value={candidate.contactPerson2} />
            <Field label="Phone (2nd)" value={candidate.contactPhone2} />
            <Field label="COC center" value={candidate.cocCenterName} />
            <Field label="Certificate No." value={candidate.certificateNo} />
            <Field label="Medical place" value={candidate.medicalPlace} />
            <Field label="Remark" value={candidate.remark} />
          </ProfileSection>
        </ShowEmptyFields.Provider>
        </TabsContent>

        <TabsContent value="documents" className="mt-4">
          <div className="grid gap-4 lg:grid-cols-[minmax(0,320px)_1fr]">
            {hasPermission("candidate.update") && (
              <div className="rounded-xl border bg-card p-4 shadow-sm">
                <div className="mb-3 flex items-center gap-2">
                  <FileText className="h-4 w-4 text-muted-foreground" />
                  <h2 className="text-sm font-semibold">Upload</h2>
                </div>
                <DocumentUploader
                  candidateId={candidate.id}
                  onUploaded={() => mutateDocs()}
                />
              </div>
            )}
            <div className="rounded-xl border bg-card p-4 shadow-sm min-h-[220px]">
              <h2 className="mb-3 text-sm font-semibold">Files</h2>
              <DocumentList documents={documents} />
            </div>
          </div>
        </TabsContent>

        <TabsContent value="timeline" className="mt-4">
          <div className="rounded-xl border bg-card p-5 shadow-sm">
            <CandidateTimeline events={events} />
          </div>
        </TabsContent>

        <TabsContent value="actions" className="mt-4">
          <div className="rounded-xl border bg-card p-5 shadow-sm space-y-4">
            <div>
              <h2 className="text-sm font-semibold">Workflow transitions</h2>
              <p className="mt-1 text-sm text-muted-foreground">
                Move this candidate to the next stage when requirements are met.
              </p>
            </div>
            <Separator />
            {hasPermission("workflow.execute") || hasPermission("workflow.view") ? (
              <ActionButtonBar
                candidateId={candidate.id}
                actions={actions}
                onExecuted={refreshAll}
                className="gap-2"
              />
            ) : (
              <p className="text-sm text-muted-foreground">
                No permission to view actions.
              </p>
            )}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  );
}

/**
 * When false (the default) fields with no value are hidden, and a section whose
 * fields are all empty disappears entirely. A candidate record has ~50 optional
 * fields, so showing every blank one buries the real information in dashes.
 */
const ShowEmptyFields = React.createContext(false);

function ProfileSection({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  const showEmpty = React.useContext(ShowEmptyFields);

  const all = React.Children.toArray(children).filter(React.isValidElement) as
    React.ReactElement<{ value?: string | null }>[];
  const filled = all.filter((c) => !!c.props.value?.toString().trim());
  const visible = showEmpty ? all : filled;

  if (visible.length === 0) return null;

  return (
    <section className="rounded-xl border bg-card p-5 shadow-sm">
      <div className="mb-4 flex items-baseline justify-between gap-3">
        <h2 className="text-sm font-semibold text-foreground">{title}</h2>
        {!showEmpty && filled.length < all.length ? (
          <span className="text-xs text-muted-foreground">
            {all.length - filled.length} not filled
          </span>
        ) : null}
      </div>
      <div className="grid gap-x-8 gap-y-4 sm:grid-cols-2">{visible}</div>
    </section>
  );
}

function Field({ label, value }: { label: string; value?: string | null }) {
  const text = value?.toString().trim();
  return (
    <div className="min-w-0">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p
        title={text || undefined}
        className="mt-0.5 truncate text-sm font-medium text-foreground"
      >
        {text || "—"}
      </p>
    </div>
  );
}
