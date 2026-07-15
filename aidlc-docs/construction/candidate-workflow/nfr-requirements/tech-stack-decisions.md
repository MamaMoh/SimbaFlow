# Tech Stack Decisions — Unit 2: Candidate & Workflow Engine

## Confirmed Stack (inherited from Unit 1)

All infrastructure established in Unit 1 applies — no new frameworks or major dependencies needed for Unit 2.

| Component | Technology | Notes |
|-----------|-----------|-------|
| ORM | EF Core 10.0 | Candidate + workflow entities in tenant schema |
| CQRS | MediatR 13.0 | Commands and queries for all operations |
| Validation | FluentValidation 12.0 | Request validation for all commands |
| Event Store | Custom (EF Core + PostgreSQL JSONB) | WorkflowEvent table with JSONB Data column |
| Real-time | SignalR (from Unit 1) | Broadcasts on candidate changes |
| File Storage | LocalFileStorageService (from Unit 1) | Candidate documents |
| PDF | QuestPDF | CV generation |
| Search | PostgreSQL full-text + ILIKE | Candidate search across text fields |
| PBT | FsCheck 3.0 | Property-based tests for workflow engine |

## Unit 2-Specific Tech Decisions

### Decision 1: Event Store Implementation
- **Choice**: Custom implementation using EF Core + PostgreSQL (NOT a dedicated event store like EventStoreDB)
- **Rationale**: Self-hosted Docker deployment; no need for distributed event streaming; PostgreSQL JSONB gives flexible event data; simpler operational model; snapshot strategy keeps replay fast
- **Trade-off**: No built-in projections or subscriptions — handled by domain events + denormalized fields

### Decision 2: Workflow State Denormalization
- **Choice**: Denormalize `CurrentStageId`, `CurrentStageName`, `CurrentStatusValues` directly on Candidate entity
- **Rationale**: View queries (candidates-in-stage) must be fast (<300ms for 50K candidates); querying event stream for every candidate on every page load is prohibitive; denormalized state is always consistent because it's updated in the same transaction as the event append
- **Trade-off**: Slight write overhead (update Candidate + append Event in one transaction) for massive read performance gain

### Decision 3: Condition Evaluation Engine
- **Choice**: Custom JSONB condition evaluator with AND/OR support
- **Rationale**: Conditions are simple (field == value, field != value, field in [values]); no need for a full rules engine like Drools; JSONB storage allows admin to configure without schema changes; condition format is easily serializable and displayable in admin UI
- **Trade-off**: Limited to simple boolean logic (no arithmetic, no temporal conditions); if needed later, can extend the evaluator without changing storage

### Decision 4: Snapshot Strategy
- **Choice**: Create snapshot every 20 events per candidate
- **Rationale**: Most candidates will have 10-50 events total (8 stages × ~3-5 events per stage); snapshot at 20 means max 20 events to replay; worst case for long-lived candidates (500+ events) is still 20 event replay
- **Frequency**: Evaluated after every event append; low overhead (one INSERT every 20 events)

### Decision 5: Full-Text Search
- **Choice**: PostgreSQL ILIKE with GIN index on tsvector (for advanced queries later)
- **Rationale**: Self-hosted; no Elasticsearch dependency; PostgreSQL full-text is sufficient for 50K candidates; ILIKE for simple partial matches, tsvector for weighted relevance (future)
- **Initial implementation**: ILIKE on (FirstName, LastName, PassportNumber, LabourId) with OR
- **Future path**: GIN index on tsvector column combining all searchable fields

### Decision 6: PDF Generation Library
- **Choice**: QuestPDF (community license)
- **Rationale**: Fluent C# API (no HTML-to-PDF conversion); fast rendering; supports images, tables, layouts; MIT license for community edition; works in Linux Docker containers
- **Usage**: CV generation with candidate photo + bio data + standardized template

### Decision 7: Frontend State Management for Workflow Views
- **Choice**: SWR for server state + Zustand for filter/UI state
- **Rationale**: Existing pattern from the codebase; SWR handles cache invalidation on SignalR updates (mutate); Zustand manages which filters are active, which stage is selected
- **SignalR integration**: On `candidateUpdated` event → call `mutate()` to refetch affected queries

### Decision 8: Optimistic UI for Action Buttons
- **Choice**: Client evaluates conditions locally AND server re-validates on execute
- **Rationale**: Better UX (buttons enable instantly when user changes a field); server is authoritative (security); client gets condition rules from workflow definition (already loaded)
- **Implementation**: Frontend holds the workflow definition in Zustand store; evaluates conditions against local candidate state on every field change
