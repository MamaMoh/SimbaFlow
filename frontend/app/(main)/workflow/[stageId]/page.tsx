"use client";

import { useParams } from "next/navigation";
import { usePermissions } from "@/lib/tenant/tenant-provider";

export default function WorkflowViewPage() {
  const { stageId } = useParams<{ stageId: string }>();
  const { hasPermission } = usePermissions();

  if (!hasPermission("workflow.view")) {
    return <div className="p-6">Access denied</div>;
  }

  return (
    <div className="flex flex-col gap-6 p-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Workflow View</h1>
        <span className="text-sm text-muted-foreground">Stage: {stageId}</span>
      </div>

      {/* Workflow view table with action buttons per candidate */}
      <div className="rounded-md border">
        <div className="p-8 text-center text-muted-foreground">
          Workflow view table — candidates in this stage with dynamic action buttons
        </div>
      </div>
    </div>
  );
}
