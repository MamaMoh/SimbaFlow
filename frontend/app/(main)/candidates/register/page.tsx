"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useRouter } from "next/navigation";

const registerCandidateSchema = z.object({
  firstName: z.string().min(2, "First name must be at least 2 characters"),
  lastName: z.string().min(2, "Last name must be at least 2 characters"),
  middleName: z.string().optional(),
  passportNumber: z.string().min(5).max(20).regex(/^[A-Za-z0-9]+$/, "Alphanumeric only"),
  dateOfBirth: z.string().min(1, "Date of birth is required"),
  gender: z.number().min(0).max(1),
  nationality: z.string().optional(),
  phoneNumber: z.string().optional(),
  email: z.string().email().optional().or(z.literal("")),
  address: z.string().optional(),
  city: z.string().optional(),
  country: z.string().optional(),
  labourId: z.string().optional(),
  countryOfTravel: z.string().optional(),
  officeName: z.string().optional(),
  contractDate: z.string().optional(),
  officeId: z.string().optional().default("00000000-0000-0000-0000-000000000000"),
});

type RegisterCandidateForm = z.infer<typeof registerCandidateSchema>;

export default function RegisterCandidatePage() {
  const router = useRouter();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RegisterCandidateForm>({
    resolver: zodResolver(registerCandidateSchema),
  });

  const onSubmit = async (data: RegisterCandidateForm) => {
    try {
      const response = await fetch("/api/proxy/candidates", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          ...data,
          gender: Number(data.gender),
          officeId: data.officeId || "00000000-0000-0000-0000-000000000000",
        }),
      });

      const result = await response.json();
      if (result.isSuccess) {
        router.push(`/candidates/${result.data}`);
      } else {
        console.error(result.error || "Registration failed");
      }
    } catch (error) {
      console.error("Registration failed:", error);
      console.error("Registration failed");
    }
  };

  return (
    <div className="flex flex-col gap-6 p-6 max-w-2xl">
      <h1 className="text-2xl font-semibold">Register New Candidate</h1>

      <form
        data-testid="register-candidate-form"
        onSubmit={handleSubmit(onSubmit)}
        className="space-y-6"
      >
        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="firstName">First Name *</Label>
            <Input id="firstName" {...register("firstName")} data-testid="candidate-first-name" />
            {errors.firstName && <p className="text-sm text-destructive">{errors.firstName.message}</p>}
          </div>
          <div className="space-y-2">
            <Label htmlFor="lastName">Last Name *</Label>
            <Input id="lastName" {...register("lastName")} data-testid="candidate-last-name" />
            {errors.lastName && <p className="text-sm text-destructive">{errors.lastName.message}</p>}
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="passportNumber">Passport Number *</Label>
            <Input id="passportNumber" {...register("passportNumber")} data-testid="candidate-passport" />
            {errors.passportNumber && <p className="text-sm text-destructive">{errors.passportNumber.message}</p>}
          </div>
          <div className="space-y-2">
            <Label htmlFor="dateOfBirth">Date of Birth *</Label>
            <Input id="dateOfBirth" type="date" {...register("dateOfBirth")} data-testid="candidate-dob" />
            {errors.dateOfBirth && <p className="text-sm text-destructive">{errors.dateOfBirth.message}</p>}
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="labourId">Labour ID</Label>
            <Input id="labourId" {...register("labourId")} data-testid="candidate-labour-id" />
          </div>
          <div className="space-y-2">
            <Label htmlFor="countryOfTravel">Country of Travel</Label>
            <Input id="countryOfTravel" {...register("countryOfTravel")} data-testid="candidate-country-travel" />
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="phoneNumber">Phone Number</Label>
            <Input id="phoneNumber" {...register("phoneNumber")} data-testid="candidate-phone" />
          </div>
          <div className="space-y-2">
            <Label htmlFor="nationality">Nationality</Label>
            <Input id="nationality" {...register("nationality")} data-testid="candidate-nationality" />
          </div>
        </div>

        <Button type="submit" disabled={isSubmitting} data-testid="register-candidate-submit-button">
          {isSubmitting ? "Registering..." : "Register Candidate"}
        </Button>
      </form>
    </div>
  );
}
