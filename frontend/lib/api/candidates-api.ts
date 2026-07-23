import { apiClient } from "@/lib/api/client";
import { mockApi, USE_MOCKS } from "@/lib/api/mock-api";
import type { CandidateDetail, CandidateListItem, TimelineItem } from "@/types/candidate";
import type { Office, WorkflowDefinition, WorkflowViewRow } from "@/types/workflow";

type ApiResult<T> = {
  isSuccess: boolean;
  data: T;
  statusCode: number;
  error?: string | null;
};

export const candidatesApi = {
  async list(search?: string): Promise<ApiResult<{ items: CandidateListItem[]; totalCount: number }>> {
    if (USE_MOCKS) return mockApi.getCandidates(search) as never;
    const qs = search ? `?search=${encodeURIComponent(search)}&page=1&pageSize=100` : "?page=1&pageSize=100";
    return apiClient.get(`/candidates${qs}`);
  },

  async getById(id: string): Promise<ApiResult<CandidateDetail>> {
    if (USE_MOCKS) return mockApi.getCandidateById(id) as never;
    return apiClient.get(`/candidates/${id}`);
  },

  async timeline(id: string): Promise<ApiResult<TimelineItem[]>> {
    if (USE_MOCKS) return mockApi.getTimeline(id) as never;
    return apiClient.get(`/candidates/${id}/timeline`);
  },

  async register(body: Record<string, unknown>): Promise<ApiResult<string>> {
    if (USE_MOCKS) return mockApi.registerCandidate(body) as never;
    return apiClient.post("/candidates", body);
  },

  async applyAction(candidateId: string, actionId: string): Promise<ApiResult<{ message: string }>> {
    if (USE_MOCKS) return mockApi.applyCandidateAction(candidateId, actionId) as never;
    return apiClient.post(`/candidates/${candidateId}/actions`, { actionId });
  },

  async update(id: string, body: Record<string, unknown>): Promise<ApiResult<CandidateDetail>> {
    if (USE_MOCKS) return mockApi.updateCandidate(id, body) as never;
    return apiClient.put(`/candidates/${id}`, body);
  },

  async remove(id: string): Promise<ApiResult<boolean>> {
    if (USE_MOCKS) return mockApi.deleteCandidate(id) as never;
    return apiClient.delete(`/candidates/${id}`);
  },
};

export const workflowApi = {
  async definition(): Promise<ApiResult<WorkflowDefinition>> {
    if (USE_MOCKS) return mockApi.getWorkflowDefinition() as never;
    return apiClient.get("/workflow/config");
  },

  async stageView(stageIdOrSlug: string): Promise<
    ApiResult<{ stageId: string; stageName: string; items: WorkflowViewRow[] }>
  > {
    if (USE_MOCKS) return mockApi.getStageView(stageIdOrSlug) as never;

    let stageId = stageIdOrSlug;
    const isGuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(stageIdOrSlug);
    if (!isGuid) {
      const def = await this.definition();
      const slug = stageIdOrSlug.toLowerCase().replace(/s$/, "");
      const stage = def.data?.stages?.find((s) => {
        const name = s.name.toLowerCase().replace(/\s+/g, "-");
        return name === stageIdOrSlug.toLowerCase() || name.startsWith(slug) || name.includes(slug);
      });
      if (!stage) {
        return { isSuccess: false, data: { stageId: stageIdOrSlug, stageName: stageIdOrSlug, items: [] }, statusCode: 404, error: "Stage not found" };
      }
      stageId = stage.id;
    }

    const result = await apiClient.get<{ stageId: string; stageName: string; items: WorkflowViewRow[]; totalCount: number }>(
      `/workflow/views/${stageId}/candidates?page=1&pageSize=100`,
    );
    return result as never;
  },
};

export const officesApi = {
  async list(): Promise<ApiResult<Office[]>> {
    if (USE_MOCKS) return mockApi.getOffices() as never;
    return apiClient.get("/offices");
  },
};

export const partnersApi = {
  async list() {
    if (USE_MOCKS) return mockApi.getPartners();
    return apiClient.get("/partners");
  },
};

export { USE_MOCKS, mockApi };
