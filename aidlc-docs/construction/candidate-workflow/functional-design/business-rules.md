# Business Rules — Unit 2: Candidate & Workflow Engine

## BR-C01: Candidate Registration Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-C01.1 | Passport number must be unique within the tenant | Unique index on (PassportNumber) in tenant schema |
| BR-C01.2 | Labour ID must be unique within the tenant (if provided) | Unique index on (LabourId) WHERE LabourId IS NOT NULL |
| BR-C01.3 | Date of birth must be in the past | Validation: DOB < today |
| BR-C01.4 | Candidate must be assigned to an office | OfficeId required, validated against existing offices |
| BR-C01.5 | Passport number format: alphanumeric, 5-20 characters | Regex validation |
| BR-C01.6 | First and last name are required (min 2 chars each) | FluentValidation |
| BR-C01.7 | On registration, candidate enters the workflow's initial stage | WorkflowEngine sets CurrentStageId |

## BR-C02: Workflow Event Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-C02.1 | Events are append-only — never modified or deleted | No UPDATE/DELETE operations on WorkflowEvent table |
| BR-C02.2 | SequenceNumber is strictly monotonic per candidate | Database sequence or max+1 logic |
| BR-C02.3 | Every event must have a valid UserId | Not-null constraint |
| BR-C02.4 | Event Timestamp is set server-side (UTC) | Cannot be provided by client |
| BR-C02.5 | Event Data (JSONB) is validated per EventType schema | Validated before append |
| BR-C02.6 | Snapshots created every 20 events per candidate | Post-append check |

## BR-C03: Transition Execution Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-C03.1 | Candidate must be in the transition's source stage | Pre-check: CurrentStageId == rule.SourceStageId |
| BR-C03.2 | User's role must be in the transition's AllowedRoles | Pre-check against JWT role claim |
| BR-C03.3 | All transition Conditions must evaluate to TRUE | Condition evaluator (BL-07) |
| BR-C03.4 | All RequiredFields must have non-empty values | Check candidate entity fields |
| BR-C03.5 | A candidate cannot transition to a stage it's already in (unless mirror) | Prevent self-loops |
| BR-C03.6 | Concurrent transitions on the same candidate are prevented | Optimistic concurrency via RowVersion |
| BR-C03.7 | Transition execution is atomic (all-or-nothing) | Single database transaction |

## BR-C04: Mirror View Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-C04.1 | Mirror view does not duplicate the candidate record | Single Candidate row, multiple stage visibility |
| BR-C04.2 | Mirror activation is automatic when conditions are met | Evaluated on every status/field update |
| BR-C04.3 | Mirror deactivation is automatic when conditions are no longer met | Evaluated on every status/field update |
| BR-C04.4 | Updates made in either view (source or mirror) affect the same record | Single record, updates via candidateId |
| BR-C04.5 | Explicit transfer (e.g., "To LMIS") may remove source visibility | TransitionRule.RemoveFromSource = true |

## BR-C05: Available Actions Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-C05.1 | Available actions are computed server-side (authoritative) | API endpoint returns actions |
| BR-C05.2 | Client may evaluate conditions optimistically for UX | Frontend local evaluation |
| BR-C05.3 | Server ALWAYS re-validates on action execution | ExecuteTransition validates regardless of client state |
| BR-C05.4 | Disabled actions include a reason for UI display | DisabledReason field in response |
| BR-C05.5 | Actions only visible to users with appropriate roles | Filtered by user's current roles |

## BR-C06: Parallel Track Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-C06.1 | Each parallel track has independent status progression | Separate status values per track in JSONB |
| BR-C06.2 | Stage advancement requires ALL tracks to reach completion status | Combined condition evaluation |
| BR-C06.3 | Updating one track does not affect other tracks | Independent JSONB fields |
| BR-C06.4 | Track statuses are displayed side-by-side in the UI | API returns all track statuses per candidate |

## BR-C07: Workflow Configuration Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-C07.1 | Only Agency Owner or Admin can configure workflow | Permission: workflow.configure |
| BR-C07.2 | Exactly one stage must be marked as initial (IsInitialStage) | Validation on save |
| BR-C07.3 | Removing a stage does not affect candidates already in that stage | Candidates retain their stage until manually moved |
| BR-C07.4 | Transition rules cannot create circular dependencies that are unresolvable | Validation: no mandatory loops without exit |
| BR-C07.5 | Condition field names must reference valid candidate/status fields | Validated against known field list |
| BR-C07.6 | Workflow version is incremented on any configuration change | Auto-increment on save |
| BR-C07.7 | Active transitions cannot reference deleted stages | Cascade validation on stage deletion |

## BR-C08: Document Management Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-C08.1 | File size limit: 10MB per document | Validated before upload |
| BR-C08.2 | Allowed types: PDF, JPG, JPEG, PNG, DOCX | Magic byte + extension validation |
| BR-C08.3 | Documents belong to a single candidate | FK constraint |
| BR-C08.4 | Document deletion is soft (file remains on disk) | IsDeleted flag |
| BR-C08.5 | Accessing documents requires candidate.read permission + same tenant | Authorization check |
| BR-C08.6 | Document access is read-audit-logged | IReadAuditService call |

## BR-C09: CV Generation Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-C09.1 | CV includes all non-null biographical fields | Template renders available data |
| BR-C09.2 | CV includes candidate photo if uploaded | Photo path resolved from documents |
| BR-C09.3 | Generated CV is stored as a document (DocumentType: CV) | Auto-saved to file storage |
| BR-C09.4 | Previous CVs are not deleted when new one is generated | Multiple CV versions allowed |

## Testable Properties (PBT-01 Compliance)

| Property | Category | Description |
|----------|----------|-------------|
| Event replay idempotence | Idempotence | Replaying events N times always produces the same state |
| Event ordering invariant | Invariant | State derived from events in order X always equals state from events in order X (deterministic) |
| Transition precondition enforcement | Invariant | If any precondition fails, no event is appended (atomic reject) |
| Mirror view symmetry | Invariant | Candidate visible in mirror stage query ↔ targetStageId in visibleInStages |
| Snapshot consistency | Round-trip | State from (snapshot + events after) == state from (all events from start) |
| Condition evaluation commutativity | Commutativity | AND conditions: order of evaluation doesn't change result |
| Candidate search invariant | Invariant | Total candidates in all stages == total active candidates (no candidate lost or duplicated) |
| Available actions determinism | Idempotence | Same (candidate state + user role) always produces same available actions |
