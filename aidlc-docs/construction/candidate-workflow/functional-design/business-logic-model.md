# Business Logic Model — Unit 2: Candidate & Workflow Engine

## BL-01: Candidate Registration

### Process Flow
```
RegisterCandidateCommand received
  1. Validate: required fields present, passport format valid
  2. Check uniqueness: passport number not already registered in this tenant
  3. Check uniqueness: labour ID (if provided) not already used
  4. Create Candidate entity with Status=Active
  5. Set CurrentStageId to the workflow's initial stage
  6. Append WorkflowEvent (EventType: Registered)
  7. Update denormalized CurrentStageId/CurrentStageName on candidate
  8. Emit CandidateRegistered domain event
  9. Return candidate ID
```

## BL-02: Workflow Engine — Event Sourcing

### State Derivation
```
GetWorkflowState(candidateId):
  1. Load latest WorkflowSnapshot for candidate (if exists)
  2. Load all WorkflowEvents after snapshot's SequenceNumber
  3. Apply each event sequentially to build current state:
     - StageTransitioned → update currentStage
     - StatusUpdated → update statusValues[trackName] = newValue
     - MirrorViewActivated → add targetStageId to visibleInStages[]
     - MirrorViewDeactivated → remove from visibleInStages[]
  4. Return: { currentStageId, stageName, statusValues, visibleInStages }
```

### Snapshot Strategy
```
After appending a WorkflowEvent:
  If (event.SequenceNumber % 20 == 0):
    Create WorkflowSnapshot with current derived state
    (This limits replay to max 20 events for any candidate)
```

## BL-03: Workflow Engine — Transition Execution

### Process Flow
```
ExecuteTransitionCommand(candidateId, transitionRuleId) received
  1. Load WorkflowDefinition for tenant
  2. Load TransitionRule by ID
  3. Derive current workflow state (BL-02)
  4. Validate preconditions:
     a. Candidate is currently in the rule's SourceStageId
     b. User's role is in AllowedRoles
     c. All Conditions evaluate to TRUE against current state
     d. All RequiredFields have non-empty values on candidate
  5. If ANY validation fails → return detailed error
  6. Append WorkflowEvent (EventType: StageTransitioned)
     - Data: { fromStage, toStage, transitionRuleId }
  7. Update denormalized fields on Candidate:
     - CurrentStageId = rule.TargetStageId
     - CurrentStageName = targetStage.Name
  8. If rule.RemoveFromSource == false → also keep source visibility
  9. Check mirror view rules on new stage → activate if conditions met
  10. Create snapshot if sequence threshold reached
  11. Emit CandidateStageChanged domain event
  12. Return success
```

## BL-04: Workflow Engine — Available Actions Calculation

### Process Flow
```
GetAvailableActions(candidateId, userId) received
  1. Derive current workflow state
  2. Get user's roles and officeId
  3. Load all TransitionRules from candidate's current stage(s)
     (including mirror view stages where candidate is visible)
  4. For each rule, evaluate:
     a. Is user's role in AllowedRoles? → include/exclude
     b. Are all Conditions met against current state? → enabled/disabled
     c. Are all RequiredFields filled? → enabled/disabled
  5. Return list of available actions:
     { transitionRuleId, buttonLabel, buttonIcon, isEnabled, disabledReason? }
```

### Client-Side Optimistic Evaluation
```
Frontend receives candidate data + available actions from API.
On field change (e.g., user updates medical status):
  1. Frontend re-evaluates conditions locally
  2. If newly enabled → show button immediately (optimistic)
  3. On button click → server re-validates (authoritative)
  4. If server rejects → show error, revert button state
```

## BL-05: Workflow Engine — Mirror View Logic

### Process Flow
```
After any StatusUpdate or FieldUpdate event:
  1. Load MirrorViewRules for candidate's current stage
  2. For each rule:
     a. Evaluate Conditions against current state
     b. If conditions NOW met AND candidate NOT already in target view:
        - Append MirrorViewActivated event
        - Add targetStageId to candidate's visibleInStages
     c. If conditions NO LONGER met AND candidate IS in target view:
        - Append MirrorViewDeactivated event
        - Remove targetStageId from visibleInStages
  3. Emit MirrorViewChanged domain event (if any changes)
```

### View Query Logic
```
GetViewCandidates(stageId):
  Return candidates WHERE:
    CurrentStageId == stageId
    OR stageId IN candidate.VisibleInStages (mirror view)
```

## BL-06: Workflow Engine — Parallel Track Status Updates

### Process Flow
```
UpdateStatusCommand(candidateId, trackName, newStatusValue) received
  1. Derive current workflow state
  2. Validate: candidate is in a ParallelTrack stage
  3. Validate: trackName is a valid track for this stage
  4. Validate: newStatusValue is a valid status for this track
  5. Append WorkflowEvent (EventType: StatusUpdated)
     - Data: { trackName, oldValue, newValue }
  6. Update denormalized CurrentStatusValues on candidate
  7. Re-evaluate mirror view conditions (BL-05)
  8. Emit CandidateStatusChanged domain event
```

## BL-07: Workflow Engine — Condition Evaluation

### Condition JSON Processing
```
EvaluateConditions(conditions: JsonDocument, currentState: WorkflowState) → bool
  
  Parse root operator (AND / OR):
  For each rule in rules[]:
    Extract: field, op, value
    Get actual value from currentState.statusValues[field] or candidate fields
    Evaluate based on op:
      - "eq": actual == expected
      - "neq": actual != expected
      - "in": expected.Contains(actual)
      - "not_empty": actual is not null/empty
      - "empty": actual is null/empty
    
  Combine results with root operator (AND = all true, OR = any true)
```

## BL-08: Candidate Search & Filtering

### Process Flow
```
SearchCandidatesQuery(term, filters, page, pageSize) received
  1. Start with base query: Candidates.Where(!IsDeleted && Status == Active)
  2. Apply text search (if term provided):
     - Match against: FirstName, LastName, PassportNumber, LabourId
     - Case-insensitive, partial match (ILIKE in PostgreSQL)
  3. Apply filters:
     - stageId → CurrentStageId == stageId OR stageId in VisibleInStages
     - officeId → OfficeId == officeId
     - countryOfTravel → CountryOfTravel == value
     - status field values → JSONB query on CurrentStatusValues
  4. Apply pagination (OFFSET/LIMIT)
  5. Return paginated result with total count
```

## BL-09: Candidate CV Generation

### Process Flow
```
GenerateCVCommand(candidateId) received
  1. Load candidate with all profile fields
  2. Load candidate photo (if exists)
  3. Build CV data model (name, nationality, DOB, contact, photo, skills, etc.)
  4. Render PDF using CV template (QuestPDF)
  5. Store generated PDF in file storage (DocumentType: CV)
  6. Return PDF byte stream
```

## BL-10: Workflow Configuration — Default Template Seeding

### 8-Stage Default Template
```
Stage 1: "Intake" (Initial Stage, Simple)
  Statuses: ["Registered"]
  Transition: → "New Contracts" (auto on registration)

Stage 2: "New Contracts" (Simple)
  Statuses: ["Pending Review", "Ready"]
  Transition: "To Embassy" → "Embassy" (requires: Status=Ready, role: Office Manager+)

Stage 3: "Embassy" (ParallelTrack)
  Tracks: ["Medical", "Tasheer"]
  Medical Statuses: ["Pending", "Booked", "Fit", "Unfit"]
  Tasheer Statuses: ["Pending", "Booked", "Book Done", "Expired"]
  Additional Status: ["Ready", "Submitted", "Visa Issued", "Visa Rejected"]
  Mirror Rule: IF medical=Fit AND tasheer=BookDone → Show in "LMIS"
  Transition: "To LMIS" → "LMIS" (requires: visa_status=Issued, RemoveFromSource=true)

Stage 4: "LMIS" (MilestoneSequence)
  Statuses: ["Insurance Unpaid", "Insurance Paid", "Available"]
  Milestones: ["Uploaded", "Check Verified", "Issued"]
  Transition: "To Ticket" → "Ticket" (requires: milestone=Issued)

Stage 5: "Ticket" (Simple)
  Statuses: ["Pending", "Booking Complete"]
  Required Fields for transition: ticket_status, destination, flight_date
  Transition: "To Departure" → "Departure" (requires: all 3 fields filled)

Stage 6: "Departure" (Simple)
  Statuses: ["Awaiting", "Notified", "Departed", "Not Departed"]
  Transition A: "To Arrival" → "Arrival" (requires: status=Departed)
  Transition B: "Back to Ticket" → "Ticket" (requires: status=Not Departed)
  Transition C: "Cancel" → "Canceled" (requires: status=Not Departed)

Stage 7: "Arrival" (Simple)
  Statuses: ["Pending", "Arrived", "Returned", "Runaway"]
  Transition: "Add to Commission" → stays in Arrival + creates commission record

Stage 8: "Commission" (Final Stage)
  Statuses: ["Pending", "Partial", "Settled", "Disputed"]
```
