"use client";

import { useParams } from "next/navigation";
import useSWR from "swr";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { workflowApi, USE_MOCKS } from "@/lib/api/candidates-api";
import { WorkflowStageWorkbench } from "@/components/workflow/workflow-stage-workbench";
import { resolveStageSlug } from "@/lib/demo/demo-data";

export default function WorkflowViewPage() {
  const { stageId } = useParams<{ stageId: string }>();
  const { hasPermission } = usePermissions();
  const slug = resolveStageSlug(stageId);

  const { data, isLoading } = useSWR(
    stageId ? ["workflow-view", stageId] : null,
    () => workflowApi.stageView(stageId),
    { revalidateOnFocus: false },
  );

  if (!hasPermission("workflow.view") && !USE_MOCKS) {
    return <div className="p-6">Access denied</div>;
  }

  const view = data?.data;

  return (
    <div className="flex flex-col gap-2 p-4 md:p-6">
      <WorkflowStageWorkbench
        stageName={view?.stageName ?? slug}
        stageSlug={slug}
        rows={view?.items ?? []}
        isLoading={isLoading}
      />
    </div>
  );
}
