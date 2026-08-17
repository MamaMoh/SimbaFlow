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
} from "@/components/ui/sheet";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { toast } from "sonner";

const schema = z.object({
  name: z.string().min(2, "Name required"),
  countryCode: z.string().min(2).max(8),
  countryName: z.string().min(2),
  capacityTier: z.coerce.number().int().min(0).max(2),
  foreignLicenseId: z.string().optional(),
  contactEmail: z.string().email().optional().or(z.literal("")),
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
    defaultValues: { capacityTier: 1, countryCode: "SA", countryName: "Saudi Arabia" },
  });

  const onSubmit = async (data: FormValues) => {
    try {
      const res = await fetch("/api/proxy/partners", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: data.name,
          countryCode: data.countryCode,
          countryName: data.countryName,
          capacityTier: data.capacityTier,
          foreignLicenseId: data.foreignLicenseId || null,
          contactEmail: data.contactEmail || null,
        }),
      });
      const body = await res.json();
      if (!body.isSuccess) {
        toast.error(body.error || "Failed to create partner");
        return;
      }
      toast.success("Partner added to catalog");
      reset();
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
          <SheetTitle>Add foreign partner</SheetTitle>
          <SheetDescription>
            Platform catalog entry for an Arab / receiving-country agency (Art. 40 capacity).
          </SheetDescription>
        </SheetHeader>
        <form onSubmit={handleSubmit(onSubmit)} className="mt-4 flex flex-1 flex-col gap-4">
          <div className="space-y-1.5">
            <Label>Agency name *</Label>
            <Input {...register("name")} placeholder="e.g. Etenaa Resources" />
            {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>Country code *</Label>
              <Input {...register("countryCode")} placeholder="SA" />
            </div>
            <div className="space-y-1.5">
              <Label>Country name *</Label>
              <Input {...register("countryName")} placeholder="Saudi Arabia" />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label>Art. 40 capacity</Label>
            <Select
              value={String(watch("capacityTier"))}
              onValueChange={(v) => setValue("capacityTier", Number(v))}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="0">Low (≤2 Ethiopian agencies)</SelectItem>
                <SelectItem value="1">Medium (≤4)</SelectItem>
                <SelectItem value="2">High (≤8)</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label>Foreign license ID</Label>
            <Input {...register("foreignLicenseId")} />
          </div>
          <div className="space-y-1.5">
            <Label>Contact email</Label>
            <Input type="email" {...register("contactEmail")} />
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
