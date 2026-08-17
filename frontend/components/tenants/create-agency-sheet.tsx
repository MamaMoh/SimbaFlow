"use client";

import { useMemo } from "react";
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
} from "@/components/ui/sheet";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import { Checkbox } from "@/components/ui/checkbox";
import { Building2, User, Key, Scale } from "lucide-react";
import { toast } from "sonner";
import { PhoneInputField } from "@/components/ui/phone-input";

/** MoLS Directive 1126/2018 Arts. 18–22 */
const AGENCY_LEVELS = [
  { level: 1, maxPartnersPerCountry: 20, maxCountries: null as number | null, label: "Level 1 — All occupations" },
  { level: 2, maxPartnersPerCountry: 20, maxCountries: 8, label: "Level 2 — Domestic / labour / skilled" },
  { level: 3, maxPartnersPerCountry: 16, maxCountries: 8, label: "Level 3 — Domestic + labour" },
  { level: 4, maxPartnersPerCountry: 8, maxCountries: 4, label: "Level 4 — Domestic + labour" },
  { level: 5, maxPartnersPerCountry: 4, maxCountries: 2, label: "Level 5 — Domestic only" },
] as const;

const DESTINATION_OPTIONS = [
  "Saudi Arabia",
  "United Arab Emirates",
  "Kuwait",
  "Qatar",
  "Bahrain",
  "Oman",
  "Jordan",
  "Lebanon",
] as const;

const createAgencySchema = z
  .object({
    agencyName: z.string().min(3, "Agency name must be at least 3 characters"),
    slug: z
      .string()
      .min(3)
      .max(50)
      .regex(/^[a-z][a-z0-9-]*[a-z0-9]$/, "Lowercase, alphanumeric with hyphens"),
    contactEmail: z.string().email("Valid email required"),
    contactPhone: z.string().optional(),
    address: z.string().optional(),
    city: z.string().optional(),
    country: z.string().optional(),
    agencyLevel: z.coerce.number().int().min(1).max(5),
    licenseNumber: z.string().optional(),
    licenseIssuedAt: z.string().optional(),
    licenseExpiresAt: z.string().optional(),
    licensedCountries: z.array(z.string()).default([]),
    adminFirstName: z.string().min(2, "First name required"),
    adminLastName: z.string().min(2, "Last name required"),
    adminEmail: z.string().email("Valid admin email required"),
    temporaryPassword: z.string().min(8, "Password must be at least 8 characters"),
  })
  .superRefine((data, ctx) => {
    const caps = AGENCY_LEVELS.find((l) => l.level === data.agencyLevel);
    if (caps?.maxCountries != null && data.licensedCountries.length > caps.maxCountries) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["licensedCountries"],
        message: `Level ${data.agencyLevel} may license at most ${caps.maxCountries} destination countries`,
      });
    }
  });

type CreateAgencyForm = z.infer<typeof createAgencySchema>;

interface CreateAgencySheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onCreated: () => void;
}

export function CreateAgencySheet({ open, onOpenChange, onCreated }: CreateAgencySheetProps) {
  const {
    register,
    handleSubmit,
    watch,
    setValue,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<CreateAgencyForm>({
    resolver: zodResolver(createAgencySchema),
    defaultValues: {
      temporaryPassword: "Welcome@123!",
      agencyLevel: 5,
      licensedCountries: [],
      country: "Ethiopia",
    },
  });

  const agencyName = watch("agencyName");
  const agencyLevel = watch("agencyLevel");
  const licensedCountries = watch("licensedCountries") || [];

  const levelMeta = useMemo(
    () => AGENCY_LEVELS.find((l) => l.level === Number(agencyLevel)) ?? AGENCY_LEVELS[4],
    [agencyLevel]
  );

  const generateSlug = () => {
    if (agencyName) {
      const slug = agencyName
        .toLowerCase()
        .replace(/[^a-z0-9\s-]/g, "")
        .replace(/\s+/g, "-")
        .replace(/-+/g, "-")
        .replace(/^-|-$/g, "");
      setValue("slug", slug);
    }
  };

  const toggleCountry = (country: string, checked: boolean) => {
    const next = checked
      ? [...licensedCountries, country]
      : licensedCountries.filter((c) => c !== country);
    if (
      checked &&
      levelMeta.maxCountries != null &&
      next.length > levelMeta.maxCountries
    ) {
      toast.error(
        `Level ${levelMeta.level} allows at most ${levelMeta.maxCountries} destination countries`
      );
      return;
    }
    setValue("licensedCountries", next, { shouldValidate: true });
  };

  const onSubmit = async (data: CreateAgencyForm) => {
    try {
      const response = await fetch("/api/proxy/tenants", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          agencyName: data.agencyName,
          slug: data.slug,
          contactEmail: data.contactEmail,
          contactPhone: data.contactPhone || null,
          address: data.address || null,
          city: data.city || null,
          country: data.country || "Ethiopia",
          agencyLevel: data.agencyLevel,
          licenseNumber: data.licenseNumber || null,
          licenseIssuedAt: data.licenseIssuedAt || null,
          licenseExpiresAt: data.licenseExpiresAt || null,
          licensedCountries: data.licensedCountries,
          adminFirstName: data.adminFirstName,
          adminLastName: data.adminLastName,
          adminEmail: data.adminEmail,
          temporaryPassword: data.temporaryPassword,
        }),
      });
      const result = await response.json();
      if (result.isSuccess) {
        toast.success(`Agency "${data.agencyName}" created (Level ${data.agencyLevel})`);
        reset();
        onOpenChange(false);
        onCreated();
      } else {
        toast.error(result.error || "Failed to create agency");
      }
    } catch {
      toast.error("Failed to create agency");
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-[560px] sm:max-w-[560px] flex flex-col px-6">
        <SheetHeader className="pb-4">
          <SheetTitle className="flex items-center gap-2">
            <Building2 className="h-5 w-5 text-green-700" />
            Create New Agency
          </SheetTitle>
          <SheetDescription>
            Register a labour export agency with MoLS level, destination countries, and owner
            account.
          </SheetDescription>
        </SheetHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col flex-1 overflow-hidden">
          <div className="flex-1 overflow-y-auto space-y-6 pr-1">
            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <Building2 className="h-4 w-4 text-green-700" />
                Agency Details
              </h3>
              <div className="space-y-4">
                <div className="space-y-1.5">
                  <Label>
                    Agency Name <span className="text-red-500">*</span>
                  </Label>
                  <Input
                    placeholder="e.g. Ethio Star Labour Export"
                    {...register("agencyName")}
                    onBlur={generateSlug}
                  />
                  {errors.agencyName && (
                    <p className="text-xs text-destructive mt-1">{errors.agencyName.message}</p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label>
                    Slug (URL identifier) <span className="text-red-500">*</span>
                  </Label>
                  <Input placeholder="e.g. ethio-star" {...register("slug")} />
                  {errors.slug && (
                    <p className="text-xs text-destructive mt-1">{errors.slug.message}</p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label>
                    Contact Email <span className="text-red-500">*</span>
                  </Label>
                  <Input type="email" placeholder="agency@example.com" {...register("contactEmail")} />
                  {errors.contactEmail && (
                    <p className="text-xs text-destructive mt-1">{errors.contactEmail.message}</p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label>Contact Phone</Label>
                  <PhoneInputField
                    value={watch("contactPhone") || ""}
                    onChange={(val) => setValue("contactPhone", val)}
                  />
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1.5">
                    <Label>City</Label>
                    <Input placeholder="Addis Ababa" {...register("city")} />
                  </div>
                  <div className="space-y-1.5">
                    <Label>Country</Label>
                    <Input {...register("country")} />
                  </div>
                </div>
                <div className="space-y-1.5">
                  <Label>HQ Address</Label>
                  <Input placeholder="Street / building" {...register("address")} />
                </div>
              </div>
            </div>

            <Separator />

            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-2">
                <Scale className="h-4 w-4 text-green-700" />
                MoLS license &amp; level
              </h3>
              <p className="text-xs text-muted-foreground mb-4">
                Level (ደረጃ) sets how many foreign partner agencies the tenant may link per country
                (Directive 1126/2018 Arts. 18–22).
              </p>
              <div className="space-y-4">
                <div className="space-y-1.5">
                  <Label>
                    Agency level <span className="text-red-500">*</span>
                  </Label>
                  <Select
                    value={String(agencyLevel)}
                    onValueChange={(v) => {
                      const level = Number(v);
                      setValue("agencyLevel", level, { shouldValidate: true });
                      const caps = AGENCY_LEVELS.find((l) => l.level === level);
                      if (
                        caps?.maxCountries != null &&
                        licensedCountries.length > caps.maxCountries
                      ) {
                        setValue(
                          "licensedCountries",
                          licensedCountries.slice(0, caps.maxCountries),
                          { shouldValidate: true }
                        );
                      }
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select level" />
                    </SelectTrigger>
                    <SelectContent position="popper" className="z-[200]">
                      {AGENCY_LEVELS.map((l) => (
                        <SelectItem key={l.level} value={String(l.level)}>
                          {l.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <div className="rounded-md border border-emerald-100 bg-emerald-50/60 px-3 py-2 text-xs text-emerald-950">
                    ≤ <strong>{levelMeta.maxPartnersPerCountry}</strong> foreign partners per
                    destination country
                    {levelMeta.maxCountries == null ? (
                      <> · unlimited countries</>
                    ) : (
                      <>
                        {" "}
                        · up to <strong>{levelMeta.maxCountries}</strong> licensed countries
                      </>
                    )}
                  </div>
                </div>

                <div className="space-y-1.5">
                  <Label>License number</Label>
                  <Input placeholder="MoLS license #" {...register("licenseNumber")} />
                </div>
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1.5">
                    <Label>License issued</Label>
                    <Input type="date" {...register("licenseIssuedAt")} />
                  </div>
                  <div className="space-y-1.5">
                    <Label>License expires</Label>
                    <Input type="date" {...register("licenseExpiresAt")} />
                  </div>
                </div>

                <div className="space-y-2">
                  <Label>Licensed destination countries</Label>
                  <div className="grid grid-cols-2 gap-2 rounded-md border p-3">
                    {DESTINATION_OPTIONS.map((country) => {
                      const checked = licensedCountries.includes(country);
                      return (
                        <label key={country} className="flex items-center gap-2 text-sm">
                          <Checkbox
                            checked={checked}
                            onCheckedChange={(v) => toggleCountry(country, v === true)}
                          />
                          {country}
                        </label>
                      );
                    })}
                  </div>
                  {errors.licensedCountries && (
                    <p className="text-xs text-destructive">
                      {errors.licensedCountries.message as string}
                    </p>
                  )}
                </div>
              </div>
            </div>

            <Separator />

            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <User className="h-4 w-4 text-green-700" />
                Agency Owner Account
              </h3>
              <div className="space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <Label>
                      First Name <span className="text-red-500">*</span>
                    </Label>
                    <Input placeholder="First name" {...register("adminFirstName")} />
                    {errors.adminFirstName && (
                      <p className="text-xs text-destructive mt-1">
                        {errors.adminFirstName.message}
                      </p>
                    )}
                  </div>
                  <div className="space-y-1.5">
                    <Label>
                      Last Name <span className="text-red-500">*</span>
                    </Label>
                    <Input placeholder="Last name" {...register("adminLastName")} />
                    {errors.adminLastName && (
                      <p className="text-xs text-destructive mt-1">
                        {errors.adminLastName.message}
                      </p>
                    )}
                  </div>
                </div>
                <div className="space-y-1.5">
                  <Label>
                    Admin Email <span className="text-red-500">*</span>
                  </Label>
                  <Input type="email" placeholder="owner@agency.com" {...register("adminEmail")} />
                  {errors.adminEmail && (
                    <p className="text-xs text-destructive mt-1">{errors.adminEmail.message}</p>
                  )}
                </div>
              </div>
            </div>

            <Separator />

            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <Key className="h-4 w-4 text-green-700" />
                Initial Password
              </h3>
              <div className="space-y-1.5">
                <Label>
                  Temporary Password <span className="text-red-500">*</span>
                </Label>
                <Input type="password" {...register("temporaryPassword")} />
                {errors.temporaryPassword && (
                  <p className="text-xs text-destructive mt-1">
                    {errors.temporaryPassword.message}
                  </p>
                )}
                <p className="text-xs text-muted-foreground">
                  The agency owner will be forced to change this on first login.
                </p>
              </div>
            </div>
          </div>

          <div className="border-t pt-4 pb-2 flex flex-row justify-end gap-3 mt-auto">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button
              type="submit"
              disabled={isSubmitting}
              className="bg-green-800 hover:bg-green-900 text-white"
            >
              {isSubmitting ? "Creating..." : "Create Agency"}
            </Button>
          </div>
        </form>
      </SheetContent>
    </Sheet>
  );
}
