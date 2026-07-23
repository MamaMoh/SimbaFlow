"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useRouter } from "next/navigation";
import { candidatesApi, officesApi, USE_MOCKS } from "@/lib/api/candidates-api";
import useSWR from "swr";
import { toast } from "sonner";

const registerCandidateSchema = z.object({
  firstName: z.string().min(2, "First name must be at least 2 characters"),
  lastName: z.string().min(2, "Last name must be at least 2 characters"),
  middleName: z.string().optional(),
  passportNumber: z.string().min(5).max(20).regex(/^[A-Za-z0-9]+$/, "Alphanumeric only"),
  passportType: z.string().optional(),
  passportIssueDate: z.string().optional(),
  passportExpiryDate: z.string().optional(),
  placeOfIssue: z.string().optional(),
  placeOfBirth: z.string().optional(),
  dateOfBirth: z.string().min(1, "Date of birth is required"),
  gender: z.coerce.number().min(0).max(1),
  nationality: z.string().optional(),
  religion: z.string().optional(),
  maritalStatus: z.string().optional(),
  occupation: z.string().optional(),
  qualification: z.string().optional(),
  phoneNumber: z.string().optional(),
  phone2: z.string().optional(),
  email: z.string().email().optional().or(z.literal("")),
  region: z.string().optional(),
  subcity: z.string().optional(),
  woreda: z.string().optional(),
  houseNo: z.string().optional(),
  city: z.string().optional(),
  country: z.string().optional(),
  labourId: z.string().optional(),
  countryOfTravel: z.string().optional(),
  worksIn: z.string().optional(),
  sponsorName: z.string().optional(),
  sponsorId: z.string().optional(),
  visaNumber: z.string().optional(),
  agent: z.string().optional(),
  contractDate: z.string().optional(),
  relativeName: z.string().optional(),
  relativePhone: z.string().optional(),
  relativeKinship: z.string().optional(),
  englishLevel: z.string().optional(),
  arabicLevel: z.string().optional(),
  canIron: z.boolean().optional(),
  canClean: z.boolean().optional(),
  canCook: z.boolean().optional(),
  officeId: z.string().min(1, "Office is required"),
});

type RegisterCandidateForm = z.infer<typeof registerCandidateSchema>;

export default function RegisterCandidatePage() {
  const router = useRouter();
  const { data: officesResult } = useSWR("offices", () => officesApi.list());
  const offices = officesResult?.data ?? [];

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RegisterCandidateForm>({
    resolver: zodResolver(registerCandidateSchema),
    defaultValues: {
      gender: 0,
      nationality: "Ethiopia",
      passportType: "Normal",
      officeId: offices[0]?.id ?? "",
    },
  });

  const onSubmit = async (data: RegisterCandidateForm) => {
    try {
      const result = await candidatesApi.register({
        ...data,
        gender: Number(data.gender),
        placement: {
          worksIn: data.worksIn ?? data.countryOfTravel,
          countryOfTravel: data.countryOfTravel,
          sponsorName: data.sponsorName,
          sponsorId: data.sponsorId,
          visaNumber: data.visaNumber,
          agent: data.agent,
          contractDate: data.contractDate,
        },
        relative: data.relativeName
          ? {
              relativeName: data.relativeName,
              relativePhone: data.relativePhone,
              relativeKinship: data.relativeKinship,
            }
          : undefined,
        skills: {
          englishLevel: data.englishLevel,
          arabicLevel: data.arabicLevel,
          canIron: data.canIron,
          canClean: data.canClean,
          canCook: data.canCook,
        },
      });

      if (result.isSuccess) {
        toast.success(USE_MOCKS ? "Candidate registered" : "Candidate registered");
        router.push(USE_MOCKS ? `/candidates/${result.data}` : `/candidates`);
      } else {
        toast.error(result.error || "Registration failed");
      }
    } catch (error) {
      console.error(error);
      toast.error("Registration failed");
    }
  };

  return (
    <div className="flex flex-col gap-6 p-6 max-w-4xl">
      <div>
        <h1 className="text-2xl font-semibold">Register New Candidate</h1>
        {USE_MOCKS && <p className="text-sm text-muted-foreground">Using mock API</p>}
      </div>

      <form
        data-testid="register-candidate-form"
        onSubmit={handleSubmit(onSubmit)}
        className="space-y-8"
      >
        <section className="space-y-4">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Identity</h2>
          <div className="grid grid-cols-2 gap-4">
            <Field label="First Name *" error={errors.firstName?.message}>
              <Input {...register("firstName")} data-testid="candidate-first-name" />
            </Field>
            <Field label="Last Name *" error={errors.lastName?.message}>
              <Input {...register("lastName")} />
            </Field>
            <Field label="Middle Name">
              <Input {...register("middleName")} />
            </Field>
            <Field label="Passport No. *" error={errors.passportNumber?.message}>
              <Input {...register("passportNumber")} />
            </Field>
            <Field label="Date of Birth *" error={errors.dateOfBirth?.message}>
              <Input type="date" {...register("dateOfBirth")} />
            </Field>
            <Field label="Gender">
              <select className="border rounded-md h-9 px-2 w-full" {...register("gender")}>
                <option value={0}>Male</option>
                <option value={1}>Female</option>
              </select>
            </Field>
            <Field label="Phone">
              <Input {...register("phoneNumber")} />
            </Field>
            <Field label="Labour ID">
              <Input {...register("labourId")} />
            </Field>
            <Field label="Office *" error={errors.officeId?.message}>
              <select className="border rounded-md h-9 px-2 w-full" {...register("officeId")}>
                <option value="">Select…</option>
                {offices.map((o) => (
                  <option key={o.id} value={o.id}>
                    {o.name}
                  </option>
                ))}
              </select>
            </Field>
            <Field label="Country of Travel">
              <Input {...register("countryOfTravel")} />
            </Field>
          </div>
        </section>

        <section className="space-y-4">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Sponsor & visa</h2>
          <div className="grid grid-cols-2 gap-4">
            <Field label="Sponsor Name">
              <Input {...register("sponsorName")} />
            </Field>
            <Field label="Sponsor ID">
              <Input {...register("sponsorId")} />
            </Field>
            <Field label="Visa Number">
              <Input {...register("visaNumber")} />
            </Field>
            <Field label="Agent">
              <Input {...register("agent")} />
            </Field>
            <Field label="Contract Date">
              <Input type="date" {...register("contractDate")} />
            </Field>
          </div>
        </section>

        <section className="space-y-4">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Relative</h2>
          <div className="grid grid-cols-2 gap-4">
            <Field label="Relative Name">
              <Input {...register("relativeName")} />
            </Field>
            <Field label="Relative Phone">
              <Input {...register("relativePhone")} />
            </Field>
            <Field label="Kinship">
              <Input {...register("relativeKinship")} />
            </Field>
          </div>
        </section>

        <section className="space-y-4">
          <h2 className="text-sm font-semibold uppercase tracking-wide text-muted-foreground">Skills</h2>
          <div className="grid grid-cols-2 gap-4">
            <Field label="English">
              <Input {...register("englishLevel")} />
            </Field>
            <Field label="Arabic">
              <Input {...register("arabicLevel")} />
            </Field>
          </div>
          <div className="flex gap-4 text-sm">
            <label className="flex items-center gap-2">
              <input type="checkbox" {...register("canIron")} /> Ironing
            </label>
            <label className="flex items-center gap-2">
              <input type="checkbox" {...register("canClean")} /> Cleaning
            </label>
            <label className="flex items-center gap-2">
              <input type="checkbox" {...register("canCook")} /> Cooking
            </label>
          </div>
        </section>

        <div className="flex justify-end gap-2">
          <Button type="button" variant="outline" onClick={() => router.back()}>
            Cancel
          </Button>
          <Button type="submit" disabled={isSubmitting} data-testid="register-candidate-submit-button">
            {isSubmitting ? "Saving…" : "Save"}
          </Button>
        </div>
      </form>
    </div>
  );
}

function Field({
  label,
  error,
  children,
}: {
  label: string;
  error?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      {children}
      {error && <p className="text-sm text-destructive">{error}</p>}
    </div>
  );
}
