# NFR Design — Unit 2: Candidate & Workflow Engine

## 1. Event Store Design (PostgreSQL)

### Table Schema
```sql
CREATE TABLE workflow_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    candidate_id UUID NOT NULL REFERENCES candidates(id),
    sequence_number BIGINT NOT NULL,
    event_type SMALLINT NOT NULL,
    from_stage_id UUID,
    from_stage_name VARCHAR(100),
    to_stage_id UUID,
    to_stage_name VARCHAR(100),
    data JSONB NOT NULL DEFAULT '{}',
    user_id UUID NOT NULL,
    user_name VARCHAR(200) NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    notes TEXT,
    
    CONSTRAINT uq_candidate_sequence UNIQUE (candidate_id, sequence_number)
);

CREATE INDEX ix_workflow_events_candidate_seq 
    ON workflow_events (candidate_id, sequence_number);
CREATE INDEX ix_workflow_events_timestamp 
    ON workflow_events (timestamp);
```

### Append Pattern
```csharp
// Atomic: append event + update candidate in single transaction
await using var transaction = await _context.Database.BeginTransactionAsync(ct);

var nextSeq = await _context.WorkflowEvents
    .Where(e => e.CandidateId == candidateId)
    .MaxAsync(e => (long?)e.SequenceNumber, ct) ?? 0;

var workflowEvent = new WorkflowEvent {
    CandidateId = candidateId,
    SequenceNumber = nextSeq + 1,
    EventType = eventType,
    Data = JsonSerializer.SerializeToDocument(eventData),
    // ...
};

_context.WorkflowEvents.Add(workflowEvent);
candidate.CurrentStageId = targetStageId;  // denormalized
candidate.CurrentStageName = targetStageName;
await _context.SaveChangesAsync(ct);
await transaction.CommitAsync(ct);
```

### Replay Pattern
```csharp
public async Task<WorkflowState> DeriveStateAsync(Guid candidateId, CancellationToken ct)
{
    // Load latest snapshot
    var snapshot = await _context.WorkflowSnapshots
        .Where(s => s.CandidateId == candidateId)
        .OrderByDescending(s => s.SequenceNumber)
        .FirstOrDefaultAsync(ct);

    var startSeq = snapshot?.SequenceNumber ?? 0;
    var state = snapshot is not null
        ? WorkflowState.FromSnapshot(snapshot)
        : WorkflowState.Initial();

    // Load events after snapshot
    var events = await _context.WorkflowEvents
        .Where(e => e.CandidateId == candidateId && e.SequenceNumber > startSeq)
        .OrderBy(e => e.SequenceNumber)
        .ToListAsync(ct);

    // Apply each event to state
    foreach (var evt in events)
        state = state.Apply(evt);

    return state;
}
```

---

## 2. Denormalization Strategy

### Write Path (Transition)
```
Transaction {
  1. Append WorkflowEvent
  2. Update Candidate.CurrentStageId
  3. Update Candidate.CurrentStageName  
  4. Update Candidate.CurrentStatusValues (JSONB)
  5. Commit
}
→ SignalR broadcast (fire-and-forget, outside transaction)
```

### Read Path (View Query)
```sql
-- Fast view query using denormalized fields (no event replay needed)
SELECT * FROM candidates 
WHERE current_stage_id = @stageId 
   OR @stageId = ANY(visible_in_stages)
ORDER BY created_at DESC
LIMIT @pageSize OFFSET @offset;
```

### Consistency Guarantee
- Denormalized state is ALWAYS updated in the same transaction as the event append
- If transaction fails, both event and denormalized state are rolled back
- Snapshots are eventually consistent (created asynchronously every 20 events)
- Source of truth is always the event stream — denormalized state can be rebuilt

---

## 3. Condition Evaluation Engine

### Architecture
```
ConditionEvaluator (stateless service)
  ├── Input: ConditionDocument (JSONB), WorkflowState
  ├── Output: bool (true/false)
  └── Logic:
      Parse root: { operator: "AND"|"OR", rules: [...] }
      For each rule:
        Extract field value from state.statusValues or candidate fields
        Apply operator: eq, neq, in, not_empty, empty
      Combine with root operator
```

### Supported Operators
| Operator | Description | Example |
|----------|-------------|---------|
| `eq` | Equals | `{"field": "medical", "op": "eq", "value": "Fit"}` |
| `neq` | Not equals | `{"field": "status", "op": "neq", "value": "Rejected"}` |
| `in` | In list | `{"field": "role", "op": "in", "value": ["Admin","Manager"]}` |
| `not_empty` | Has a value | `{"field": "flight_date", "op": "not_empty"}` |
| `empty` | Is null/empty | `{"field": "rejection_reason", "op": "empty"}` |

### Client-Side Evaluation (TypeScript)
```typescript
function evaluateConditions(conditions: ConditionGroup, state: CandidateState): boolean {
  const results = conditions.rules.map(rule => {
    const value = state.statusValues[rule.field] ?? state.fields[rule.field];
    switch (rule.op) {
      case 'eq': return value === rule.value;
      case 'neq': return value !== rule.value;
      case 'in': return (rule.value as string[]).includes(value);
      case 'not_empty': return !!value;
      case 'empty': return !value;
      default: return false;
    }
  });
  return conditions.operator === 'AND' 
    ? results.every(Boolean) 
    : results.some(Boolean);
}
```

---

## 4. Search & Indexing Strategy

### PostgreSQL Indexes for Candidate Table
```sql
-- Primary search (name, passport, labour ID)
CREATE INDEX ix_candidates_search ON candidates 
    USING GIN (to_tsvector('simple', 
        coalesce(first_name,'') || ' ' || 
        coalesce(last_name,'') || ' ' || 
        coalesce(passport_number,'') || ' ' || 
        coalesce(labour_id,'')));

-- Stage view queries (most common)
CREATE INDEX ix_candidates_stage ON candidates (current_stage_id) 
    WHERE is_deleted = false AND status = 0;

-- Office filtering
CREATE INDEX ix_candidates_office ON candidates (office_id) 
    WHERE is_deleted = false;

-- Mirror view queries (array contains)
CREATE INDEX ix_candidates_visible_stages ON candidates 
    USING GIN (visible_in_stages);
```

### Query Pattern
```csharp
var query = _context.Candidates
    .Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active);

if (!string.IsNullOrEmpty(search))
    query = query.Where(c => 
        EF.Functions.ILike(c.FirstName, $"%{search}%") ||
        EF.Functions.ILike(c.LastName, $"%{search}%") ||
        EF.Functions.ILike(c.PassportNumber, $"%{search}%") ||
        EF.Functions.ILike(c.LabourId ?? "", $"%{search}%"));

if (stageId.HasValue)
    query = query.Where(c => 
        c.CurrentStageId == stageId || 
        c.VisibleInStages.Contains(stageId.Value));
```

---

## 5. SignalR Integration Pattern

### Broadcast on Every Write
```csharp
// In transition handler (after successful commit)
await _broadcaster.BroadcastCandidateUpdateAsync(
    tenantId: _tenantContext.TenantId!.Value,
    officeId: candidate.OfficeId,
    message: new CandidateUpdatedMessage(
        CandidateId: candidate.Id,
        ChangeType: "StageTransitioned",
        Field: "currentStage",
        OldValue: fromStageName,
        NewValue: toStageName,
        ChangedBy: _currentUser.UserName!,
        Timestamp: DateTime.UtcNow));
```

### Frontend SWR Cache Invalidation
```typescript
// In SignalR event handler
useEffect(() => {
  subscribe('candidateUpdated', (msg: CandidateUpdatedMessage) => {
    // Invalidate SWR cache for affected queries
    mutate(`/api/candidates/${msg.candidateId}`);
    mutate(key => typeof key === 'string' && key.includes('/workflow/views/'));
  });
}, [subscribe]);
```

---

## 6. Concurrency Control

### Optimistic Concurrency on Candidate
- `RowVersion` (PostgreSQL `xmin`) prevents lost updates
- If two users transition the same candidate simultaneously:
  - First write succeeds
  - Second write gets `DbUpdateConcurrencyException`
  - Handler retries once (re-derive state, re-validate)
  - If still fails, return 409 Conflict to client

### Event Sequence Uniqueness
- `UNIQUE (candidate_id, sequence_number)` constraint prevents duplicate events
- If two concurrent appends try the same sequence: one succeeds, one gets constraint violation
- Retry with incremented sequence on violation

---

## 7. PBT Test Architecture

### Stateful Model (PBT-06)
```csharp
// Model: simplified workflow state machine
class WorkflowModel {
    Guid CurrentStageId;
    Dictionary<string, string> StatusValues;
    HashSet<Guid> VisibleInStages;
}

// Commands generated by FsCheck
interface IWorkflowCommand { }
record RegisterCmd(string Name, string Passport) : IWorkflowCommand;
record TransitionCmd(Guid TransitionRuleId) : IWorkflowCommand;
record UpdateStatusCmd(string Track, string Value) : IWorkflowCommand;

// Property: for any sequence of valid commands,
// the real system state matches the model state
[Property]
public Property WorkflowEngine_MatchesModel() {
    return Prop.ForAll(
        GenCommandSequence(),
        commands => {
            var model = new WorkflowModel();
            var system = new WorkflowEngineService(...);
            foreach (var cmd in commands) {
                ExecuteOnBoth(cmd, model, system);
                Assert model.State == system.DerivedState;
            }
        });
}
```

### Round-Trip Property (Snapshot)
```csharp
[Property]
public Property Snapshot_RoundTrip() {
    return Prop.ForAll(
        GenEventSequence(minLength: 25), // ensure at least one snapshot
        events => {
            var stateFromAll = ReplayAll(events);
            var (snapshot, tail) = SplitAtSnapshot(events);
            var stateFromSnapshotPlusTail = ReplayFromSnapshot(snapshot, tail);
            return stateFromAll.Equals(stateFromSnapshotPlusTail);
        });
}
```

---

## 8. CV PDF Generation Architecture

### QuestPDF Template Structure
```csharp
public class CandidateCVDocument : IDocument
{
    private readonly CandidateCVData _data;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page => {
            page.Margin(30);
            page.Header().Element(ComposeHeader);    // Name + photo
            page.Content().Element(ComposeContent);  // Bio + details
            page.Footer().Element(ComposeFooter);    // Generated date
        });
    }
    
    // Sections: Personal Info, Contact, Nationality/Travel, 
    // Employment History (future), Languages (future), Skills (future)
}
```

### Generation Flow
```
GenerateCVCommand → Handler:
  1. Load Candidate with all fields
  2. Load photo document (if exists) → read file bytes
  3. Build CandidateCVData DTO
  4. Render via QuestPDF → byte[]
  5. Save PDF to file storage (DocumentType: CV)
  6. Return file bytes for immediate download
```
