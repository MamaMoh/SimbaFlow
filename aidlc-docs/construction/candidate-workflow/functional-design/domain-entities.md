# Domain Entities — Unit 2: Candidate & Workflow Engine

## Core Entities

### Candidate (Aggregate Root)

```
Candidate : BaseEntity
├── FirstName : string (required, max 100)
├── LastName : string (required, max 100)
├── MiddleName : string? (max 100)
├── PassportNumber : string (required, unique per tenant, max 20)
├── LabourId : string? (unique per tenant, max 50)
├── Nationality : string? (max 100)
├── DateOfBirth : DateOnly (required)
├── Gender : Gender (Male, Female)
├── PhoneNumber : string? (max 20)
├── Email : string? (max 200)
├── Address : string? (max 500)
├── City : string? (max 100)
├── Country : string? (max 100)
├── CountryOfTravel : string? (max 100)
├── OfficeName : string? (max 200) — overseas partner/employer
├── ContractDate : DateOnly?
├── OfficeId : Guid (required — which branch registered this candidate)
├── PhotoPath : string? — file system relative path
├── Status : CandidateStatus (Active, Archived, Deleted)
├── CurrentStageId : Guid? — denormalized from workflow events for query performance
├── CurrentStageName : string? — denormalized
├── CurrentStatusValues : Dictionary<string, string>? (JSONB) — e.g. {"medical": "Fit", "tasheer": "BookDone"}
├── RegisteredAt : DateTime
├── RegisteredBy : string
│
├── Documents : ICollection<CandidateDocument>
└── WorkflowEvents : ICollection<WorkflowEvent> (event stream)
```

### CandidateDocument

```
CandidateDocument : BaseEntity
├── CandidateId : Guid (FK)
├── FileName : string (required, max 255)
├── OriginalFileName : string (required, max 255)
├── ContentType : string (required, max 100)
├── FilePath : string (required) — relative path on file system
├── ThumbnailPath : string? — thumbnail relative path (images only)
├── DocumentType : DocumentType (Passport, Photo, Contract, CV, LMIS, Other)
├── FileSizeBytes : long
├── UploadedAt : DateTime
└── UploadedBy : string
```

---

## Workflow Event Sourcing Entities

### WorkflowEvent (Append-Only Event Stream)

```
WorkflowEvent
├── Id : Guid (PK)
├── CandidateId : Guid (FK, indexed)
├── SequenceNumber : long (auto-increment per candidate)
├── EventType : WorkflowEventType (enum)
├── FromStageId : Guid?
├── FromStageName : string?
├── ToStageId : Guid?
├── ToStageName : string?
├── Data : JsonDocument (JSONB) — event-specific payload
├── UserId : Guid (who performed the action)
├── UserName : string
├── Timestamp : DateTime (UTC)
├── Notes : string?
└── TenantId : Guid (for cross-query if needed)
```

**Event Types**:
- `Registered` — Candidate first entered the system
- `StageTransitioned` — Moved from one stage to another
- `StatusUpdated` — Status field changed within a stage (e.g., medical: Booked → Fit)
- `FieldUpdated` — Data field changed (e.g., insurance: Unpaid → Paid)
- `ActionExecuted` — Action button clicked (e.g., "To Embassy")
- `MirrorViewActivated` — Conditions met for mirror view
- `MirrorViewDeactivated` — Conditions no longer met or explicit transfer
- `ExceptionFlagged` — Returned/Runaway
- `Archived` — Soft deleted

### WorkflowSnapshot (Performance Optimization)

```
WorkflowSnapshot
├── Id : Guid (PK)
├── CandidateId : Guid (FK, indexed)
├── SequenceNumber : long — snapshot taken at this event
├── StageId : Guid
├── StageName : string
├── StatusValues : JsonDocument (JSONB) — all current status fields
├── VisibleInStages : Guid[] — which stages this candidate appears in (mirror views)
├── CreatedAt : DateTime
```

---

## Workflow Configuration Entities

### WorkflowDefinition

```
WorkflowDefinition : BaseEntity
├── TenantId : Guid (one definition per tenant)
├── Name : string (default: "Default Workflow")
├── Description : string?
├── Version : int (incremented on changes)
├── IsActive : bool
│
├── Stages : ICollection<WorkflowStage>
└── TransitionRules : ICollection<WorkflowTransitionRule>
```

### WorkflowStage

```
WorkflowStage : BaseEntity
├── WorkflowDefinitionId : Guid (FK)
├── Name : string (required, max 100)
├── Description : string?
├── SortOrder : int (display order)
├── StageType : StageType (Simple, ParallelTrack, MilestoneSequence)
├── IsInitialStage : bool (first stage candidates enter)
├── IsFinalStage : bool (completion/archive stage)
│
├── Statuses : ICollection<WorkflowStageStatus>
├── ParallelTracks : ICollection<ParallelTrackDefinition>?
├── MirrorViewRules : ICollection<MirrorViewRule>?
└── MandatoryFields : ICollection<StageMandatoryField>?
```

### WorkflowStageStatus

```
WorkflowStageStatus : BaseEntity
├── WorkflowStageId : Guid (FK)
├── Name : string (required, max 100)
├── SortOrder : int
├── IsTerminal : bool (if true, marks stage as complete)
├── TrackName : string? (for parallel tracks — which track this status belongs to)
└── Color : string? (hex color for UI)
```

### WorkflowTransitionRule

```
WorkflowTransitionRule : BaseEntity
├── WorkflowDefinitionId : Guid (FK)
├── SourceStageId : Guid (FK)
├── TargetStageId : Guid (FK)
├── ButtonLabel : string (required, max 100) — e.g., "To Embassy", "To LMIS"
├── ButtonIcon : string? — optional icon name
├── SortOrder : int — button display order
├── Conditions : JsonDocument (JSONB) — field conditions that must be true
├── RequiredFields : string[] — fields that must have values before transition
├── AllowedRoles : string[] — roles that can execute this transition
├── RemoveFromSource : bool (default: true) — if false, candidate remains visible in source (mirror)
├── IsActive : bool
```

**Conditions JSON Format**:
```json
{
  "operator": "AND",
  "rules": [
    { "field": "medical_status", "op": "eq", "value": "Fit" },
    { "field": "tasheer_status", "op": "eq", "value": "BookDone" }
  ]
}
```

### ParallelTrackDefinition

```
ParallelTrackDefinition : BaseEntity
├── WorkflowStageId : Guid (FK)
├── TrackName : string (required, max 100) — e.g., "Medical", "Tasheer"
├── CompletionStatus : string — which status value means "track done"
├── SortOrder : int
```

### MirrorViewRule

```
MirrorViewRule : BaseEntity
├── WorkflowStageId : Guid (FK — source stage)
├── TargetStageId : Guid (FK — stage where candidate also appears)
├── Conditions : JsonDocument (JSONB) — same format as transition conditions
├── IsActive : bool
```

### StageMandatoryField

```
StageMandatoryField : BaseEntity
├── WorkflowStageId : Guid (FK)
├── FieldName : string — candidate field name
├── TransitionRuleId : Guid? — if null, mandatory for ALL transitions from this stage
```

---

## Enums

### CandidateStatus
```
Active = 0
Archived = 1
```

### Gender
```
Male = 0
Female = 1
```

### DocumentType
```
Passport = 0
Photo = 1
Contract = 2
CV = 3
LMIS = 4
MedicalCertificate = 5
TasheerDocument = 6
TicketBooking = 7
Other = 99
```

### WorkflowEventType
```
Registered = 0
StageTransitioned = 1
StatusUpdated = 2
FieldUpdated = 3
ActionExecuted = 4
MirrorViewActivated = 5
MirrorViewDeactivated = 6
ExceptionFlagged = 7
Archived = 8
```

### StageType
```
Simple = 0           — Single status track
ParallelTrack = 1    — Multiple independent tracks (e.g., Medical + Tasheer)
MilestoneSequence = 2 — Sequential milestones (e.g., Uploaded → Verified → Issued)
```

---

## Indexes (Performance Critical)

- `IX_Candidate_TenantPassport` — Unique: (TenantId via schema, PassportNumber)
- `IX_Candidate_LabourId` — Unique: (LabourId) where not null
- `IX_Candidate_CurrentStageId` — For view queries: candidates in a specific stage
- `IX_Candidate_OfficeId` — For office-scoped queries
- `IX_WorkflowEvent_CandidateId_Seq` — For event replay: (CandidateId, SequenceNumber)
- `IX_WorkflowEvent_Timestamp` — For temporal queries
- `IX_WorkflowSnapshot_CandidateId` — Latest snapshot lookup
