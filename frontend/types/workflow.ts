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
  stageType: number;
  isInitialStage: boolean;
  isFinalStage: boolean;
  expectedDurationHours?: number;
  warningDurationHours?: number;
  criticalDurationHours?: number;
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
  currentStageEnteredAt?: string;
  daysInStage?: number;
  isOverdue?: boolean;
}

export interface TrackStatusCell {
  trackKey: string;
  status?: string;
  since?: string;
  daysOnStep?: number;
}

export interface WorkflowViewRow {
  id: string;
  applicationNo: string;
  fullName: string;
  passportNumber: string;
  labourId?: string;
  countryOfTravel?: string;
  sponsorName?: string;
  officeName?: string;
  currentStatusValues: Record<string, string>;
  tracks: TrackStatusCell[];
  enteredAt?: string;
  daysInStage: number;
  lastActionAt?: string;
  lastActionLabel?: string;
  isOverdue: boolean;
  isPreview?: boolean;
  flightDate?: string;
  remainingDays?: number;
  availableActions: AvailableAction[];
}

export interface Office {
  id: string;
  name: string;
  code: string;
  city?: string;
  phone?: string;
  email?: string;
  isActive: boolean;
}
