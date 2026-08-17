"use client";

import { useEffect, useMemo } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import useSWR from "swr";
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
import { Building2, Pencil, User, Key, Scale } from "lucide-react";
import { toast } from "sonner";
import { PhoneInputField } from "@/components/ui/phone-input";

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

const LICENSE_STATUSES = [
  { value: 0, label: "Pending" },
  { value: 1, label: "Active" },
  { value: 2, label: "Suspended" },
  { value: 3, label: "Revoked" },
  { value: 4, label: "Expired" },
] as const;

const editAgencySchema = z
  .object({
    name: z.string().min(3, "Agency name must be at least 3 characters"),
    contactEmail: z.string().email("Valid email required"),
    contactPhone: z.string().optional(),
    maxUsers: z.number().min(1).optional(),
    agencyLevel: z.coerce.number().int().min(1).max(5),
    licenseNumber: z.string().optional(),
    licenseIssuedAt: z.string().optional(),
    licenseExpiresAt: z.string().optional(),
    licenseStatus: z.coerce.number().int().min(0).max(4),
    licensedCountries: z.array(z.string()).default([]),
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

type EditAgencyForm = z.infer<typeof editAgencySchema>;

interface EditAgencySheetProps {
  agencyId: string | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onUpdated: () => void;
}

const fetcher = (url: string) => fetch(url).then((r) => r.json());

export function EditAgencySheet({
  agencyId,
  open,
  onOpenChange,
  onUpdated,
}: EditAgencySheetProps) {
  const { data } = useSWR(
    agencyId && open ? `/api/proxy/tenants/${agencyId}` : null,
    fetcher
  );

  const {
    register,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<EditAgencyForm>({
    resolver: zodResolver(editAgencySchema),
    defaultValues: {
      agencyLevel: 5,
      licenseStatus: 0,
      licensedCountries: [],
    },
  });

  const agencyLevel = watch("agencyLevel");
  const licenseStatus = watch("licenseStatus");
  const licensedCountries = watch("licensedCountries") || [];

  const levelMeta = useMemo(
    () => AGENCY_LEVELS.find((l) => l.level === Number(agencyLevel)) ?? AGENCY_LEVELS[4],
    [agencyLevel]
  );

  useEffect(() => {
    if (data?.data) {
      const t = data.data;
      reset({
        name: t.name || "",
        contactEmail: t.contactEmail || "",
        contactPhone: t.contactPhone || "",
        maxUsers: t.maxUsers || 50,
        agencyLevel: t.agencyLevel ?? 5,
        licenseNumber: t.licenseNumber || "",
        licenseIssuedAt: t.licenseIssuedAt || "",
        licenseExpiresAt: t.licenseExpiresAt || "",
        licenseStatus:
          typeof t.licenseStatus === "number" ? t.licenseStatus : 0,
        licensedCountries: Array.isArray(t.licensedCountries)
          ? t.licensedCountries
          : [],
      });
    }
  }, [data, reset]);

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

  const onSubmit = async (formData: EditAgencyForm) => {
    if (!agencyId) return;
    try {
      const res = await fetch(`/api/proxy/tenants/${agencyId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: formData.name,
          contactEmail: formData.contactEmail,
          contactPhone: formData.contactPhone || null,
          maxUsers: formData.maxUsers,
          agencyLevel: formData.agencyLevel,
          licenseNumber: formData.licenseNumber || null,
          licenseIssuedAt: formData.licenseIssuedAt || null,
          licenseExpiresAt: formData.licenseExpiresAt || null,
          licenseStatus: formData.licenseStatus,
          licensedCountries: formData.licensedCountries,
        }),
      });
      const result = await res.json();
      if (result.isSuccess) {
        toast.success("Agency updated successfully");
        onOpenChange(false);
        onUpdated();
      } else {
        toast.error(result.error || "Failed to update agency");
      }
    } catch {
      toast.error("Failed to update agency");
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-[560px] sm:max-w-[560px] flex flex-col px-6">
        <SheetHeader className="pb-4">
          <SheetTitle className="flex items-center gap-2">
            <Pencil className="h-5 w-5 text-green-700" />
            Edit Agency
          </SheetTitle>
          <SheetDescription>
            Update agency details and MoLS license. Slug and schema cannot be changed.
          </SheetDescription>
        </SheetHeader>

        <form
          onSubmit={handleSubmit(onSubmit)}
          className="flex flex-col flex-1 overflow-hidden"
        >
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
                    {...register("name")}
                  />
                  {errors.name && (
                    <p className="text-xs text-destructive mt-1">
                      {errors.name.message}
                    </p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label>Slug (URL identifier)</Label>
                  <Input
                    value={data?.data?.slug || ""}
                    disabled
                    className="bg-muted"
                  />
                  <p className="text-xs text-muted-foreground">
                    Used for database schema. Cannot be changed later.
                  </p>
                </div>
                <div className="space-y-1.5">
                  <Label>
                    Contact Email <span className="text-red-500">*</span>
                  </Label>
                  <Input
                    type="email"
                    placeholder="agency@example.com"
                    {...register("contactEmail")}
                  />
                  {errors.contactEmail && (
                    <p className="text-xs text-destructive mt-1">
                      {errors.contactEmail.message}
                    </p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label>Contact Phone</Label>
                  <PhoneInputField
                    value={watch("contactPhone") || ""}
                    onChange={(val) => setValue("contactPhone", val)}
                  />
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
                Level sets foreign partner caps per country (Directive 1126/2018).
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
                    ≤ <strong>{levelMeta.maxPartnersPerCountry}</strong> foreign
                    partners per destination country
                    {levelMeta.maxCountries == null ? (
                      <> · unlimited countries</>
                    ) : (
                      <>
                        {" "}
                        · up to <strong>{levelMeta.maxCountries}</strong> licensed
                        countries
                      </>
                    )}
                  </div>
                </div>

                <div className="space-y-1.5">
                  <Label>License number</Label>
                  <Input
                    placeholder="MoLS license #"
                    {...register("licenseNumber")}
                  />
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

                <div className="space-y-1.5">
                  <Label>License status</Label>
                  <Select
                    value={String(licenseStatus)}
                    onValueChange={(v) =>
                      setValue("licenseStatus", Number(v), { shouldValidate: true })
                    }
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Status" />
                    </SelectTrigger>
                    <SelectContent position="popper" className="z-[200]">
                      {LICENSE_STATUSES.map((s) => (
                        <SelectItem key={s.value} value={String(s.value)}>
                          {s.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2">
                  <Label>Licensed destination countries</Label>
                  <div className="grid grid-cols-2 gap-2 rounded-md border p-3">
                    {DESTINATION_OPTIONS.map((country) => {
                      const checked = licensedCountries.includes(country);
                      return (
                        <label
                          key={country}
                          className="flex items-center gap-2 text-sm"
                        >
                          <Checkbox
                            checked={checked}
                            onCheckedChange={(v) =>
                              toggleCountry(country, v === true)
                            }
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
              <p className="text-xs text-muted-foreground mb-4">
                The agency administrator. To change, use the Staff management page.
              </p>
              <div className="space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <Label>First Name</Label>
                    <Input
                      value={data?.data?.ownerFirstName || "—"}
                      disabled
                      className="bg-muted"
                    />
                  </div>
                  <div className="space-y-1.5">
                    <Label>Last Name</Label>
                    <Input
                      value={data?.data?.ownerLastName || "—"}
                      disabled
                      className="bg-muted"
                    />
                  </div>
                </div>
                <div className="space-y-1.5">
                  <Label>Admin Email</Label>
                  <Input
                    value={data?.data?.ownerEmail || "—"}
                    disabled
                    className="bg-muted"
                  />
                  <p className="text-xs text-muted-foreground">
                    This is their login username.
                  </p>
                </div>
              </div>
            </div>

            <Separator />

            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <Key className="h-4 w-4 text-green-700" />
                Settings
              </h3>
              <div className="space-y-4">
                <div className="space-y-1.5">
                  <Label>Max Users</Label>
                  <Input
                    type="number"
                    placeholder="50"
                    {...register("maxUsers", { valueAsNumber: true })}
                  />
                  <p className="text-xs text-muted-foreground">
                    Maximum number of user accounts for this agency.
                  </p>
                </div>
                <div className="space-y-1.5">
                  <Label>Schema Name</Label>
                  <Input
                    value={data?.data?.schemaName || ""}
                    disabled
                    className="bg-muted font-mono text-sm"
                  />
                  <p className="text-xs text-muted-foreground">
                    Database schema. Cannot be changed.
                  </p>
                </div>
              </div>
            </div>
          </div>

          <div className="border-t pt-4 pb-2 flex flex-row justify-end gap-3 mt-auto">
            <Button
              type="button"
              variant="outline"
              onClick={() => onOpenChange(false)}
            >
              Cancel
            </Button>
            <Button
              type="submit"
              disabled={isSubmitting}
              className="bg-green-800 hover:bg-green-900 text-white"
            >
              {isSubmitting ? "Saving..." : "Save Changes"}
            </Button>
          </div>
        </form>
      </SheetContent>
    </Sheet>
  );
}
