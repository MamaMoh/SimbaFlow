"use client";

import { useParams } from "next/navigation";
import useSWR from "swr";
import { toast } from "sonner";
import { usePermissions } from "@/lib/tenant/tenant-provider";
import { candidatesApi, workflowApi, USE_MOCKS } from "@/lib/api/candidates-api";
import { WorkflowStageWorkbench } from "@/components/workflow/workflow-stage-workbench";
import { resolveStageSlug } from "@/lib/demo/demo-data";

export default function WorkflowViewPage() {
  const { stageId } = useParams<{ stageId: string }>();
  const { hasPermission } = usePermissions();
  const slug = resolveStageSlug(stageId);

  const { data, isLoading, mutate } = useSWR(
    stageId ? ["workflow-view", stageId] : null,
    () => workflowApi.stageView(stageId),
    { revalidateOnFocus: false },
  );

  if (!hasPermission("workflow.view") && !USE_MOCKS) {
    return <div className="p-6">Access denied</div>;
  }

  const view = data?.data;

  const handleAction = async (candidateId: string, actionId: string) => {
    const result = await candidatesApi.applyAction(candidateId, actionId);
    if (result.isSuccess) {
      toast.success((result.data as any)?.message ?? "Action applied");
      mutate();
    } else {
      toast.error(result.error || "Action failed");
    }
  };

  return (
    <div className="flex flex-col gap-2 p-4 md:p-6">
      <WorkflowStageWorkbench
        stageName={view?.stageName ?? slug}
        stageSlug={slug}
        rows={view?.items ?? []}
        isLoading={isLoading}
        onAction={handleAction}
      />
    </div>
  );
}
