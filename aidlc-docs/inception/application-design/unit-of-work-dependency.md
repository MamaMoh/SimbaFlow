# Unit of Work Dependencies

## Dependency Graph

```mermaid
flowchart TD
    PRE["Pre-Unit<br/>Clinical Code Deletion"]
    U1["Unit 1<br/>Core Infrastructure"]
    U2["Unit 2<br/>Candidate & Workflow"]
    U3["Unit 3<br/>Embassy & LMIS"]
    U4["Unit 4<br/>Travel & Arrival"]
    U5["Unit 5<br/>Finance & Commission"]
    U6["Unit 6<br/>Agency ERP"]
    U7["Unit 7<br/>Bot & Notifications"]
    U8["Unit 8<br/>Reporting & Analytics"]

    PRE --> U1
    U1 --> U2
    U2 --> U3
    U3 --> U4
    U2 --> U5
    U4 --> U5
    U1 --> U6
    U2 --> U6
    U1 --> U7
    U2 --> U7
    U2 --> U8
    U5 --> U8
    U6 --> U8

    style PRE fill:#EF5350,stroke:#B71C1C,stroke-width:3px,color:#fff
    style U1 fill:#42A5F5,stroke:#1565C0,stroke-width:3px,color:#fff
    style U2 fill:#66BB6A,stroke:#2E7D32,stroke-width:3px,color:#fff
    style U3 fill:#FFA726,stroke:#E65100,stroke-width:3px,color:#000
    style U4 fill:#FFA726,stroke:#E65100,stroke-width:3px,color:#000
    style U5 fill:#AB47BC,stroke:#6A1B9A,stroke-width:3px,color:#fff
    style U6 fill:#78909C,stroke:#37474F,stroke-width:3px,color:#fff
    style U7 fill:#26C6DA,stroke:#00838F,stroke-width:3px,color:#000
    style U8 fill:#FFCA28,stroke:#F57F17,stroke-width:3px,color:#000
```

## Dependency Matrix

| Unit | Depends On | Depended By | Dependency Type |
|------|-----------|-------------|-----------------|
| Pre (Deletion) | — | U1 | Must complete first |
| U1 (Core Infra) | Pre | U2, U6, U7 | Foundation (schema, SignalR, Docker) |
| U2 (Candidate & Workflow) | U1 | U3, U4, U5, U6, U7, U8 | Core domain (all units use candidates/workflow) |
| U3 (Embassy & LMIS) | U2 | U4 | Workflow stages (LMIS → Ticket transition) |
| U4 (Travel & Arrival) | U2, U3 | U5 | Arrival triggers commission |
| U5 (Finance) | U2, U4 | U8 | Financial data for reports |
| U6 (Agency ERP) | U1, U2 | U8 | Office/staff data for reports |
| U7 (Bot & Notifications) | U1, U2 | — | Listens to all domain events |
| U8 (Reporting) | U2, U5, U6 | — | Aggregates data from all sources |

## Critical Path

```
Pre → U1 → U2 → U3 → U4 → U5 → (U6, U7, U8 can be parallel after U5)
```

**Strictly sequential execution** (per user decision), so actual order:
```
Pre → U1 → U2 → U3 → U4 → U5 → U6 → U7 → U8
```

## Integration Points Between Units

### U1 → U2: Infrastructure Contracts
- `ITenantSchemaResolver` — Unit 2 uses this to scope all queries
- `IFileStorageService` — Unit 2 uses for candidate document uploads
- `ISignalRNotificationService` — Unit 2 broadcasts candidate updates
- Docker Compose base config — Unit 2 adds migrations

### U2 → U3: Workflow Engine
- `IWorkflowEngineService` — Unit 3 uses to evaluate embassy/LMIS transitions
- `WorkflowEvent` table — Unit 3 appends embassy/LMIS-specific events
- Candidate queries — Unit 3 filters candidates by embassy/LMIS stage

### U2 → U5: Domain Events
- `CandidateArrived` event — Unit 5 listens to initialize commission
- `Candidate` entity — Unit 5 references for commission records
- `WorkflowEvent` stream — Unit 5 uses for audit trail of financial triggers

### U3 → U4: Stage Transitions
- "To Ticket" transition — Unit 4 receives candidates from LMIS stage
- Workflow configuration — Unit 4's stages defined in same workflow config

### U4 → U5: Financial Triggers
- `CandidateArrived` event — Triggers commission initialization
- `ExceptionResolved` event — Triggers financial adjustments
- `LiabilityAssigned` event — Creates accounting entries

### U1, U2 → U7: Event Broadcasting
- All domain events — Unit 7 listens to ALL events for notification dispatch
- SignalR hub — Unit 7 configures which events broadcast to which groups
- Candidate data — Bot uses for status lookups

### U2, U5, U6 → U8: Data Aggregation
- Candidate/Workflow data — Pipeline reports, overdue detection
- Financial data — Financial summary, commission reports
- Office data — Office comparisons, performance metrics

## Shared Resources

| Resource | Owner Unit | Consumers |
|----------|-----------|-----------|
| PostgreSQL (public schema) | U1 | All units |
| PostgreSQL (tenant schemas) | U1 | All units |
| SignalR Hub | U1 | U2-U8 (all broadcast through it) |
| File System Volume | U1 | U2 (documents), U8 (report exports) |
| WorkflowEngineService | U2 | U3, U4 (stage transitions) |
| Domain Event Dispatcher | U1 (existing) | U5, U7 (event consumers) |
| MediatR Pipeline | U1 (existing) | All units (all handlers) |
