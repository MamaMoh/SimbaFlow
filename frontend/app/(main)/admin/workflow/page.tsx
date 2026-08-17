"use client";

import { WorkflowConfigEditor } from "@/components/workflow/workflow-config-editor";

export default function AdminWorkflowPage() {
  return (
    <div className="flex flex-col gap-6">
      <WorkflowConfigEditor />
    </div>
  );
}
