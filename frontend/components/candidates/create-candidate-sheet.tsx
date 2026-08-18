"use client";

import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import useSWR, { useSWRConfig } from "swr";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import {
  User,
  Mail,
  MapPin,
  FileText,
  Camera,
  Stamp,
  Users,
  Upload,
  X,
  ImageIcon,
  Loader2,
  Plus,
  ScanLine,
  BookOpen,
  ArrowLeft,
  ArrowRight,
  type LucideIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { toast } from "sonner";
import { CountrySelect } from "@/components/ui/country-select";
import { PhoneInputField } from "@/components/ui/phone-input";
import { Stepper, StepperStep } from "@/components/ui/stepper";
import { Progress } from "@/components/ui/progress";
import type { DepartmentListItem } from "@/lib/schemas/department";
import {
  generateCandidateVisaForm,
  uploadCandidateDocument,
} from "@/lib/api/candidates";
import { useRouter } from "next/navigation";
import Link from "next/link";

const APPLICATION_STEPS = [
  {
    title: "Documents",
    description: "Passport & photos",
  },
  {
    title: "Identity",
    description: "Name & passport",
  },
  {
    title: "Family",
    description: "Optional · contacts & COC",
  },
  {
    title: "Experience",
    description: "Optional · languages & skills",
  },
  {
    title: "Placement",
    description: "Sponsor & travel",
  },
] as const;

function FormSection({
  icon: Icon,
  title,
  description,
  children,
  className,
}: {
  icon: LucideIcon;
  title: string;
  description?: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section
      className={cn(
        "rounded-xl border border-slate-200/90 bg-white shadow-sm",
        className
      )}
    >
      <div className="flex items-start gap-3 border-b border-slate-100 px-4 py-3.5">
        <span className="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-emerald-50 text-emerald-800">
          <Icon className="h-3.5 w-3.5" />
        </span>
        <div className="min-w-0 flex-1">
          <h3 className="text-sm font-semibold tracking-tight text-slate-900">{title}</h3>
          {description ? (
            <p className="mt-0.5 text-xs leading-relaxed text-muted-foreground">{description}</p>
          ) : null}
        </div>
      </div>
      <div className="space-y-4 px-4 py-4">{children}</div>
    </section>
  );
}
const opt = z.string().optional().or(z.literal(""));

const optionalNonNegInt = z
  .string()
  .optional()
  .or(z.literal(""))
  .refine((v) => !v || (/^\d+$/.test(v) && Number(v) >= 0), {
    message: "Must be a whole number (0 or more)",
  });

const optionalPositiveNumber = z
  .string()
  .optional()
  .or(z.literal(""))
  .refine((v) => !v || (/^\d+(\.\d+)?$/.test(v) && Number(v) > 0), {
    message: "Must be a valid number greater than 0",
  });

const RELIGIONS = [
  "Orthodox",
  "Muslim",
  "Protestant",
  "Catholic",
  "Other",
] as const;

const MARITAL_STATUSES = ["Single", "Married", "Divorced", "Widowed"] as const;

const OCCUPATIONS = [
  "HOUSE MAID",
  "NANNY",
  "COOK",
  "DRIVER",
  "CLEANER",
  "CAREGIVER",
  "OTHER",
] as const;

const LANGUAGE_LEVELS = ["None", "Fair", "Good", "Excellent"] as const;

const LANGUAGE_OPTIONS = [
  "English",
  "Arabic",
  "Amharic",
  "Oromo",
  "Tigrinya",
  "Somali",
  "French",
  "Hindi",
  "Urdu",
  "Tagalog",
  "Swahili",
  "Other",
] as const;

type LanguageRow = { id: string; language: string; level: string };

function newLanguageRow(language = "", level = ""): LanguageRow {
  return {
    id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    language,
    level,
  };
}

function rowsFromCandidate(d: {
  englishLevel?: string | null;
  arabicLevel?: string | null;
  otherLanguages?: string | null;
}): LanguageRow[] {
  const rows: LanguageRow[] = [];
  if (d.englishLevel) rows.push(newLanguageRow("English", d.englishLevel));
  if (d.arabicLevel) rows.push(newLanguageRow("Arabic", d.arabicLevel));
  if (d.otherLanguages) {
    for (const part of d.otherLanguages.split(";")) {
      const trimmed = part.trim();
      if (!trimmed) continue;
      const idx = trimmed.indexOf(":");
      if (idx > 0) {
        rows.push(
          newLanguageRow(trimmed.slice(0, idx).trim(), trimmed.slice(idx + 1).trim())
        );
      }
    }
  }
  return rows.length > 0 ? rows : [newLanguageRow("English", ""), newLanguageRow("Arabic", "")];
}

function syncLanguagesToForm(
  rows: LanguageRow[],
  setValue: (name: "englishLevel" | "arabicLevel" | "otherLanguages", value: string) => void
) {
  const english = rows.find((r) => r.language === "English" && r.level);
  const arabic = rows.find((r) => r.language === "Arabic" && r.level);
  setValue("englishLevel", english?.level || "");
  setValue("arabicLevel", arabic?.level || "");
  const other = rows
    .filter((r) => r.language && r.level && r.language !== "English" && r.language !== "Arabic")
    .map((r) => `${r.language}: ${r.level}`)
    .join("; ");
  setValue("otherLanguages", other);
}

const PASSPORT_TYPES = ["Normal", "Diplomatic", "Service"] as const;

const VISA_TYPES = ["Work", "Visit", "Transit", "Other"] as const;

function parseMeasure(value?: string | null): string {
  if (!value) return "";
  const m = String(value).match(/(\d+(?:\.\d+)?)/);
  return m?.[1] ?? "";
}

const registerCandidateSchema = z.object({
  firstName: z.string().min(1, "First name is required").min(2, "First name must be at least 2 characters"),
  lastName: z.string().min(1, "Last name is required").min(2, "Last name must be at least 2 characters"),
  middleName: opt,
  localFullName: opt,
  passportNumber: z
    .string()
    .min(5, "Passport must be at least 5 characters")
    .max(20)
    .regex(/^[A-Za-z0-9]+$/, "Alphanumeric only"),
  dateOfBirth: z.string().min(1, "Date of birth is required"),
  gender: z.string().min(1, "Gender is required"),
  nationality: opt,
  phoneNumber: opt,
  email: z.string().email().optional().or(z.literal("")),
  address: opt,
  city: opt,
  country: opt,
  labourId: opt,
  countryOfTravel: opt,
  partnerName: opt,
  partnerAgencyId: opt,
  contractDate: opt,
  // intake
  placeOfBirth: opt,
  religion: opt,
  maritalStatus: opt,
  numberOfChildren: optionalNonNegInt,
  height: optionalPositiveNumber,
  weight: optionalPositiveNumber,
  nationalId: opt,
  biometricId: opt,
  passportType: opt,
  passportPlaceOfIssue: opt,
  passportIssueDate: opt,
  passportExpiryDate: opt,
  region: opt,
  subcity: opt,
  woreda: opt,
  houseNo: opt,
  occupation: opt,
  qualification: opt,
  monthlySalary: optionalPositiveNumber,
  contractPeriod: opt,
  englishLevel: opt,
  arabicLevel: opt,
  otherLanguages: opt,
  experienceAbroadYears: optionalNonNegInt,
  worksIn: opt,
  referenceNo: opt,
  remark: opt,
  cookingLevel: opt,
  skillCleaning: z.boolean().optional(),
  skillWashing: z.boolean().optional(),
  skillCooking: z.boolean().optional(),
  skillIroning: z.boolean().optional(),
  skillSewing: z.boolean().optional(),
  skillBabysitting: z.boolean().optional(),
  skillChildCare: z.boolean().optional(),
  visaNumber: opt,
  visaType: opt,
  sponsorName: opt,
  sponsorIdNumber: opt,
  sponsorPhone: opt,
  sponsorAddress: opt,
  sponsorArabicName: opt,
  agentName: opt,
  applicationNo: opt,
  fileNo: opt,
  wakalaNo: opt,
  contractNo: opt,
  stickerVisaNo: opt,
  signedOn: opt,
  relativeName: opt,
  relativePhone: opt,
  relativeKinship: opt,
  relativeGender: opt,
  relativeBirthDate: opt,
  relativeCity: opt,
  relativeRegion: opt,
  relativeSubcity: opt,
  relativeWoreda: opt,
  relativeHouseNo: opt,
  contactPerson2: opt,
  contactPhone2: opt,
  cocCenterName: opt,
  certificateNo: opt,
  certifiedDate: opt,
  medicalPlace: opt,
});

type RegisterCandidateForm = z.infer<typeof registerCandidateSchema>;

/** Fields validated before leaving each step (empty = no gate). */
const STEP_FIELDS: (keyof RegisterCandidateForm)[][] = [
  [],
  ["firstName", "lastName", "passportNumber", "dateOfBirth", "gender"],
  [],
  [],
  ["email"],
];

/** The minimum a candidate needs to exist — everything else can be added later. */
const ESSENTIAL_FIELDS = [
  "firstName",
  "lastName",
  "passportNumber",
  "dateOfBirth",
  "gender",
] as const;

interface CandidateApplicationFormProps {
  /** When set, form opens in edit mode with the same fields as create. */
  candidateId?: string | null;
  onSaved?: () => void;
}

const fetcher = (url: string) => fetch(url).then((r) => r.json());
const PHOTO_TYPE = 1;
const FULL_PHOTO_TYPE = 8;
const PASSPORT_TYPE = 0;

const defaults: Partial<RegisterCandidateForm> = {
  gender: "",
  partnerAgencyId: "",
  visaType: "Work",
  nationality: "Ethiopia",
  passportType: "Normal",
  passportPlaceOfIssue: "ETHIOPIA",
  occupation: "HOUSE MAID",
  qualification: "SECONDARY LEVEL",
  monthlySalary: "1000",
  contractPeriod: "2 Years",
  religion: "",
  maritalStatus: "Single",
  numberOfChildren: "0",
  height: "",
  weight: "",
  englishLevel: "",
  arabicLevel: "",
  otherLanguages: "",
  skillCleaning: true,
  skillWashing: true,
  skillCooking: false,
  skillIroning: false,
  skillSewing: false,
  skillBabysitting: false,
  skillChildCare: false,
};

function formatApiError(result: {
  error?: string;
  errors?: { propertyName?: string; errorMessage?: string }[];
}): string {
  if (result.errors?.length) {
    return result.errors
      .map((e) => e.errorMessage || e.propertyName)
      .filter(Boolean)
      .join("; ");
  }
  return result.error || "Registration failed";
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function PhotoPicker({
  label,
  hint,
  file,
  onChange,
  existingUrl,
  aspect = "portrait",
}: {
  label: string;
  hint?: string;
  file: File | null;
  onChange: (file: File | null) => void;
  existingUrl?: string | null;
  aspect?: "portrait" | "full" | "passport";
}) {
  const inputRef = useRef<HTMLInputElement>(null);
  const previewGen = useRef(0);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [broken, setBroken] = useState(false);
  const [dragOver, setDragOver] = useState(false);

  useEffect(() => {
    const gen = ++previewGen.current;
    let reader: FileReader | null = null;

    setBroken(false);

    if (file) {
      // Data URLs avoid createObjectURL revoke races under React Strict Mode.
      let cancelled = false;
      setLoading(true);
      setPreviewUrl(null);
      reader = new FileReader();
      reader.onload = () => {
        if (cancelled || previewGen.current !== gen) return;
        setPreviewUrl(typeof reader?.result === "string" ? reader.result : null);
        setLoading(false);
        setBroken(false);
      };
      reader.onerror = () => {
        if (cancelled || previewGen.current !== gen) return;
        setPreviewUrl(null);
        setBroken(true);
        setLoading(false);
      };
      reader.readAsDataURL(file);
      return () => {
        cancelled = true;
        try {
          reader?.abort();
        } catch {
          /* ignore */
        }
      };
    }

    if (!existingUrl) {
      setPreviewUrl(null);
      setLoading(false);
      return;
    }

    setLoading(true);
    setPreviewUrl(null);
    let cancelled = false;

    (async () => {
      try {
        const res = await fetch(existingUrl, { cache: "no-store" });
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const blob = await res.blob();
        if (cancelled || previewGen.current !== gen) return;
        const looksLikeImage =
          blob.type.startsWith("image/") ||
          blob.type === "application/octet-stream" ||
          blob.size > 100;
        if (!looksLikeImage) throw new Error("Not an image");
        // Prefer data: URLs — CSP img-src allows data: but not blob: by default.
        const dataUrl = await new Promise<string>((resolve, reject) => {
          const fr = new FileReader();
          fr.onload = () =>
            typeof fr.result === "string" ? resolve(fr.result) : reject(new Error("read failed"));
          fr.onerror = () => reject(fr.error ?? new Error("read failed"));
          fr.readAsDataURL(blob);
        });
        if (cancelled || previewGen.current !== gen) return;
        setPreviewUrl(dataUrl);
        setBroken(false);
      } catch {
        if (!cancelled && previewGen.current === gen) {
          setPreviewUrl(null);
          setBroken(true);
        }
      } finally {
        if (!cancelled && previewGen.current === gen) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [file, existingUrl]);

  const acceptFile = (next: File | null) => {
    if (!next) {
      onChange(null);
      return;
    }
    const okType =
      next.type.startsWith("image/") ||
      /\.(jpe?g|png|webp|gif)$/i.test(next.name);
    if (!okType) {
      toast.error("Please choose a JPEG, PNG, or WebP image");
      return;
    }
    if (next.size > 8 * 1024 * 1024) {
      toast.error("Image must be under 8 MB");
      return;
    }
    onChange(next);
  };

  const frameClass =
    aspect === "full"
      ? "min-h-[200px] h-52 lg:h-56"
      : aspect === "passport"
        ? "min-h-[220px] h-56 lg:min-h-[280px] lg:h-72"
        : "min-h-[200px] h-52 lg:h-56";

  const showImage = !!previewUrl && !loading && !broken;

  return (
    <div className="space-y-3">
      <div
        className={cn(
          "relative flex items-center justify-center overflow-hidden rounded-lg border bg-slate-50 transition-colors",
          frameClass,
          dragOver ? "border-emerald-500 bg-emerald-50/60" : "border-slate-200",
          broken ? "border-destructive/40" : null
        )}
        onDragOver={(e) => {
          e.preventDefault();
          setDragOver(true);
        }}
        onDragLeave={() => setDragOver(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDragOver(false);
          acceptFile(e.dataTransfer.files?.[0] ?? null);
        }}
      >
        {loading ? (
          <div className="flex flex-col items-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="h-6 w-6 animate-spin text-emerald-700" />
            <span>Loading preview…</span>
          </div>
        ) : showImage ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            key={previewUrl.slice(0, 64)}
            src={previewUrl}
            alt={label}
            className="h-full w-full object-contain bg-white"
            onError={() => {
              setBroken(true);
              setPreviewUrl(null);
            }}
          />
        ) : (
          <div className="flex flex-col items-center gap-2 px-4 text-center text-sm text-muted-foreground">
            <ImageIcon className="h-8 w-8 text-slate-400" />
            <p className="font-medium text-slate-600">{label}</p>
            <p className="text-xs text-slate-500">
              {hint || "JPEG, PNG, or WebP · max 8 MB"}
            </p>
            {broken ? (
              <p className="text-xs text-destructive">Could not load preview</p>
            ) : null}
          </div>
        )}

        {(file || previewUrl) && (
          <button
            type="button"
            className="absolute right-2 top-2 rounded-full border border-slate-200 bg-white/95 p-1.5 text-slate-600 shadow-sm hover:bg-white hover:text-slate-900"
            aria-label={`Remove ${label}`}
            onClick={() => {
              onChange(null);
              if (inputRef.current) inputRef.current.value = "";
            }}
          >
            <X className="h-3.5 w-3.5" />
          </button>
        )}
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <input
          ref={inputRef}
          type="file"
          accept="image/jpeg,image/png,image/webp,image/gif,.jpg,.jpeg,.png,.webp"
          className="sr-only"
          onChange={(e) => {
            const selected = e.target.files?.[0] ?? null;
            acceptFile(selected);
            e.target.value = "";
          }}
        />
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="gap-1.5"
          onClick={() => inputRef.current?.click()}
        >
          <Upload className="h-3.5 w-3.5" />
          {file || previewUrl ? "Replace" : "Upload"}
        </Button>
        {file ? (
          <p className="min-w-0 flex-1 truncate text-xs text-muted-foreground">
            {file.name} · {formatFileSize(file.size)}
          </p>
        ) : existingUrl && previewUrl ? (
          <p className="text-xs text-muted-foreground">Saved image on file</p>
        ) : (
          <p className="text-xs text-muted-foreground">Drag & drop or browse</p>
        )}
      </div>
    </div>
  );
}

function SkillCheck({
  id,
  label,
  checked,
  onChange,
}: {
  id: string;
  label: string;
  checked: boolean;
  onChange: (v: boolean) => void;
}) {
  return (
    <label htmlFor={id} className="flex items-center gap-2 text-sm">
      <Checkbox id={id} checked={checked} onCheckedChange={(v) => onChange(v === true)} />
      {label}
    </label>
  );
}

export function CandidateApplicationForm({
  candidateId = null,
  onSaved,
}: CandidateApplicationFormProps) {
  const isEdit = !!candidateId;
  const router = useRouter();
  const { mutate } = useSWRConfig();
  const { data: linkedPartnersResponse } = useSWR(
    "/api/proxy/partners?linkedOnly=true&usableOnly=true",
    fetcher,
    { revalidateOnFocus: false }
  );
  const { data: candidateResponse } = useSWR(
    isEdit ? `/api/proxy/candidates/${candidateId}` : null,
    fetcher,
    { revalidateOnFocus: false }
  );

  const linkedPartners: { id: string; name: string; country: string }[] = useMemo(
    () => linkedPartnersResponse?.data || [],
    [linkedPartnersResponse?.data]
  );

  const [photoFile, setPhotoFile] = useState<File | null>(null);
  const [fullPhotoFile, setFullPhotoFile] = useState<File | null>(null);
  const [passportFile, setPassportFile] = useState<File | null>(null);
  const [ocrBusy, setOcrBusy] = useState(false);
  const [ocrStatus, setOcrStatus] = useState("");
  const [ocrPercent, setOcrPercent] = useState(0);
  const [languageRows, setLanguageRows] = useState<LanguageRow[]>(() => [
    newLanguageRow("English", ""),
    newLanguageRow("Arabic", ""),
  ]);
  const [generateVisaAfterSave, setGenerateVisaAfterSave] = useState(!isEdit);
  const [existingMedia, setExistingMedia] = useState<{
    photo: boolean;
    fullPhoto: boolean;
    passport: boolean;
  }>({ photo: false, fullPhoto: false, passport: false });
  const [currentStep, setCurrentStep] = useState(0);
  const lastStep = APPLICATION_STEPS.length - 1;

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    reset,
    trigger,
    formState: { errors, isSubmitting },
  } = useForm<RegisterCandidateForm>({
    resolver: zodResolver(registerCandidateSchema),
    defaultValues: defaults,
  });

  const applyPassportOcr = async (file: File) => {
    setOcrBusy(true);
    setOcrStatus("Starting scan…");
    setOcrPercent(0);
    try {
      const { scanPassportImage } = await import("@/lib/ocr/passport-ocr");
      const result = await scanPassportImage(file, ({ message, percent }) => {
        setOcrStatus(message);
        setOcrPercent(percent);
      });
      setValue("firstName", result.firstName, { shouldValidate: true });
      setValue("middleName", result.middleName || "");
      setValue("lastName", result.lastName, { shouldValidate: true });
      setValue("passportNumber", result.passportNumber, { shouldValidate: true });
      if (result.dateOfBirth) setValue("dateOfBirth", result.dateOfBirth, { shouldValidate: true });
      if (result.gender) setValue("gender", result.gender, { shouldValidate: true });
      if (result.nationality) setValue("nationality", result.nationality);
      if (result.passportExpiryDate) setValue("passportExpiryDate", result.passportExpiryDate);
      if (result.passportPlaceOfIssue) setValue("passportPlaceOfIssue", result.passportPlaceOfIssue);
      if (result.placeOfBirth) setValue("placeOfBirth", result.placeOfBirth);
      if (result.passportIssueDate) setValue("passportIssueDate", result.passportIssueDate);
      if (result.passportType) setValue("passportType", result.passportType);
      setOcrPercent(100);
      setOcrStatus("Passport data verified");
      toast.success(
        result.confidence === "high"
          ? "Passport scanned — basic details filled in"
          : "Passport scanned — please verify the filled fields"
      );
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Passport scan failed");
    } finally {
      setOcrBusy(false);
      setOcrStatus("");
      setOcrPercent(0);
    }
  };

  const onPassportFileChange = (file: File | null) => {
    setPassportFile(file);
    if (file && !isEdit) {
      void applyPassportOcr(file);
    }
  };

  // Reset create form on mount
  useEffect(() => {
    if (isEdit) return;
    reset({
      ...defaults,
    });
    setPhotoFile(null);
    setFullPhotoFile(null);
    setPassportFile(null);
    setLanguageRows([newLanguageRow("English", ""), newLanguageRow("Arabic", "")]);
    setGenerateVisaAfterSave(true);
    setExistingMedia({ photo: false, fullPhoto: false, passport: false });
    setCurrentStep(0);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isEdit, reset]);

  // Prefill edit form when candidate detail loads.
  useEffect(() => {
    if (!isEdit) return;
    const d = candidateResponse?.data;
    if (!d) return;

    reset({
      ...defaults,
      firstName: d.firstName || "",
      lastName: d.lastName || "",
      middleName: d.middleName || "",
      localFullName: d.localFullName || "",
      passportNumber: d.passportNumber || "",
      dateOfBirth: d.dateOfBirth || "",
      gender: d.gender != null ? String(d.gender) : "",
      nationality: d.nationality || "Ethiopia",
      phoneNumber: d.phoneNumber || "",
      email: d.email || "",
      address: d.address || "",
      city: d.city || "",
      country: d.country || "",
      labourId: d.labourId || "",
      countryOfTravel: d.countryOfTravel || "",
      partnerName: d.partnerName || "",
      partnerAgencyId: d.partnerAgencyId || "",
      contractDate: d.contractDate || "",
      placeOfBirth: d.placeOfBirth || "",
      religion: d.religion || "",
      maritalStatus: d.maritalStatus || "",
      numberOfChildren: d.numberOfChildren != null ? String(d.numberOfChildren) : "0",
      height: parseMeasure(d.height),
      weight: parseMeasure(d.weight),
      nationalId: d.nationalId || "",
      biometricId: d.biometricId || "",
      passportType: d.passportType || "Normal",
      passportPlaceOfIssue: d.passportPlaceOfIssue || "ETHIOPIA",
      passportIssueDate: d.passportIssueDate || "",
      passportExpiryDate: d.passportExpiryDate || "",
      region: d.region || "",
      subcity: d.subcity || "",
      woreda: d.woreda || "",
      houseNo: d.houseNo || "",
      occupation: d.occupation || "HOUSE MAID",
      qualification: d.qualification || "SECONDARY LEVEL",
      monthlySalary: d.monthlySalary || "1000",
      contractPeriod: d.contractPeriod || "2 Years",
      englishLevel: d.englishLevel || "",
      arabicLevel: d.arabicLevel || "",
      otherLanguages: d.otherLanguages || "",
      experienceAbroadYears:
        d.experienceAbroadYears != null ? String(d.experienceAbroadYears) : "",
      worksIn: d.worksIn || "",
      referenceNo: d.referenceNo || "",
      remark: d.remark || "",
      cookingLevel: d.cookingLevel || "",
      skillCleaning: !!d.skillCleaning,
      skillWashing: !!d.skillWashing,
      skillCooking: !!d.skillCooking,
      skillIroning: !!d.skillIroning,
      skillSewing: !!d.skillSewing,
      skillBabysitting: !!d.skillBabysitting,
      skillChildCare: !!d.skillChildCare,
      visaNumber: d.visaNumber || "",
      visaType: d.visaType || "Work",
      sponsorName: d.sponsorName || "",
      sponsorIdNumber: d.sponsorIdNumber || "",
      sponsorPhone: d.sponsorPhone || "",
      sponsorAddress: d.sponsorAddress || "",
      sponsorArabicName: d.sponsorArabicName || "",
      agentName: d.agentName || "",
      applicationNo: d.applicationNo || "",
      fileNo: d.fileNo || "",
      wakalaNo: d.wakalaNo || "",
      contractNo: d.contractNo || "",
      stickerVisaNo: d.stickerVisaNo || "",
      signedOn: d.signedOn || "",
      relativeName: d.relativeName || "",
      relativePhone: d.relativePhone || "",
      relativeKinship: d.relativeKinship || "",
      relativeGender: d.relativeGender || "",
      relativeBirthDate: d.relativeBirthDate || "",
      relativeCity: d.relativeCity || "",
      relativeRegion: d.relativeRegion || "",
      relativeSubcity: d.relativeSubcity || "",
      relativeWoreda: d.relativeWoreda || "",
      relativeHouseNo: d.relativeHouseNo || "",
      contactPerson2: d.contactPerson2 || "",
      contactPhone2: d.contactPhone2 || "",
      cocCenterName: d.cocCenterName || "",
      certificateNo: d.certificateNo || "",
      certifiedDate: d.certifiedDate || "",
      medicalPlace: d.medicalPlace || "",
    });
    setPhotoFile(null);
    setFullPhotoFile(null);
    setPassportFile(null);
    setLanguageRows(rowsFromCandidate(d));
    setGenerateVisaAfterSave(false);
    setExistingMedia({
      photo: !!d.photoPath,
      fullPhoto: !!d.fullPhotoPath,
      passport: false,
    });

    // Probe passport document separately — never assume it exists (avoids wrong preview).
    void (async () => {
      try {
        const res = await fetch(`/api/proxy/candidates/${candidateId}/media/passport`, {
          method: "HEAD",
          cache: "no-store",
        });
        // Some proxies don't support HEAD — fall back to GET range check via documents list
        if (res.ok) {
          setExistingMedia((m) => ({ ...m, passport: true }));
          return;
        }
      } catch {
        /* ignore */
      }
      try {
        const res = await fetch(`/api/proxy/candidates/${candidateId}/documents`);
        const body = await res.json();
        const docs = body?.data ?? [];
        const hasPassport = Array.isArray(docs) && docs.some((doc: { documentType?: number }) => doc.documentType === 0);
        setExistingMedia((m) => ({ ...m, passport: hasPassport }));
      } catch {
        setExistingMedia((m) => ({ ...m, passport: false }));
      }
    })();
  }, [isEdit, candidateResponse, reset, candidateId]);

  const goBack = () => {
    if (isEdit && candidateId) router.push(`/candidates/${candidateId}`);
    else router.push("/candidates");
  };

  /**
   * Essentials = the only fields the system actually requires. Once these are
   * filled the record can be saved immediately; everything after this step is
   * optional detail that can be added later from the candidate page. This keeps
   * registration short for field staff who are not comfortable with long forms.
   */
  const essentials = watch(ESSENTIAL_FIELDS);
  const essentialsComplete = essentials.every(
    (v) => typeof v === "string" && v.trim().length > 0,
  );

  const goToPreviousStep = () => {
    setCurrentStep((s) => Math.max(0, s - 1));
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const goToNextStep = async () => {
    const fields = STEP_FIELDS[currentStep] ?? [];
    if (fields.length > 0) {
      const ok = await trigger(fields);
      if (!ok) {
        toast.error("Please fix the highlighted fields before continuing.");
        return;
      }
    }
    setCurrentStep((s) => Math.min(lastStep, s + 1));
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const buildIntake = (data: RegisterCandidateForm) => ({
    localFullName: data.localFullName || null,
    placeOfBirth: data.placeOfBirth || null,
    religion: data.religion || null,
    maritalStatus: data.maritalStatus || null,
    numberOfChildren: data.numberOfChildren ? Number(data.numberOfChildren) : null,
    height: data.height ? `${data.height} cm` : null,
    weight: data.weight ? `${data.weight} kg` : null,
    nationalId: data.nationalId || null,
    biometricId: data.biometricId || null,
    passportType: data.passportType || "Normal",
    passportPlaceOfIssue: data.passportPlaceOfIssue || null,
    passportIssueDate: data.passportIssueDate || null,
    passportExpiryDate: data.passportExpiryDate || null,
    region: data.region || null,
    subcity: data.subcity || null,
    woreda: data.woreda || null,
    houseNo: data.houseNo || null,
    occupation: data.occupation || null,
    qualification: data.qualification || null,
    monthlySalary: data.monthlySalary || null,
    contractPeriod: data.contractPeriod || "2 Years",
    englishLevel: data.englishLevel || null,
    arabicLevel: data.arabicLevel || null,
    otherLanguages: data.otherLanguages || null,
    experienceAbroadYears: data.experienceAbroadYears
      ? Number(data.experienceAbroadYears)
      : null,
    worksIn: data.worksIn || null,
    referenceNo: data.referenceNo || null,
    remark: data.remark || null,
    cookingLevel: data.cookingLevel || null,
    skillCleaning: !!data.skillCleaning,
    skillWashing: !!data.skillWashing,
    skillCooking: !!data.skillCooking,
    skillIroning: !!data.skillIroning,
    skillSewing: !!data.skillSewing,
    skillBabysitting: !!data.skillBabysitting,
    skillChildCare: !!data.skillChildCare,
    visaNumber: data.visaNumber || null,
    visaType: data.visaType || "Work",
    sponsorName: data.sponsorName || null,
    sponsorIdNumber: data.sponsorIdNumber || null,
    sponsorPhone: data.sponsorPhone || null,
    sponsorAddress: data.sponsorAddress || null,
    sponsorArabicName: data.sponsorArabicName || null,
    agentName: data.agentName || null,
    applicationNo: isEdit ? data.applicationNo || null : null,
    fileNo: data.fileNo || null,
    wakalaNo: data.wakalaNo || null,
    contractNo: data.contractNo || null,
    stickerVisaNo: data.stickerVisaNo || null,
    signedOn: data.signedOn || null,
    relativeName: data.relativeName || null,
    relativePhone: data.relativePhone || null,
    relativeKinship: data.relativeKinship || null,
    relativeGender: data.relativeGender || null,
    relativeBirthDate: data.relativeBirthDate || null,
    relativeCity: data.relativeCity || null,
    relativeRegion: data.relativeRegion || null,
    relativeSubcity: data.relativeSubcity || null,
    relativeWoreda: data.relativeWoreda || null,
    relativeHouseNo: data.relativeHouseNo || null,
    contactPerson2: data.contactPerson2 || null,
    contactPhone2: data.contactPhone2 || null,
    cocCenterName: data.cocCenterName || null,
    certificateNo: data.certificateNo || null,
    certifiedDate: data.certifiedDate || null,
    medicalPlace: data.medicalPlace || null,
  });

  const uploadPhotos = async (id: string) => {
    try {
      if (photoFile) await uploadCandidateDocument(id, photoFile, PHOTO_TYPE);
      if (fullPhotoFile) await uploadCandidateDocument(id, fullPhotoFile, FULL_PHOTO_TYPE);
      if (passportFile) await uploadCandidateDocument(id, passportFile, PASSPORT_TYPE);
    } catch (uploadErr) {
      toast.error(
        uploadErr instanceof Error
          ? `Saved, but photo upload failed: ${uploadErr.message}`
          : "Saved, but photo upload failed"
      );
    }
  };

  const onSubmit = async (data: RegisterCandidateForm) => {
    try {
      const intake = buildIntake(data);

      if (isEdit && candidateId) {
        const response = await fetch(`/api/proxy/candidates/${candidateId}`, {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            firstName: data.firstName,
            lastName: data.lastName,
            middleName: data.middleName || null,
            passportNumber: data.passportNumber,
            dateOfBirth: data.dateOfBirth,
            gender: Number(data.gender),
            nationality: data.nationality || null,
            phoneNumber: data.phoneNumber || null,
            email: data.email || null,
            address: data.address || null,
            city: data.city || null,
            country: data.country || null,
            labourId: data.labourId || null,
            countryOfTravel: data.countryOfTravel || null,
            partnerName: data.partnerName || null,
            partnerAgencyId: data.partnerAgencyId || null,
            contractDate: data.contractDate || null,
            intake,
          }),
        });
        const result = await response.json();
        if (!result.isSuccess) {
          toast.error(formatApiError(result));
          return;
        }

        await uploadPhotos(candidateId);
        toast.success("Candidate updated");
        onSaved?.();
        mutate((key: unknown) => typeof key === "string" && key.includes("/candidates"));
        router.push(`/candidates/${candidateId}`);
        return;
      }

      const response = await fetch("/api/proxy/candidates", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          firstName: data.firstName,
          lastName: data.lastName,
          middleName: data.middleName || null,
          passportNumber: data.passportNumber,
          dateOfBirth: data.dateOfBirth,
          gender: Number(data.gender),
          nationality: data.nationality || null,
          phoneNumber: data.phoneNumber || null,
          email: data.email || null,
          address: data.address || null,
          city: data.city || null,
          country: data.country || null,
          labourId: data.labourId || null,
          countryOfTravel: data.countryOfTravel || null,
          partnerName: data.partnerName || null,
          partnerAgencyId: data.partnerAgencyId || null,
          contractDate: data.contractDate || null,
          intake,
        }),
      });

      const result = await response.json();
      if (!result.isSuccess) {
        toast.error(formatApiError(result));
        return;
      }

      const newId = result.data as string;
      await uploadPhotos(newId);

      if (generateVisaAfterSave) {
        try {
          const blob = await generateCandidateVisaForm(newId);
          const url = URL.createObjectURL(blob);
          window.open(url, "_blank");
          toast.success("Candidate registered · visa form generated");
        } catch {
          toast.success("Candidate registered (visa form could not be generated)");
        }
      } else {
        toast.success("Candidate registered successfully");
      }

      onSaved?.();
      mutate((key: unknown) => typeof key === "string" && key.includes("/candidates"));
      router.push(`/candidates/${newId}`);
    } catch {
      toast.error(isEdit ? "Update failed. Please try again." : "Registration failed. Please try again.");
    }
  };

  const photoUrl =
    isEdit && existingMedia.photo
      ? `/api/proxy/candidates/${candidateId}/media/photo`
      : null;
  const fullPhotoUrl =
    isEdit && existingMedia.fullPhoto
      ? `/api/proxy/candidates/${candidateId}/media/full-photo`
      : null;
  const passportUrl =
    isEdit && existingMedia.passport
      ? `/api/proxy/candidates/${candidateId}/media/passport`
      : null;

  return (
    <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 pb-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div className="space-y-1">
          <Link
            href={isEdit && candidateId ? `/candidates/${candidateId}` : "/candidates"}
            className="inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
          >
            <ArrowLeft className="h-4 w-4" />
            Back to {isEdit ? "profile" : "candidates"}
          </Link>
          <h1 className="flex items-center gap-2 text-2xl font-bold tracking-tight">
            <User className="h-6 w-6 text-emerald-700" />
            {isEdit ? "Edit Application" : "New Application"}
          </h1>
          <p className="text-sm text-muted-foreground">
            {isEdit
              ? "Update the application step by step — documents, identity, family, experience, then placement."
              : "Only name, passport, date of birth and gender are required. Fill those and press “Save now” — the rest can be added any time later."}
          </p>
        </div>
      </div>

      <div className="rounded-xl border border-slate-200/90 bg-white px-3 py-4 shadow-sm sm:px-6">
        <Stepper currentStep={currentStep} className="flex w-full items-start justify-between gap-1">
          {APPLICATION_STEPS.map((step, index) => (
            <StepperStep
              key={step.title}
              stepNumber={index}
              title={step.title}
              description={step.description}
              currentStep={currentStep}
              isLastStep={index === lastStep}
            />
          ))}
        </Stepper>
      </div>

      <form
        onSubmit={(e) => {
          if (currentStep < lastStep) {
            e.preventDefault();
            void goToNextStep();
            return;
          }
          void handleSubmit(onSubmit)(e);
        }}
        className="flex flex-col gap-4"
      >
          {/* Step 1 — Documents */}
          <div className={cn("space-y-4", currentStep !== 0 && "hidden")}>
            <FormSection
              icon={BookOpen}
              title="Passport scan"
              description="Upload a clear biodata page. We read the MRZ to fill name, passport number, dates, gender, and nationality."
            >
              <div className="grid gap-5 lg:grid-cols-12 lg:items-stretch">
                <div className="lg:col-span-7">
                  <PhotoPicker
                    key="picker-passport"
                    label="Passport biodata page"
                    hint="Full page with MRZ lines visible · JPEG/PNG · max 8 MB"
                    file={passportFile}
                    onChange={onPassportFileChange}
                    existingUrl={passportUrl}
                    aspect="passport"
                  />
                </div>
                <div className="flex flex-col gap-3 rounded-lg border border-emerald-100 bg-emerald-50/50 p-4 lg:col-span-5">
                  <div>
                    <p className="text-sm font-medium text-emerald-950">Auto-fill from MRZ</p>
                    <p className="mt-1 text-xs leading-relaxed text-emerald-900/70">
                      After upload, scanning fills Basic Information and Passport fields. Review on the Identity step.
                    </p>
                  </div>
                  <Button
                    type="button"
                    size="sm"
                    className="w-full gap-1.5 bg-emerald-700 hover:bg-emerald-800"
                    disabled={!passportFile || ocrBusy}
                    onClick={() => passportFile && void applyPassportOcr(passportFile)}
                  >
                    {ocrBusy ? (
                      <Loader2 className="h-3.5 w-3.5 animate-spin" />
                    ) : (
                      <ScanLine className="h-3.5 w-3.5" />
                    )}
                    {ocrBusy ? "Scanning…" : "Scan passport & fill form"}
                  </Button>
                  {ocrBusy ? (
                    <div className="space-y-2">
                      <div className="flex items-center justify-between gap-2 text-xs text-emerald-950">
                        <span className="truncate">{ocrStatus || "Scanning…"}</span>
                        <span className="shrink-0 tabular-nums font-semibold">{ocrPercent}%</span>
                      </div>
                      <Progress value={ocrPercent} className="h-2.5 bg-emerald-100" />
                    </div>
                  ) : (
                    <p className="text-[11px] leading-relaxed text-muted-foreground">
                      Tip: avoid glare on the bottom two MRZ lines for best results.
                    </p>
                  )}
                </div>
              </div>
            </FormSection>

            <FormSection
              icon={Camera}
              title="Candidate photos"
              description="Portrait for the CV and a full-body photo for agency forms. Separate from the passport scan above."
            >
              <div className="grid gap-5 sm:grid-cols-2">
                <div className="space-y-2">
                  <Label className="text-xs font-medium text-slate-700">Portrait</Label>
                  <PhotoPicker
                    key="picker-portrait"
                    label="Portrait photo"
                    hint="Head-and-shoulders · used on CV"
                    file={photoFile}
                    onChange={setPhotoFile}
                    existingUrl={photoUrl}
                    aspect="portrait"
                  />
                </div>
                <div className="space-y-2">
                  <Label className="text-xs font-medium text-slate-700">Full body</Label>
                  <PhotoPicker
                    key="picker-full"
                    label="Full-body photo"
                    hint="Standing photo · used on agency forms"
                    file={fullPhotoFile}
                    onChange={setFullPhotoFile}
                    existingUrl={fullPhotoUrl}
                    aspect="full"
                  />
                </div>
              </div>
            </FormSection>
          </div>

          {/* Step 2 — Identity */}
          <div className={cn("space-y-4", currentStep !== 1 && "hidden")}>
            <FormSection
              icon={FileText}
              title="Basic Information"
              description="Name and application meta. Passport scan fills these when available."
            >
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label>Application No.</Label>
                  <Input
                    value={
                      isEdit
                        ? watch("applicationNo") || "—"
                        : "Auto-generated on save"
                    }
                    readOnly
                    disabled
                    className="bg-muted/50 text-muted-foreground"
                  />
                </div>
                <div className="space-y-1.5">
                  <Label>Signed On</Label>
                  <Input type="date" {...register("signedOn")} />
                </div>
              </div>
              <div className="grid grid-cols-3 gap-3">
                <div className="space-y-1.5">
                  <Label>
                    First Name <span className="text-red-500">*</span>
                  </Label>
                  <Input {...register("firstName")} />
                  {errors.firstName && (
                    <p className="text-xs text-destructive">{errors.firstName.message}</p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label>Middle Name</Label>
                  <Input {...register("middleName")} />
                </div>
                <div className="space-y-1.5">
                  <Label>
                    Last Name <span className="text-red-500">*</span>
                  </Label>
                  <Input {...register("lastName")} />
                  {errors.lastName && (
                    <p className="text-xs text-destructive">{errors.lastName.message}</p>
                  )}
                </div>
              </div>
              <div className="space-y-1.5">
                <Label>Full Name (local / Amharic)</Label>
                <Input {...register("localFullName")} />
              </div>
            </FormSection>

            <FormSection
              icon={Stamp}
              title="Passport details"
              description="Document numbers and dates. Confirm values after scanning."
            >
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label>
                    Passport No. <span className="text-red-500">*</span>
                  </Label>
                  <Input {...register("passportNumber")} />
                  {errors.passportNumber && (
                    <p className="text-xs text-destructive">{errors.passportNumber.message}</p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label>Passport Type</Label>
                  <Select
                    value={watch("passportType") || undefined}
                    onValueChange={(v) => setValue("passportType", v)}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select type" />
                    </SelectTrigger>
                    <SelectContent position="popper" className="z-[200]">
                      {PASSPORT_TYPES.map((t) => (
                        <SelectItem key={t} value={t}>
                          {t}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  <Label>Place of Issue</Label>
                  <Input {...register("passportPlaceOfIssue")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Place of Birth</Label>
                  <Input {...register("placeOfBirth")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Date of Issue</Label>
                  <Input type="date" {...register("passportIssueDate")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Date of Expiry</Label>
                  <Input type="date" {...register("passportExpiryDate")} />
                </div>
                <div className="space-y-1.5">
                  <Label>
                    Date of Birth <span className="text-red-500">*</span>
                  </Label>
                  <Input type="date" {...register("dateOfBirth")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Phone No.</Label>
                  <PhoneInputField
                    value={watch("phoneNumber") || ""}
                    onChange={(v) => setValue("phoneNumber", v)}
                  />
                </div>
              </div>
            </FormSection>

            <FormSection
              icon={User}
              title="Details of Applicant"
              description="Personal profile used on CVs and agency paperwork."
            >
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label>
                    Gender <span className="text-red-500">*</span>
                  </Label>
                  <Select
                    value={watch("gender") || undefined}
                    onValueChange={(v) => setValue("gender", v, { shouldValidate: true })}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select" />
                    </SelectTrigger>
                    <SelectContent position="popper" className="z-[200]">
                      <SelectItem value="1">Female</SelectItem>
                      <SelectItem value="0">Male</SelectItem>
                    </SelectContent>
                  </Select>
                  {errors.gender && (
                    <p className="text-xs text-destructive">{errors.gender.message}</p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label>Religion</Label>
                  <Select
                    value={watch("religion") || undefined}
                    onValueChange={(v) => setValue("religion", v)}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select religion" />
                    </SelectTrigger>
                    <SelectContent position="popper" className="z-[200]">
                      {RELIGIONS.map((r) => (
                        <SelectItem key={r} value={r}>
                          {r}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  <Label>Marital Status</Label>
                  <Select
                    value={watch("maritalStatus") || undefined}
                    onValueChange={(v) => setValue("maritalStatus", v)}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select status" />
                    </SelectTrigger>
                    <SelectContent position="popper" className="z-[200]">
                      {MARITAL_STATUSES.map((s) => (
                        <SelectItem key={s} value={s}>
                          {s}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  <Label>No. of Children</Label>
                  <Input
                    type="number"
                    min={0}
                    step={1}
                    inputMode="numeric"
                    placeholder="0"
                    {...register("numberOfChildren")}
                  />
                  {errors.numberOfChildren && (
                    <p className="text-xs text-destructive">{errors.numberOfChildren.message}</p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label>Height (cm)</Label>
                  <Input
                    type="number"
                    min={1}
                    step={1}
                    inputMode="decimal"
                    placeholder="e.g. 165"
                    {...register("height")}
                  />
                  {errors.height && (
                    <p className="text-xs text-destructive">{errors.height.message}</p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label>Weight (kg)</Label>
                  <Input
                    type="number"
                    min={1}
                    step={0.1}
                    inputMode="decimal"
                    placeholder="e.g. 55"
                    {...register("weight")}
                  />
                  {errors.weight && (
                    <p className="text-xs text-destructive">{errors.weight.message}</p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label>Occupation</Label>
                  <Select
                    value={watch("occupation") || undefined}
                    onValueChange={(v) => setValue("occupation", v)}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select occupation" />
                    </SelectTrigger>
                    <SelectContent position="popper" className="z-[200]">
                      {OCCUPATIONS.map((o) => (
                        <SelectItem key={o} value={o}>
                          {o}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  <Label>Nationality</Label>
                  <CountrySelect
                    value={watch("nationality") || ""}
                    onChange={(v) => setValue("nationality", v)}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label>Region</Label>
                  <Input {...register("region")} />
                </div>
                <div className="space-y-1.5">
                  <Label>City</Label>
                  <Input {...register("city")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Subcity / Zone</Label>
                  <Input {...register("subcity")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Woreda</Label>
                  <Input {...register("woreda")} />
                </div>
                <div className="space-y-1.5">
                  <Label>House No.</Label>
                  <Input {...register("houseNo")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Address</Label>
                  <Input {...register("address")} />
                </div>
              </div>
            </FormSection>
          </div>

          {/* Step 3 — Family */}
          <div className={cn("space-y-4", currentStep !== 2 && "hidden")}>
            <FormSection
              icon={Users}
              title="Relative Information"
              description="Emergency contact and next of kin."
            >
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label>Relative Name</Label>
                  <Input {...register("relativeName")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Relative Kinship</Label>
                  <Input {...register("relativeKinship")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Relative Phone</Label>
                  <Input {...register("relativePhone")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Relative Gender</Label>
                  <Input {...register("relativeGender")} />
                </div>
                <div className="space-y-1.5">
                  <Label>City</Label>
                  <Input {...register("relativeCity")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Region</Label>
                  <Input {...register("relativeRegion")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Subcity / Zone</Label>
                  <Input {...register("relativeSubcity")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Woreda</Label>
                  <Input {...register("relativeWoreda")} />
                </div>
                <div className="space-y-1.5">
                  <Label>House No.</Label>
                  <Input {...register("relativeHouseNo")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Birth Date</Label>
                  <Input type="date" {...register("relativeBirthDate")} />
                </div>
              </div>
            </FormSection>

            <FormSection icon={Mail} title="Other Information">
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label>Contact Person (2nd)</Label>
                  <Input {...register("contactPerson2")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Contact Phone (2nd)</Label>
                  <Input {...register("contactPhone2")} />
                </div>
                <div className="space-y-1.5">
                  <Label>COC Center</Label>
                  <Input {...register("cocCenterName")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Certificate No.</Label>
                  <Input {...register("certificateNo")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Certified Date</Label>
                  <Input type="date" {...register("certifiedDate")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Medical Place</Label>
                  <Input {...register("medicalPlace")} />
                </div>
              </div>
            </FormSection>
          </div>

          {/* Step 4 — Experience */}
          <div className={cn("space-y-4", currentStep !== 3 && "hidden")}>
            <FormSection icon={FileText} title="Languages & Education">
              <div className="flex items-center justify-between gap-3">
                <p className="text-xs text-muted-foreground">Add spoken languages and education level.</p>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="gap-1.5"
                  onClick={() => {
                    const next = [...languageRows, newLanguageRow()];
                    setLanguageRows(next);
                    syncLanguagesToForm(next, setValue);
                  }}
                >
                  <Plus className="h-3.5 w-3.5" />
                  Add language
                </Button>
              </div>
              <div className="space-y-3">
                {languageRows.map((row, index) => (
                  <div key={row.id} className="grid grid-cols-[1fr_1fr_auto] gap-3 items-end">
                    <div className="space-y-1.5">
                      {index === 0 ? <Label>Language</Label> : null}
                      <Select
                        value={row.language || undefined}
                        onValueChange={(language) => {
                          const next = languageRows.map((r) =>
                            r.id === row.id ? { ...r, language } : r
                          );
                          setLanguageRows(next);
                          syncLanguagesToForm(next, setValue);
                        }}
                      >
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Select language" />
                        </SelectTrigger>
                        <SelectContent position="popper" className="z-[200]">
                          {LANGUAGE_OPTIONS.map((lang) => (
                            <SelectItem key={lang} value={lang}>
                              {lang}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <div className="space-y-1.5">
                      {index === 0 ? <Label>Proficiency</Label> : null}
                      <Select
                        value={row.level || undefined}
                        onValueChange={(level) => {
                          const next = languageRows.map((r) =>
                            r.id === row.id ? { ...r, level } : r
                          );
                          setLanguageRows(next);
                          syncLanguagesToForm(next, setValue);
                        }}
                      >
                        <SelectTrigger className="w-full">
                          <SelectValue placeholder="Select level" />
                        </SelectTrigger>
                        <SelectContent position="popper" className="z-[200]">
                          {LANGUAGE_LEVELS.map((l) => (
                            <SelectItem key={l} value={l}>
                              {l}
                            </SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </div>
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      className="h-9 w-9 shrink-0 text-muted-foreground hover:text-destructive"
                      disabled={languageRows.length <= 1}
                      aria-label="Remove language"
                      onClick={() => {
                        const next = languageRows.filter((r) => r.id !== row.id);
                        setLanguageRows(next);
                        syncLanguagesToForm(next, setValue);
                      }}
                    >
                      <X className="h-4 w-4" />
                    </Button>
                  </div>
                ))}
              </div>
              <div className="space-y-1.5">
                <Label>Education</Label>
                <Input
                  {...register("qualification")}
                  placeholder="e.g. SECONDARY LEVEL"
                />
              </div>
            </FormSection>
            <FormSection icon={MapPin} title="Work Experience">
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label>Period (years abroad)</Label>
                  <Input
                    type="number"
                    min={0}
                    step={1}
                    inputMode="numeric"
                    {...register("experienceAbroadYears")}
                    placeholder="e.g. 2"
                  />
                  {errors.experienceAbroadYears && (
                    <p className="text-xs text-destructive">
                      {errors.experienceAbroadYears.message}
                    </p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label>Country</Label>
                  <Input
                    {...register("worksIn")}
                    placeholder="Country worked in"
                  />
                </div>
                <div className="space-y-1.5">
                  <Label>Salary (ETB / SAR)</Label>
                  <Input
                    type="number"
                    min={1}
                    step={1}
                    inputMode="decimal"
                    {...register("monthlySalary")}
                  />
                  {errors.monthlySalary && (
                    <p className="text-xs text-destructive">{errors.monthlySalary.message}</p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label>Contract Period</Label>
                  <Input {...register("contractPeriod")} placeholder="2 Years" />
                </div>
                <div className="space-y-1.5">
                  <Label>Country of Travel</Label>
                  <CountrySelect
                    value={watch("countryOfTravel") || ""}
                    onChange={(v) => setValue("countryOfTravel", v)}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label>Reference No.</Label>
                  <Input {...register("referenceNo")} />
                </div>
              </div>
            </FormSection>
            <FormSection icon={Stamp} title="Skills & Experience">
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label>Cooking Level</Label>
                  <Select
                    value={watch("cookingLevel") || undefined}
                    onValueChange={(v) => setValue("cookingLevel", v)}
                  >
                    <SelectTrigger className="w-full">
                      <SelectValue placeholder="Select level" />
                    </SelectTrigger>
                    <SelectContent position="popper" className="z-[200]">
                      {LANGUAGE_LEVELS.map((l) => (
                        <SelectItem key={l} value={l}>
                          {l}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              </div>
              <div className="flex flex-wrap gap-4 pt-1">
                <SkillCheck
                  id="skillCleaning"
                  label="Cleaning"
                  checked={!!watch("skillCleaning")}
                  onChange={(v) => setValue("skillCleaning", v)}
                />
                <SkillCheck
                  id="skillWashing"
                  label="Washing"
                  checked={!!watch("skillWashing")}
                  onChange={(v) => setValue("skillWashing", v)}
                />
                <SkillCheck
                  id="skillCooking"
                  label="Cooking"
                  checked={!!watch("skillCooking")}
                  onChange={(v) => setValue("skillCooking", v)}
                />
                <SkillCheck
                  id="skillBabysitting"
                  label="Baby Sitting"
                  checked={!!watch("skillBabysitting")}
                  onChange={(v) => setValue("skillBabysitting", v)}
                />
                <SkillCheck
                  id="skillChildCare"
                  label="Child care"
                  checked={!!watch("skillChildCare")}
                  onChange={(v) => setValue("skillChildCare", v)}
                />
                <SkillCheck
                  id="skillIroning"
                  label="Ironing"
                  checked={!!watch("skillIroning")}
                  onChange={(v) => setValue("skillIroning", v)}
                />
                <SkillCheck
                  id="skillSewing"
                  label="Sewing"
                  checked={!!watch("skillSewing")}
                  onChange={(v) => setValue("skillSewing", v)}
                />
              </div>
              <div className="space-y-1.5">
                <Label>Remark</Label>
                <Textarea rows={3} {...register("remark")} />
              </div>
            </FormSection>
          </div>

          {/* Step 5 — Placement */}
          <div className={cn("space-y-4", currentStep !== 4 && "hidden")}>
            <FormSection
              icon={Stamp}
              title="Sponsor & Visa"
              description="Visa and sponsor details for the destination country."
            >
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label>Visa No.</Label>
                  <Input {...register("visaNumber")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Sponsor Name</Label>
                  <Input {...register("sponsorName")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Sponsor ID</Label>
                  <Input {...register("sponsorIdNumber")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Sponsor Phone</Label>
                  <Input {...register("sponsorPhone")} />
                </div>
                <div className="space-y-1.5 col-span-2">
                  <Label>Sponsor Address</Label>
                  <Input {...register("sponsorAddress")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Sponsor Arabic</Label>
                  <Input {...register("sponsorArabicName")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Arab agent / local agent</Label>
                  <Input
                    placeholder="Agent name at the foreign agency"
                    {...register("agentName")}
                  />
                </div>
                <div className="space-y-1.5">
                  <Label>National ID</Label>
                  <Input {...register("nationalId")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Visa Type</Label>
                  <Select
                    value={watch("visaType") || undefined}
                    onValueChange={(v) => setValue("visaType", v)}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select type" />
                    </SelectTrigger>
                    <SelectContent position="popper" className="z-[200]">
                      {VISA_TYPES.map((t) => (
                        <SelectItem key={t} value={t}>
                          {t}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="space-y-1.5">
                  <Label>Email</Label>
                  <Input type="email" {...register("email")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Labour ID</Label>
                  <Input {...register("labourId")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Biometric ID</Label>
                  <Input {...register("biometricId")} />
                </div>
                <div className="space-y-1.5">
                  <Label>File No.</Label>
                  <Input {...register("fileNo")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Contract #</Label>
                  <Input {...register("contractNo")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Wakala #</Label>
                  <Input {...register("wakalaNo")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Sticker Visa #</Label>
                  <Input {...register("stickerVisaNo")} />
                </div>
              </div>
            </FormSection>
            <FormSection
              icon={MapPin}
              title="Partner / travel"
              description="Select the foreign (Arab) agency you are sending this candidate to. Only partners linked to your agency appear here."
            >
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5 col-span-2">
                  <Label>Send-to partner agency</Label>
                  <Select
                    value={watch("partnerAgencyId") || undefined}
                    onValueChange={(id) => {
                      setValue("partnerAgencyId", id, { shouldValidate: true });
                      const p = linkedPartners.find((x) => x.id === id);
                      if (p) {
                        setValue("partnerName", p.name);
                        if (p.country) setValue("countryOfTravel", p.country);
                        if (p.country) setValue("country", p.country);
                      }
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select linked partner" />
                    </SelectTrigger>
                    <SelectContent position="popper" className="z-[200]">
                      {linkedPartners.map((p) => (
                        <SelectItem key={p.id} value={p.id}>
                          {p.name} · {p.country}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  {linkedPartners.length === 0 ? (
                    <p className="text-xs text-amber-800">
                      No partners linked. An agency owner should link partners under Partners first.
                    </p>
                  ) : null}
                </div>
                <div className="space-y-1.5">
                  <Label>Partner name (snapshot)</Label>
                  <Input {...register("partnerName")} readOnly className="bg-muted/40" />
                </div>
                <div className="space-y-1.5">
                  <Label>Contract Date</Label>
                  <Input type="date" {...register("contractDate")} />
                </div>
                <div className="space-y-1.5">
                  <Label className="flex items-center gap-1">
                    <MapPin className="h-3 w-3" /> Country of travel
                  </Label>
                  <CountrySelect
                    value={watch("countryOfTravel") || watch("country") || ""}
                    onChange={(v) => {
                      setValue("countryOfTravel", v);
                      setValue("country", v);
                    }}
                  />
                </div>
              </div>
            </FormSection>
            {!isEdit && (
              <label className="flex items-center gap-2 text-sm">
                <Checkbox
                  checked={generateVisaAfterSave}
                  onCheckedChange={(v) => setGenerateVisaAfterSave(v === true)}
                />
                Generate visa form after save
              </label>
            )}
          </div>

          <div className="sticky bottom-0 z-30 mt-6 rounded-xl border border-slate-200/90 bg-background/95 px-4 py-3 shadow-[0_-8px_24px_rgba(15,23,42,0.06)] backdrop-blur supports-[backdrop-filter]:bg-background/90">
            <div className="flex items-center justify-between gap-3">
              <Button type="button" variant="outline" onClick={goBack}>
                Cancel
              </Button>
              <div className="flex items-center gap-2">
                <span className="hidden text-xs text-muted-foreground sm:inline">
                  Step {currentStep + 1} of {APPLICATION_STEPS.length}
                </span>
                {currentStep > 0 ? (
                  <Button type="button" variant="outline" onClick={goToPreviousStep} className="gap-1.5">
                    <ArrowLeft className="h-4 w-4" />
                    Back
                  </Button>
                ) : null}
                {/* Escape hatch: once the essentials are filled the user can save
                    immediately instead of stepping through the optional sections. */}
                {currentStep > 0 && currentStep < lastStep && essentialsComplete ? (
                  <Button
                    type="button"
                    variant="outline"
                    disabled={isSubmitting}
                    title="Save now and add the remaining details later"
                    // Submit directly: the form's onSubmit turns submits into
                    // "next step" while on an intermediate step.
                    onClick={() => void handleSubmit(onSubmit)()}
                  >
                    {isSubmitting ? "Saving…" : "Save now"}
                  </Button>
                ) : null}
                {currentStep < lastStep ? (
                  <Button
                    type="button"
                    onClick={() => void goToNextStep()}
                    className="gap-1.5 bg-emerald-800 hover:bg-emerald-900 text-white"
                  >
                    Next
                    <ArrowRight className="h-4 w-4" />
                  </Button>
                ) : (
                  <Button type="submit" disabled={isSubmitting} className="bg-emerald-800 hover:bg-emerald-900 text-white">
                    {isSubmitting ? "Saving…" : isEdit ? "Save Changes" : "Save"}
                  </Button>
                )}
              </div>
            </div>
          </div>
        </form>
    </div>
  );
}

/** @deprecated Use CandidateApplicationForm on a full page instead. */
export const CreateCandidateSheet = CandidateApplicationForm;
