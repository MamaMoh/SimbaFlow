"use client";

import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { toast } from "sonner";
import { GitBranch, Plus, Trash2 } from "lucide-react";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Separator } from "@/components/ui/separator";
import type { ParallelTrackDefinition, WorkflowStage, WorkflowStageStatus } from "@/types/workflow";
import { upsertStage } from "@/lib/demo/workflow-config-store";

const schema = z.object({
  name: z.string().min(2, "Stage name is required"),
  description: z.string().optional(),
  stageType: z.number(),
  expectedDurationHours: z.coerce.number().min(0).optional(),
  warningDurationHours: z.coerce.number().min(0).optional(),
  criticalDurationHours: z.coerce.number().min(0).optional(),
  isInitialStage: z.boolean(),
  isFinalStage: z.boolean(),
});

type FormValues = z.infer<typeof schema>;

const STAGE_TYPES = [
  { value: 0, label: "Simple" },
  { value: 1, label: "Parallel tracks" },
  { value: 2, label: "Milestones" },
];

export function StageFormSheet({
  open,
  onOpenChange,
  stage,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  stage?: WorkflowStage | null;
  onSaved: () => void;
}) {
  const isEdit = !!stage;
  const [statuses, setStatuses] = useState<WorkflowStageStatus[]>([]);
  const [tracks, setTracks] = useState<ParallelTrackDefinition[]>([]);

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: "",
      description: "",
      stageType: 0,
      expectedDurationHours: 72,
      warningDurationHours: 48,
      criticalDurationHours: 72,
      isInitialStage: false,
      isFinalStage: false,
    },
  });

  const stageType = watch("stageType");
  const isInitial = watch("isInitialStage");
  const isFinal = watch("isFinalStage");

  useEffect(() => {
    if (!open) return;
    if (stage) {
      reset({
        name: stage.name,
        description: stage.description ?? "",
        stageType: stage.stageType,
        expectedDurationHours: stage.expectedDurationHours,
        warningDurationHours: stage.warningDurationHours,
        criticalDurationHours: stage.criticalDurationHours,
        isInitialStage: stage.isInitialStage,
        isFinalStage: stage.isFinalStage,
      });
      setStatuses(stage.statuses ?? []);
      setTracks(stage.parallelTracks ?? []);
    } else {
      reset({
        name: "",
        description: "",
        stageType: 0,
        expectedDurationHours: 72,
        warningDurationHours: 48,
        criticalDurationHours: 72,
        isInitialStage: false,
        isFinalStage: false,
      });
      setStatuses([]);
      setTracks([]);
    }
  }, [open, stage, reset]);

  const onSubmit = async (data: FormValues) => {
    await new Promise((r) => setTimeout(r, 250));
    upsertStage({
      id: stage?.id,
      ...data,
      statuses,
      parallelTracks: data.stageType === 1 ? tracks : [],
    });
    toast.success(isEdit ? "Stage updated" : "Stage created");
    onOpenChange(false);
    onSaved();
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="flex w-full flex-col px-6 sm:max-w-[560px]">
        <SheetHeader className="pb-2">
          <SheetTitle className="flex items-center gap-2">
            <GitBranch className="h-5 w-5 text-primary" />
            {isEdit ? "Edit stage" : "Add stage"}
          </SheetTitle>
          <SheetDescription>
            Configure stage type, SLA timing, statuses, and parallel tracks.
          </SheetDescription>
        </SheetHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-1 flex-col gap-4 overflow-y-auto pb-6">
          <div className="space-y-2">
            <Label htmlFor="name">Stage name</Label>
            <Input id="name" placeholder="e.g. Embassy" {...register("name")} />
            {errors.name && <p className="text-xs text-destructive">{errors.name.message}</p>}
          </div>

          <div className="space-y-2">
            <Label htmlFor="description">Description</Label>
            <Textarea id="description" rows={2} placeholder="What happens in this stage" {...register("description")} />
          </div>

          <div className="space-y-2">
            <Label>Stage type</Label>
            <Select
              value={String(stageType)}
              onValueChange={(v) => setValue("stageType", Number(v), { shouldValidate: true })}
            >
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {STAGE_TYPES.map((t) => (
                  <SelectItem key={t.value} value={String(t.value)}>
                    {t.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="grid grid-cols-3 gap-3">
            <div className="space-y-2">
              <Label>SLA (h)</Label>
              <Input type="number" {...register("expectedDurationHours")} />
            </div>
            <div className="space-y-2">
              <Label>Warning (h)</Label>
              <Input type="number" {...register("warningDurationHours")} />
            </div>
            <div className="space-y-2">
              <Label>Critical (h)</Label>
              <Input type="number" {...register("criticalDurationHours")} />
            </div>
          </div>

          <div className="flex items-center justify-between rounded-lg border px-3 py-2">
            <div>
              <div className="text-sm font-medium">Initial stage</div>
              <div className="text-xs text-muted-foreground">Candidates enter here</div>
            </div>
            <Switch checked={isInitial} onCheckedChange={(v) => setValue("isInitialStage", v)} />
          </div>
          <div className="flex items-center justify-between rounded-lg border px-3 py-2">
            <div>
              <div className="text-sm font-medium">Final stage</div>
              <div className="text-xs text-muted-foreground">Pipeline ends here</div>
            </div>
            <Switch checked={isFinal} onCheckedChange={(v) => setValue("isFinalStage", v)} />
          </div>

          <Separator />

          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <Label>Statuses</Label>
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="h-7 gap-1"
                onClick={() =>
                  setStatuses((prev) => [
                    ...prev,
                    {
                      id: `st-${Date.now()}`,
                      name: "",
                      sortOrder: prev.length + 1,
                      isTerminal: false,
                      color: "#1f6fb2",
                    },
                  ])
                }
              >
                <Plus className="h-3.5 w-3.5" /> Add
              </Button>
            </div>
            <div className="space-y-2">
              {statuses.map((s, i) => (
                <div key={s.id} className="flex items-center gap-2">
                  <Input
                    placeholder="Status name"
                    value={s.name}
                    onChange={(e) =>
                      setStatuses((prev) => prev.map((x, idx) => (idx === i ? { ...x, name: e.target.value } : x)))
                    }
                  />
                  <Input
                    placeholder="Track"
                    className="w-28"
                    value={s.trackName ?? ""}
                    onChange={(e) =>
                      setStatuses((prev) =>
                        prev.map((x, idx) => (idx === i ? { ...x, trackName: e.target.value || undefined } : x)),
                      )
                    }
                  />
                  <label className="flex items-center gap-1 text-[11px] text-muted-foreground whitespace-nowrap">
                    <input
                      type="checkbox"
                      checked={s.isTerminal}
                      onChange={(e) =>
                        setStatuses((prev) =>
                          prev.map((x, idx) => (idx === i ? { ...x, isTerminal: e.target.checked } : x)),
                        )
                      }
                    />
                    Terminal
                  </label>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="h-8 w-8 text-destructive"
                    onClick={() => setStatuses((prev) => prev.filter((_, idx) => idx !== i))}
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </Button>
                </div>
              ))}
              {!statuses.length && <p className="text-xs text-muted-foreground">No statuses yet</p>}
            </div>
          </div>

          {stageType === 1 && (
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <Label>Parallel tracks</Label>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="h-7 gap-1"
                  onClick={() =>
                    setTracks((prev) => [
                      ...prev,
                      {
                        id: `tr-${Date.now()}`,
                        trackName: "",
                        completionStatus: "",
                        sortOrder: prev.length + 1,
                      },
                    ])
                  }
                >
                  <Plus className="h-3.5 w-3.5" /> Add track
                </Button>
              </div>
              {tracks.map((t, i) => (
                <div key={t.id} className="flex items-center gap-2">
                  <Input
                    placeholder="Track name"
                    value={t.trackName}
                    onChange={(e) =>
                      setTracks((prev) => prev.map((x, idx) => (idx === i ? { ...x, trackName: e.target.value } : x)))
                    }
                  />
                  <Input
                    placeholder="Done when"
                    value={t.completionStatus}
                    onChange={(e) =>
                      setTracks((prev) =>
                        prev.map((x, idx) => (idx === i ? { ...x, completionStatus: e.target.value } : x)),
                      )
                    }
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="h-8 w-8 text-destructive"
                    onClick={() => setTracks((prev) => prev.filter((_, idx) => idx !== i))}
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </Button>
                </div>
              ))}
            </div>
          )}

          <div className="mt-auto flex gap-2 pt-4">
            <Button type="button" variant="outline" className="flex-1" onClick={() => onOpenChange(false)}>
              Cancel
            </Button>
            <Button type="submit" className="flex-1" disabled={isSubmitting}>
              {isSubmitting ? "Saving…" : isEdit ? "Save changes" : "Create stage"}
            </Button>
          </div>
        </form>
      </SheetContent>
    </Sheet>
  );
}
