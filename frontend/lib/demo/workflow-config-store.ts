import workflowStagesJson from "@/mocks/workflow-stages.json";
import type {
  ParallelTrackDefinition,
  WorkflowDefinition,
  WorkflowStage,
  WorkflowStageStatus,
  WorkflowTransitionRule,
} from "@/types/workflow";

function cloneDefinition(): WorkflowDefinition {
  return structuredClone(workflowStagesJson) as WorkflowDefinition;
}

let store: WorkflowDefinition = cloneDefinition();
const listeners = new Set<() => void>();

function notify() {
  listeners.forEach((l) => l());
}

function nextId(prefix: string) {
  return `${prefix}-${Math.random().toString(36).slice(2, 10)}`;
}

export function getWorkflowConfig(): WorkflowDefinition {
  return store;
}

export function subscribeWorkflowConfig(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function resetWorkflowConfig() {
  store = cloneDefinition();
  notify();
  return store;
}

export function updateWorkflowMeta(patch: Partial<Pick<WorkflowDefinition, "name" | "description" | "isActive">>) {
  store = { ...store, ...patch, version: store.version + 1 };
  notify();
  return store;
}

export function upsertStage(input: {
  id?: string;
  name: string;
  description?: string;
  stageType: number;
  expectedDurationHours?: number;
  warningDurationHours?: number;
  criticalDurationHours?: number;
  isInitialStage?: boolean;
  isFinalStage?: boolean;
  statuses?: WorkflowStageStatus[];
  parallelTracks?: ParallelTrackDefinition[];
}) {
  const stages = [...store.stages].sort((a, b) => a.sortOrder - b.sortOrder);

  if (input.id) {
    store = {
      ...store,
      version: store.version + 1,
      stages: stages.map((s) =>
        s.id === input.id
          ? {
              ...s,
              name: input.name,
              description: input.description,
              stageType: input.stageType,
              expectedDurationHours: input.expectedDurationHours,
              warningDurationHours: input.warningDurationHours,
              criticalDurationHours: input.criticalDurationHours,
              isInitialStage: !!input.isInitialStage,
              isFinalStage: !!input.isFinalStage,
              statuses: input.statuses ?? s.statuses,
              parallelTracks: input.parallelTracks ?? s.parallelTracks,
            }
          : {
              ...s,
              isInitialStage: input.isInitialStage ? false : s.isInitialStage,
              isFinalStage: input.isFinalStage ? false : s.isFinalStage,
            },
      ),
    };
  } else {
    const stage: WorkflowStage = {
      id: nextId("stage"),
      name: input.name,
      description: input.description,
      sortOrder: stages.length + 1,
      stageType: input.stageType,
      isInitialStage: !!input.isInitialStage,
      isFinalStage: !!input.isFinalStage,
      expectedDurationHours: input.expectedDurationHours,
      warningDurationHours: input.warningDurationHours,
      criticalDurationHours: input.criticalDurationHours,
      statuses: input.statuses ?? [],
      parallelTracks: input.parallelTracks ?? [],
    };
    store = {
      ...store,
      version: store.version + 1,
      stages: [
        ...stages.map((s) => ({
          ...s,
          isInitialStage: stage.isInitialStage ? false : s.isInitialStage,
          isFinalStage: stage.isFinalStage ? false : s.isFinalStage,
        })),
        stage,
      ],
    };
  }
  notify();
  return store;
}

export function deleteStage(stageId: string) {
  store = {
    ...store,
    version: store.version + 1,
    stages: store.stages
      .filter((s) => s.id !== stageId)
      .map((s, i) => ({ ...s, sortOrder: i + 1 })),
    transitionRules: store.transitionRules.filter(
      (t) => t.sourceStageId !== stageId && t.targetStageId !== stageId,
    ),
  };
  notify();
  return store;
}

export function moveStage(stageId: string, direction: "up" | "down") {
  const stages = [...store.stages].sort((a, b) => a.sortOrder - b.sortOrder);
  const idx = stages.findIndex((s) => s.id === stageId);
  if (idx < 0) return store;
  const swapWith = direction === "up" ? idx - 1 : idx + 1;
  if (swapWith < 0 || swapWith >= stages.length) return store;
  const tmp = stages[idx].sortOrder;
  stages[idx] = { ...stages[idx], sortOrder: stages[swapWith].sortOrder };
  stages[swapWith] = { ...stages[swapWith], sortOrder: tmp };
  store = { ...store, version: store.version + 1, stages };
  notify();
  return store;
}

export function upsertTransition(input: Omit<WorkflowTransitionRule, "id" | "conditions" | "sortOrder"> & {
  id?: string;
  conditionField?: string;
  conditionValue?: string;
  sortOrder?: number;
}) {
  const conditions = {
    operator: "AND" as const,
    rules:
      input.conditionField && input.conditionValue
        ? [{ field: input.conditionField, op: "eq" as const, value: input.conditionValue }]
        : [],
  };

  if (input.id) {
    store = {
      ...store,
      version: store.version + 1,
      transitionRules: store.transitionRules.map((t) =>
        t.id === input.id
          ? {
              ...t,
              sourceStageId: input.sourceStageId,
              targetStageId: input.targetStageId,
              buttonLabel: input.buttonLabel,
              buttonIcon: input.buttonIcon,
              requiredFields: input.requiredFields,
              allowedRoles: input.allowedRoles,
              removeFromSource: input.removeFromSource,
              isActive: input.isActive,
              conditions,
              sortOrder: input.sortOrder ?? t.sortOrder,
            }
          : t,
      ),
    };
  } else {
    const rule: WorkflowTransitionRule = {
      id: nextId("tr"),
      sourceStageId: input.sourceStageId,
      targetStageId: input.targetStageId,
      buttonLabel: input.buttonLabel,
      buttonIcon: input.buttonIcon,
      sortOrder: input.sortOrder ?? store.transitionRules.length + 1,
      conditions,
      requiredFields: input.requiredFields,
      allowedRoles: input.allowedRoles,
      removeFromSource: input.removeFromSource,
      isActive: input.isActive,
    };
    store = {
      ...store,
      version: store.version + 1,
      transitionRules: [...store.transitionRules, rule],
    };
  }
  notify();
  return store;
}

export function deleteTransition(id: string) {
  store = {
    ...store,
    version: store.version + 1,
    transitionRules: store.transitionRules.filter((t) => t.id !== id),
  };
  notify();
  return store;
}

export function toggleTransitionActive(id: string) {
  store = {
    ...store,
    version: store.version + 1,
    transitionRules: store.transitionRules.map((t) =>
      t.id === id ? { ...t, isActive: !t.isActive } : t,
    ),
  };
  notify();
  return store;
}
