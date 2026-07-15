"use client";

import { useEffect } from "react";
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
import { Separator } from "@/components/ui/separator";
import { Pencil, Mail, MapPin, FileText } from "lucide-react";
import { CountrySelect } from "@/components/ui/country-select";
import { PhoneInputField } from "@/components/ui/phone-input";
import useSWR from "swr";
import { toast } from "sonner";

const editCandidateSchema = z.object({
  firstName: z.string().min(2),
  lastName: z.string().min(2),
  middleName: z.string().optional(),
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

type EditCandidateForm = z.infer<typeof editCandidateSchema>;

interface EditCandidateSheetProps {
  candidate: { id: string; fullName: string } | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onUpdated: () => void;
}

const fetcher = (url: string) => fetch(url).then(r => r.json());

export function EditCandidateSheet({ candidate, open, onOpenChange, onUpdated }: EditCandidateSheetProps) {
  const { data } = useSWR(
    candidate && open ? `/api/proxy/candidates/${candidate.id}` : null,
    fetcher
  );

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<EditCandidateForm>({
    resolver: zodResolver(editCandidateSchema),
  });

  // Pre-fill form when data loads
  useEffect(() => {
    if (data?.data) {
      const d = data.data;
      reset({
        firstName: d.firstName || "",
        lastName: d.lastName || "",
        middleName: d.middleName || "",
        nationality: d.nationality || "",
        phoneNumber: d.phoneNumber || "",
        email: d.email || "",
        address: d.address || "",
        city: d.city || "",
        country: d.country || "",
        labourId: d.labourId || "",
        countryOfTravel: d.countryOfTravel || "",
        officeName: d.officeName || "",
        contractDate: d.contractDate || "",
      });
    }
  }, [data, reset]);

  const onSubmit = async (formData: EditCandidateForm) => {
    if (!candidate) return;
    try {
      const response = await fetch(`/api/proxy/candidates/${candidate.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(formData),
      });
      const result = await response.json();
      if (result.isSuccess) {
        onOpenChange(false);
        onUpdated();
        toast.success("Candidate updated successfully");
      } else {
        toast.error(result.error || "Update failed");
      }
    } catch {
      toast.error("Update failed. Please try again.");
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-[540px] sm:max-w-[540px] overflow-y-auto px-6">
        <SheetHeader className="pb-4">
          <SheetTitle className="flex items-center gap-2">
            <Pencil className="h-5 w-5 text-green-700" />
            Edit Candidate
          </SheetTitle>
          <SheetDescription>
            Update candidate information for {candidate?.fullName}
          </SheetDescription>
        </SheetHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="mt-4 space-y-6">
          {/* Basic Information */}
          <div>
            <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
              <FileText className="h-4 w-4 text-green-700" />
              Basic Information
            </h3>
            <div className="space-y-4">
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <Label>First Name <span className="text-red-500">*</span></Label>
                  <Input placeholder="First name" {...register("firstName")} />
                  {errors.firstName && <p className="text-xs text-destructive mt-1">{errors.firstName.message}</p>}
                </div>
                <div className="space-y-1.5">
                  <Label>Last Name <span className="text-red-500">*</span></Label>
                  <Input placeholder="Last name" {...register("lastName")} />
                  {errors.lastName && <p className="text-xs text-destructive mt-1">{errors.lastName.message}</p>}
                </div>
              </div>
              <div className="space-y-1.5">
                <Label>Middle Name</Label>
                <Input placeholder="Middle name" {...register("middleName")} />
              </div>
              <div className="space-y-1.5">
                <Label>Nationality</Label>
                <CountrySelect value={watch("nationality") || ""} onChange={(val) => setValue("nationality", val)} placeholder="Select nationality" />
              </div>
              <div className="space-y-1.5">
                <Label>Labour ID</Label>
                <Input placeholder="Enter labour ID" {...register("labourId")} />
              </div>
            </div>
          </div>

          <Separator />

          {/* Contact */}
          <div>
            <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
              <Mail className="h-4 w-4 text-green-700" />
              Contact Information
            </h3>
            <div className="space-y-4">
              <div className="space-y-1.5">
                <Label>Email</Label>
                <Input type="email" placeholder="Enter email" {...register("email")} />
              </div>
              <div className="space-y-1.5">
                <Label>Phone Number</Label>
                <PhoneInputField value={watch("phoneNumber") || ""} onChange={(val) => setValue("phoneNumber", val)} />
              </div>
            </div>
          </div>

          <Separator />

          {/* Travel & Contract */}
          <div>
            <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
              <MapPin className="h-4 w-4 text-green-700" />
              Travel & Contract
            </h3>
            <div className="space-y-4">
              <div className="space-y-1.5">
                <Label>Country of Travel</Label>
                <CountrySelect value={watch("countryOfTravel") || ""} onChange={(val) => setValue("countryOfTravel", val)} placeholder="Select destination" />
              </div>
              <div className="space-y-1.5">
                <Label>Office / Employer</Label>
                <Input placeholder="Overseas office or employer" {...register("officeName")} />
              </div>
              <div className="space-y-1.5">
                <Label>Contract Date</Label>
                <Input type="date" {...register("contractDate")} />
              </div>
            </div>
          </div>

          <Separator />

          {/* Address */}
          <div>
            <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
              <MapPin className="h-4 w-4 text-green-700" />
              Address
            </h3>
            <div className="space-y-4">
              <div className="space-y-1.5">
                <Label>Street Address</Label>
                <Input placeholder="Street address" {...register("address")} />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <Label>City</Label>
                  <Input placeholder="City" {...register("city")} />
                </div>
                <div className="space-y-1.5">
                  <Label>Country</Label>
                  <CountrySelect value={watch("country") || ""} onChange={(val) => setValue("country", val)} placeholder="Country" />
                </div>
              </div>
            </div>
          </div>

          <SheetFooter className="sticky bottom-0 left-0 right-0 bg-background py-4 border-t flex flex-row justify-end gap-3 -mx-6 mt-6" style={{ paddingLeft: '1.5rem', paddingRight: '1.5rem' }}>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting} className="bg-green-800 hover:bg-green-900 text-white">
              {isSubmitting ? "Saving..." : "Save Changes"}
            </Button>
          </SheetFooter>
        </form>
      </SheetContent>
    </Sheet>
  );
}
