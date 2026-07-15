"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
  SheetFooter,
} from "@/components/ui/sheet";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import { User, Mail, MapPin, FileText } from "lucide-react";
import { useSWRConfig } from "swr";
import { toast } from "sonner";
import { CountrySelect } from "@/components/ui/country-select";
import { PhoneInputField } from "@/components/ui/phone-input";

const registerCandidateSchema = z.object({
  firstName: z.string().min(2, "First name must be at least 2 characters"),
  lastName: z.string().min(2, "Last name must be at least 2 characters"),
  middleName: z.string().optional(),
  passportNumber: z.string().min(5).max(20).regex(/^[A-Za-z0-9]+$/, "Alphanumeric only"),
  dateOfBirth: z.string().min(1, "Date of birth is required"),
  gender: z.string().min(1, "Gender is required"),
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
});

type RegisterCandidateForm = z.infer<typeof registerCandidateSchema>;

interface CreateCandidateSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function CreateCandidateSheet({ open, onOpenChange }: CreateCandidateSheetProps) {
  const { mutate } = useSWRConfig();
  const {
    register,
    handleSubmit,
    setValue,
    watch,
    reset,
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
          officeId: "00000000-0000-0000-0000-000000000000",
        }),
      });

      const result = await response.json();
      if (result.isSuccess) {
        toast.success("Candidate registered successfully");
        reset();
        onOpenChange(false);
        // Revalidate the candidates list
        mutate((key: string) => typeof key === "string" && key.includes("/candidates"));
      } else {
        toast.error(result.error || "Registration failed");
      }
    } catch (error) {
      toast.error("Registration failed. Please try again.");
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-[540px] sm:max-w-[540px] flex flex-col px-6">
        <SheetHeader className="pb-4">
          <SheetTitle className="flex items-center gap-2">
            <User className="h-5 w-5 text-green-700" />
            Add New Candidate
          </SheetTitle>
          <SheetDescription>
            Fill in the details to register a new candidate.
          </SheetDescription>
        </SheetHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col flex-1 overflow-hidden">
          <div className="flex-1 overflow-y-auto space-y-6 pr-1">
          {/* Basic Information */}
          <div className="space-y-1.5">
            <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
              <FileText className="h-4 w-4 text-green-700" />
              Basic Information
            </h3>
            <div className="space-y-4">
              <div className="space-y-1.5">
                <Label htmlFor="firstName">First Name <span className="text-red-500">*</span></Label>
                <Input id="firstName" placeholder="Enter first name" {...register("firstName")} data-testid="candidate-first-name" />
                {errors.firstName && <p className="text-xs text-destructive mt-1">{errors.firstName.message}</p>}
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="lastName">Last Name <span className="text-red-500">*</span></Label>
                <Input id="lastName" placeholder="Enter last name" {...register("lastName")} data-testid="candidate-last-name" />
                {errors.lastName && <p className="text-xs text-destructive mt-1">{errors.lastName.message}</p>}
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="middleName">Middle Name</Label>
                <Input id="middleName" placeholder="Enter middle name" {...register("middleName")} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="passportNumber">Passport Number <span className="text-red-500">*</span></Label>
                <Input id="passportNumber" placeholder="Enter passport number" {...register("passportNumber")} data-testid="candidate-passport" />
                {errors.passportNumber && <p className="text-xs text-destructive mt-1">{errors.passportNumber.message}</p>}
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <Label htmlFor="dateOfBirth">Date of Birth <span className="text-red-500">*</span></Label>
                  <Input id="dateOfBirth" type="date" {...register("dateOfBirth")} data-testid="candidate-dob" />
                  {errors.dateOfBirth && <p className="text-xs text-destructive mt-1">{errors.dateOfBirth.message}</p>}
                </div>
                <div className="space-y-1.5">
                  <Label>Gender <span className="text-red-500">*</span></Label>
                  <Select onValueChange={(val) => setValue("gender", val)}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select gender" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="0">Male</SelectItem>
                      <SelectItem value="1">Female</SelectItem>
                    </SelectContent>
                  </Select>
                  {errors.gender && <p className="text-xs text-destructive mt-1">{errors.gender.message}</p>}
                </div>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="nationality">Nationality</Label>
                <CountrySelect value={watch("nationality") || ""} onChange={(val) => setValue("nationality", val)} placeholder="Select nationality" />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="labourId">Labour ID</Label>
                <Input id="labourId" placeholder="Enter labour ID" {...register("labourId")} />
              </div>
            </div>
          </div>

          <Separator />

          {/* Contact Information */}
          <div className="space-y-1.5">
            <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
              <Mail className="h-4 w-4 text-green-700" />
              Contact Information
            </h3>
            <div className="space-y-4">
              <div className="space-y-1.5">
                <Label htmlFor="email">Email</Label>
                <Input id="email" type="email" placeholder="Enter email address" {...register("email")} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="phoneNumber">Phone Number</Label>
                <PhoneInputField value={watch("phoneNumber") || ""} onChange={(val) => setValue("phoneNumber", val)} />
              </div>
            </div>
          </div>

          <Separator />

          {/* Travel & Contract */}
          <div className="space-y-1.5">
            <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
              <MapPin className="h-4 w-4 text-green-700" />
              Travel & Contract
            </h3>
            <div className="space-y-4">
              <div className="space-y-1.5">
                <Label htmlFor="countryOfTravel">Country of Travel</Label>
                <CountrySelect value={watch("countryOfTravel") || ""} onChange={(val) => setValue("countryOfTravel", val)} placeholder="Select destination country" />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="officeName">Office / Employer Name</Label>
                <Input id="officeName" placeholder="Enter overseas office or employer" {...register("officeName")} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="contractDate">Contract Date</Label>
                <Input id="contractDate" type="date" {...register("contractDate")} />
              </div>
            </div>
          </div>

          <Separator />

          {/* Address */}
          <div className="space-y-1.5">
            <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
              <MapPin className="h-4 w-4 text-green-700" />
              Address
            </h3>
            <div className="space-y-4">
              <div className="space-y-1.5">
                <Label htmlFor="address">Street Address</Label>
                <Input id="address" placeholder="Enter street address" {...register("address")} />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <Label htmlFor="city">City</Label>
                  <Input id="city" placeholder="City" {...register("city")} />
                </div>
                <div className="space-y-1.5">
                  <Label htmlFor="country">Country</Label>
                  <CountrySelect value={watch("country") || ""} onChange={(val) => setValue("country", val)} placeholder="Country" />
                </div>
              </div>
            </div>
          </div>

          </div>

          <div className="border-t pt-4 pb-2 flex flex-row justify-end gap-3 mt-auto">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting} className="bg-green-800 hover:bg-green-900 text-white">
              {isSubmitting ? "Creating..." : "Create Candidate"}
            </Button>
          </div>
        </form>
      </SheetContent>
    </Sheet>
  );
}
