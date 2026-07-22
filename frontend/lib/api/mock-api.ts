import {
  DEMO_OFFICES,
  getCandidateDetail,
  getCandidateList,
  getDemoTimeline,
  getStageView,
} from "@/lib/demo/demo-data";
import workflowStagesJson from "@/mocks/workflow-stages.json";
import type { WorkflowDefinition } from "@/types/workflow";

export const USE_MOCKS =
  process.env.NEXT_PUBLIC_USE_MOCKS === "true" ||
  process.env.NEXT_PUBLIC_USE_MOCKS === "1";

function wrap<T>(data: T, statusCode = 200) {
  return { isSuccess: true, data, statusCode, error: null as string | null };
}

export const mockApi = {
  async getCandidates(search?: string) {
    const items = getCandidateList(search);
    return wrap({ items, totalCount: items.length, page: 1, pageSize: 100 });
  },

  async getCandidateById(id: string) {
    const detail = getCandidateDetail(id);
    if (!detail) return { isSuccess: false, data: null, statusCode: 404, error: "Not found" };
    return wrap(detail);
  },

  async getTimeline(candidateId: string) {
    return wrap(getDemoTimeline(candidateId));
  },

  async getOffices() {
    return wrap(DEMO_OFFICES);
  },

  async getWorkflowDefinition() {
    return wrap(workflowStagesJson as WorkflowDefinition);
  },

  async getStageView(stageId: string) {
    return wrap(getStageView(stageId));
  },
};
