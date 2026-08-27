export interface WorkflowDefinition {
  id: string;
  name: string;
  description?: string;
  version: number;
  isActive: boolean;
  stages: WorkflowStage[];
  transitionRules: WorkflowTransitionRule[];
  mirrorViewRules: MirrorViewRule[];
}

/**
 * A mirror shows a candidate on a second board without moving them off the first.
 * Embassy → LMIS is the one agencies retune, because whether tasheer must be booked
 * before LMIS registration is a government rule that varies by destination.
 */
export interface MirrorViewRule {
  id: string;
  sourceStageId: string;
  sourceStageName: string;
  targetStageId: string;
  targetStageName: string;
  conditions: ConditionGroup;
  isActive: boolean;
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
