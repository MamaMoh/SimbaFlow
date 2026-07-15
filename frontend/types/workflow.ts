export interface WorkflowDefinition {
  id: string;
  name: string;
  description?: string;
  version: number;
  isActive: boolean;
  stages: WorkflowStage[];
  transitionRules: WorkflowTransitionRule[];
}

export interface WorkflowStage {
  id: string;
  name: string;
  description?: string;
  sortOrder: number;
  stageType: number; // 0=Simple, 1=ParallelTrack, 2=MilestoneSequence
  isInitialStage: boolean;
  isFinalStage: boolean;
  statuses: WorkflowStageStatus[];
  parallelTracks: ParallelTrackDefinition[];
}

export interface WorkflowStageStatus {
  id: string;
  name: string;
  sortOrder: number;
  isTerminal: boolean;
  trackName?: string;
  color?: string;
}

export interface WorkflowTransitionRule {
  id: string;
  sourceStageId: string;
  targetStageId: string;
  buttonLabel: string;
  buttonIcon?: string;
  sortOrder: number;
  conditions: ConditionGroup;
  requiredFields: string[];
  allowedRoles: string[];
  removeFromSource: boolean;
  isActive: boolean;
}

export interface ConditionGroup {
  operator: "AND" | "OR";
  rules: ConditionRule[];
}

export interface ConditionRule {
  field: string;
  op: "eq" | "neq" | "in" | "not_empty" | "empty";
  value?: string | string[];
}

export interface ParallelTrackDefinition {
  id: string;
  trackName: string;
  completionStatus: string;
  sortOrder: number;
}

export interface AvailableAction {
  transitionRuleId: string;
  buttonLabel: string;
  buttonIcon?: string;
  isEnabled: boolean;
  disabledReason?: string;
}

export interface WorkflowState {
  stageId?: string;
  stageName?: string;
  statusValues: Record<string, string>;
  visibleInStages: string[];
}
