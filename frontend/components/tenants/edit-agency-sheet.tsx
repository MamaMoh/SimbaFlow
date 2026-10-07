"use client";

import { useEffect, useMemo } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import useSWR from "swr";
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
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from "@/components/ui/sheet";
import { Separator } from "@/components/ui/separator";
import { Building2, Pencil, User, Shield, Settings2 } from "lucide-react";
import { toast } from "sonner";
import { PhoneInputField } from "@/components/ui/phone-input";
import { AGENCY_LEVELS, DESTINATION_OPTIONS } from "@/lib/tenant/agency-levels";

const editAgencySchema = z
  .object({
    name: z.string().min(3, "Agency name must be at least 3 characters"),
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
    ownerFirstName: z.string().optional(),
    ownerLastName: z.string().optional(),
    ownerEmail: z.string().email("Valid email required").optional().or(z.literal("")),
    maxUsers: z.number().min(1).optional(),
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
    if (
      data.licenseIssuedAt &&
      data.licenseExpiresAt &&
      data.licenseExpiresAt < data.licenseIssuedAt
    ) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["licenseExpiresAt"],
        message: "Expiry must be on or after the issue date",
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
    fetcher,
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
    defaultValues: { agencyLevel: 5, licensedCountries: [] },
  });

  useEffect(() => {
    const a = data?.data;
    if (!a) return;
    reset({
      name: a.name || "",
      contactEmail: a.contactEmail || "",
      contactPhone: a.contactPhone || "",
      address: a.address || "",
      city: a.city || "",
      country: a.country || "Ethiopia",
      agencyLevel: a.agencyLevel ?? 5,
      licenseNumber: a.licenseNumber || "",
      licenseIssuedAt: a.licenseIssuedAt || "",
      licenseExpiresAt: a.licenseExpiresAt || "",
      licensedCountries: a.licensedCountries || [],
      ownerFirstName: a.ownerFirstName || "",
      ownerLastName: a.ownerLastName || "",
      ownerEmail: a.ownerEmail || "",
      maxUsers: a.maxUsers || 50,
    });
  }, [data, reset]);

  const agencyLevel = watch("agencyLevel");
  const licensedCountries = watch("licensedCountries") || [];
  const levelMeta = useMemo(
    () => AGENCY_LEVELS.find((l) => l.level === Number(agencyLevel)) ?? AGENCY_LEVELS[4],
    [agencyLevel],
  );

  const toggleCountry = (country: string, checked: boolean) => {
    const next = checked
      ? [...licensedCountries, country]
      : licensedCountries.filter((c) => c !== country);
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
          address: formData.address || "",
          city: formData.city || "",
          country: formData.country || "Ethiopia",
          agencyLevel: formData.agencyLevel,
          licenseNumber: formData.licenseNumber || "",
          // "" clears the date on the server; undefined would leave it as it was.
          licenseIssuedAt: formData.licenseIssuedAt || "",
          licenseExpiresAt: formData.licenseExpiresAt || "",
          licensedCountries: formData.licensedCountries,
          ownerFirstName: formData.ownerFirstName || null,
          ownerLastName: formData.ownerLastName || null,
          ownerEmail: formData.ownerEmail || null,
          maxUsers: formData.maxUsers,
        }),
      });
      const result = await res.json();
      if (result.isSuccess) {
        toast.success("Agency updated");
        onOpenChange(false);
        onUpdated();
      } else {
        toast.error(result.error || "Could not update the agency");
      }
    } catch {
      toast.error("Could not update the agency");
    }
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-[540px] sm:max-w-[540px] flex flex-col px-6">
        <SheetHeader className="pb-4">
          <SheetTitle className="flex items-center gap-2">
            <Pencil className="h-5 w-5 text-green-700" />
            Edit agency
          </SheetTitle>
          <SheetDescription>
            The URL identifier and schema cannot be changed after creation.
          </SheetDescription>
        </SheetHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col flex-1 overflow-hidden">
          <div className="flex-1 overflow-y-auto space-y-6 pr-1">
            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <Building2 className="h-4 w-4 text-green-700" />
                Agency details
              </h3>
              <div className="space-y-4">
                <div className="space-y-1.5">
                  <Label>
                    Agency name <span className="text-red-500">*</span>
                  </Label>
                  <Input placeholder="e.g. Ethio Star Labour Export" {...register("name")} />
                  {errors.name && (
                    <p className="text-xs text-destructive mt-1">{errors.name.message}</p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label>URL identifier</Label>
                  <Input value={data?.data?.slug || ""} disabled className="bg-muted" />
                  <p className="text-xs text-muted-foreground">
                    Names the database schema, so it cannot be changed later.
                  </p>
                </div>
                <div className="space-y-1.5">
                  <Label>
                    Contact email <span className="text-red-500">*</span>
                  </Label>
                  <Input type="email" placeholder="agency@example.com" {...register("contactEmail")} />
                  {errors.contactEmail && (
                    <p className="text-xs text-destructive mt-1">{errors.contactEmail.message}</p>
                  )}
                </div>
                <div className="space-y-1.5">
                  <Label>Contact phone</Label>
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
                  <Label>HQ address</Label>
                  <Input placeholder="Street / building" {...register("address")} />
                </div>
              </div>
            </div>

            <Separator />

            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-2">
                <Shield className="h-4 w-4 text-green-700" />
                Level & MoLS licence
              </h3>
              <p className="text-xs text-muted-foreground mb-4">
                Level sets how many foreign partners the agency may hold per country.
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
                        setValue("licensedCountries", licensedCountries.slice(0, caps.maxCountries), {
                          shouldValidate: true,
                        });
                      }
                    }}
                  >
                    <SelectTrigger>
                      <SelectValue placeholder="Select level" />
                    </SelectTrigger>
                    <SelectContent position="popper" className="z-[200]">
                      {AGENCY_LEVELS.map((l) => (
                        <SelectItem key={l.level} value={String(l.level)}>
                          Level {l.level} — ≤{l.maxPartnersPerCountry} per country,{" "}
                          {l.maxCountries == null
                            ? "any number of countries"
                            : `up to ${l.maxCountries} ${l.maxCountries === 1 ? "country" : "countries"}`}
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
                    {typeof data?.data?.activePartnerLinks === "number" && (
                      <> · {data.data.activePartnerLinks} linked today</>
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
                    {errors.licenseExpiresAt && (
                      <p className="text-xs text-destructive">{errors.licenseExpiresAt.message}</p>
                    )}
                  </div>
                </div>

                <div className="space-y-2">
                  <Label>Licensed destination countries</Label>
                  <div className="grid grid-cols-2 gap-2 rounded-md border p-3">
                    {DESTINATION_OPTIONS.map((country) => (
                      <label key={country} className="flex items-center gap-2 text-sm">
                        <Checkbox
                          checked={licensedCountries.includes(country)}
                          onCheckedChange={(v) => toggleCountry(country, v === true)}
                        />
                        {country}
                      </label>
                    ))}
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
                Agency owner account
              </h3>
              <div className="space-y-4">
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <Label>First name</Label>
                    <Input placeholder="First name" {...register("ownerFirstName")} />
                  </div>
                  <div className="space-y-1.5">
                    <Label>Last name</Label>
                    <Input placeholder="Last name" {...register("ownerLastName")} />
                  </div>
                </div>
                <div className="space-y-1.5">
                  <Label>Admin email</Label>
                  <Input type="email" placeholder="owner@example.com" {...register("ownerEmail")} />
                  {errors.ownerEmail ? (
                    <p className="text-xs text-destructive">{errors.ownerEmail.message}</p>
                  ) : (
                    <p className="text-xs text-muted-foreground">
                      Also their sign-in username, so changing it changes how they sign in.
                    </p>
                  )}
                </div>
              </div>
            </div>

            <Separator />

            <div>
              <h3 className="flex items-center gap-2 text-sm font-semibold mb-4">
                <Settings2 className="h-4 w-4 text-green-700" />
                Settings
              </h3>
              <div className="space-y-4">
                <div className="space-y-1.5">
                  <Label>Max users</Label>
                  <Input type="number" placeholder="50" {...register("maxUsers", { valueAsNumber: true })} />
                </div>
                <div className="space-y-1.5">
                  <Label>Schema name</Label>
                  <Input
                    value={data?.data?.schemaName || ""}
                    disabled
                    className="bg-muted font-mono text-sm"
                  />
                </div>
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
              {isSubmitting ? "Saving…" : "Save changes"}
            </Button>
          </div>
        </form>
      </SheetContent>
    </Sheet>
  );
}
