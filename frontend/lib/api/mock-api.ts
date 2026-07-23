import {
  DEMO_OFFICES,
  applyDemoCandidateAction,
  deleteDemoCandidate,
  getCandidateDetail,
  getCandidateList,
  getDemoTimeline,
  getStageView,
  registerDemoCandidate,
  updateDemoCandidate,
} from "@/lib/demo/demo-data";
import {
  createDemoAgency,
  createDemoRole,
  createDemoUser,
  deleteDemoAgency,
  deleteDemoRole,
  deleteDemoUser,
  getDemoAgencies,
  getDemoDepartments,
  getDemoLedger,
  getDemoOffices,
  getDemoPartners,
  getDemoRoles,
  getDemoSettings,
  getDemoUsers,
  resetDemoUserPassword,
  setDemoAgencyStatus,
  toggleDemoUser,
  updateDemoAgency,
} from "@/lib/demo/admin-demo-store";
import { getWorkflowConfig } from "@/lib/demo/workflow-config-store";

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

  async registerCandidate(body: Record<string, unknown>) {
    const id = registerDemoCandidate(body);
    return wrap(id, 201);
  },

  async applyCandidateAction(candidateId: string, actionId: string) {
    const result = applyDemoCandidateAction(candidateId, actionId);
    if (!result.ok) return { isSuccess: false, data: null, statusCode: 400, error: result.message };
    return wrap(result);
  },

  async updateCandidate(id: string, body: Record<string, unknown>) {
    const ok = updateDemoCandidate(id, body as any);
    if (!ok) return { isSuccess: false, data: null, statusCode: 404, error: "Not found" };
    return wrap(getCandidateDetail(id));
  },

  async deleteCandidate(id: string) {
    const ok = deleteDemoCandidate(id);
    if (!ok) return { isSuccess: false, data: null, statusCode: 404, error: "Not found" };
    return wrap(true);
  },

  async getOffices() {
    return wrap(getDemoOffices().length ? getDemoOffices() : DEMO_OFFICES);
  },

  async getPartners() {
    return wrap(getDemoPartners());
  },

  async getWorkflowDefinition() {
    return wrap(getWorkflowConfig());
  },

  async getStageView(stageId: string) {
    return wrap(getStageView(stageId));
  },

  async getUsers() {
    const items = getDemoUsers();
    return wrap({ items, totalCount: items.length, page: 1, pageSize: 100 });
  },

  async createUser(body: any) {
    const user = createDemoUser(body);
    return wrap(user, 201);
  },

  async toggleUser(id: string) {
    toggleDemoUser(id);
    return wrap(true);
  },

  async deleteUser(id: string) {
    deleteDemoUser(id);
    return wrap(true);
  },

  async resetUserPassword(id: string) {
    resetDemoUserPassword(id);
    return wrap(true);
  },

  async getRoles() {
    return wrap(getDemoRoles());
  },

  async createRole(body: any) {
    return wrap(createDemoRole(body), 201);
  },

  async deleteRole(id: string) {
    deleteDemoRole(id);
    return wrap(true);
  },

  async getTenants() {
    return wrap(getDemoAgencies());
  },

  async createTenant(body: any) {
    return wrap(createDemoAgency(body), 201);
  },

  async updateTenant(id: string, body: any) {
    updateDemoAgency(id, body);
    return wrap(true);
  },

  async setTenantStatus(id: string, status: number) {
    setDemoAgencyStatus(id, status);
    return wrap(true);
  },

  async deleteTenant(id: string) {
    deleteDemoAgency(id);
    return wrap(true);
  },

  async getDepartments() {
    return wrap(getDemoDepartments());
  },

  async getLedger() {
    return wrap(getDemoLedger());
  },

  async getSettings() {
    return wrap(getDemoSettings());
  },
};
