"use client";

import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from "@/components/ui/sheet";
import { CountrySelect } from "@/components/ui/country-select";
import { toast } from "sonner";
import { useState } from "react";
import { PendingImagePicker, uploadBranding } from "@/components/branding/logo-upload";

const schema = z.object({
  name: z.string().min(2, "Name required"),
  countryCode: z.string().min(2, "Country required").max(8),
  countryName: z.string().min(2),
  foreignLicenseId: z.string().optional(),
  contactEmail: z.string().email("Enter a valid email").optional().or(z.literal("")),
  contactPhone: z.string().optional(),
  address: z.string().optional(),
});

type FormValues = z.infer<typeof schema>;

export function CreatePartnerSheet({
  open,
  onOpenChange,
  onCreated,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: () => void;
}) {
  const {
    register,
    handleSubmit,
    setValue,
    watch,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { countryCode: "SA", countryName: "Saudi Arabia" },
  });

  // Held until the partner exists — the upload endpoint needs its id.
  const [logo, setLogo] = useState<File | null>(null);
  const [letterhead, setLetterhead] = useState<File | null>(null);

  const onSubmit = async (data: FormValues) => {
    try {
      const res = await fetch("/api/proxy/partners", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: data.name,
          countryCode: data.countryCode,
          countryName: data.countryName,
          foreignLicenseId: data.foreignLicenseId || null,
          contactEmail: data.contactEmail || null,
          contactPhone: data.contactPhone || null,
          address: data.address || null,
        }),
      });
      const body = await res.json();
      if (!body.isSuccess) {
        toast.error(body.error || "Failed to create partner");
        return;
      }
      const partnerId: string | undefined = body.data;
      if (partnerId) {
        const results = await Promise.all([
          logo ? uploadBranding(`/api/proxy/branding/partners/${partnerId}/logo`, logo) : true,
          letterhead
            ? uploadBranding(`/api/proxy/branding/partners/${partnerId}/letterhead`, letterhead)
            : true,
        ]);
        // The partner is saved either way; say so plainly rather than implying it failed.
        if (results.some((ok) => !ok)) {
          toast.warning(
            `${data.name} was registered, but its images could not be saved. Add them from the partner list.`
          );
        } else {
          toast.success(`${data.name} registered`);
        }
      } else {
        toast.success(`${data.name} registered`);
      }

      reset();
      setLogo(null);
      setLetterhead(null);
      onOpenChange(false);
      onCreated();
    } catch {
      toast.error("Failed to create partner");
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-[480px] sm:max-w-[480px] flex flex-col px-6">
        <SheetHeader>
          <SheetTitle>Register partner agency</SheetTitle>
          <SheetDescription>
            A receiving-country agency your agency signs agreements with. Its letterhead is printed
            on the documents of every candidate you place through it.
          </SheetDescription>
        </SheetHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="mt-4 flex flex-1 flex-col gap-4 overflow-y-auto">
          <div className="space-y-1.5">
            <Label>Agency name *</Label>
            <Input {...register("name")} placeholder="e.g. Etenaa Resources" />
            {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
          </div>

          <div className="space-y-1.5">
            <Label>Country *</Label>
            <CountrySelect
              value={watch("countryCode")}
              onChange={(code, name) => {
                setValue("countryCode", code, { shouldValidate: true });
                setValue("countryName", name, { shouldValidate: true });
              }}
            />
            {errors.countryCode && (
              <p className="text-xs text-destructive">{errors.countryCode.message}</p>
            )}
          </div>

          <div className="space-y-1.5">
            <Label>Phone number</Label>
            <Input {...register("contactPhone")} placeholder="+966 11 234 5678" />
          </div>

          <div className="space-y-1.5">
            <Label>Contact email</Label>
            <Input type="email" {...register("contactEmail")} placeholder="office@partner.com" />
            {errors.contactEmail && (
              <p className="text-xs text-destructive">{errors.contactEmail.message}</p>
            )}
          </div>

          <div className="space-y-1.5">
            <Label>Address</Label>
            <Textarea {...register("address")} rows={2} placeholder="Street, city" />
          </div>

          <div className="space-y-1.5">
            <Label>Foreign license ID</Label>
            <Input {...register("foreignLicenseId")} placeholder="Optional" />
          </div>

          <div className="space-y-4 rounded-lg border bg-muted/20 p-3">
            <p className="text-xs font-medium text-foreground">Branding</p>
            <PendingImagePicker
              file={letterhead}
              onPick={setLetterhead}
              label="Letterhead"
              hint="The wide banner printed across the top of this partner's candidate documents."
            />
            <PendingImagePicker
              file={logo}
              onPick={setLogo}
              label="Logo"
              hint="The mark, shown in lists. Stands in on documents if there is no letterhead."
            />
          </div>

          <div className="mt-auto flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting} className="bg-green-800 text-white hover:bg-green-900">
              {isSubmitting ? "Saving…" : "Save"}
            </Button>
          </div>
        </form>
      </SheetContent>
    </Sheet>
  );
}
