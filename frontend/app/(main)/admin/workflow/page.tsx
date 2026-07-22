"use client";

import workflowStagesJson from "@/mocks/workflow-stages.json";

export default function WorkflowConfigPage() {
  const stages = (workflowStagesJson as any).stages ?? [];

  return (
    <div className="flex flex-col gap-5 p-4 md:p-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Workflow configuration</h1>
        <p className="text-sm text-muted-foreground">Default seeded pipeline (read-only demo)</p>
      </div>
      <ol className="space-y-2">
        {stages.map((s: any, i: number) => (
          <li key={s.id} className="flex items-center gap-3 rounded-xl border bg-card px-4 py-3 shadow-sm">
            <span className="flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 text-sm font-bold text-primary">
              {i + 1}
            </span>
            <div>
              <div className="font-semibold">{s.name}</div>
              <div className="text-xs text-muted-foreground">
                Type {s.stageType === 1 ? "Parallel tracks" : s.stageType === 2 ? "Milestones" : "Simple"}
                {s.expectedDurationHours ? ` · SLA ${s.expectedDurationHours}h` : ""}
              </div>
            </div>
          </li>
        ))}
      </ol>
    </div>
  );
}
