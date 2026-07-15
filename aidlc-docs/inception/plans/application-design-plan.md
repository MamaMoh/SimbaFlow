# Application Design Plan

## Execution Plan

- [x] Step 1: Define high-level component architecture
- [x] Step 2: Define domain components (entities, aggregates)
- [x] Step 3: Define service layer (application services, domain services)
- [x] Step 4: Define infrastructure components (new services needed)
- [x] Step 5: Define API layer components (Carter modules)
- [x] Step 6: Define frontend components (pages, feature modules)
- [x] Step 7: Define component dependencies and communication patterns
- [x] Step 8: Generate all design artifacts

---

## Clarifying Questions

## Question 1
For the configurable workflow engine, how should the state machine be architecturally modeled?

A) Generic engine — A single WorkflowEngine service that evaluates rules from database config tables. All stages use the same code path, behavior driven by data.

B) Strategy pattern — A generic engine with pluggable strategies per stage type (e.g., ParallelTrackStage, SequentialMilestoneStage, SimpleTransferStage) for specialized behavior.

C) Event-sourced — Workflow transitions stored as an event stream, current state derived from replaying events. Full auditability and temporal queries.

D) Other (please describe after [Answer]: tag below)

[Answer]: C

---

## Question 2
For the double-entry accounting system, should it be a standalone bounded context or tightly integrated with candidate workflow?

A) Standalone bounded context — Separate accounting module with its own aggregates (Account, JournalEntry, Ledger). Workflow triggers financial events via domain events. Clean separation.

B) Integrated — Financial entities live alongside workflow entities. Commission records directly reference candidate records. Tighter coupling but simpler queries.

C) Hybrid — Core accounting is standalone (double-entry journal), but commission-specific logic integrates with candidate lifecycle. Bridge via domain events.

D) Other (please describe after [Answer]: tag below)

[Answer]: C

---

## Question 3
For the Telegram/WhatsApp bot, how should it be architecturally deployed?

A) In-process — Bot logic runs inside the same .NET API process. Simpler deployment, shared database access.

B) Separate service — Bot runs as a separate container/process in Docker Compose. Communicates with main API via HTTP/gRPC. Independent scaling and deployment.

C) Hybrid — Bot message handling in-process, but long-running polling/webhook receiver as a separate background service within the same process.

D) Other (please describe after [Answer]: tag below)

[Answer]: C

---

## Question 4
For real-time updates (SignalR), what scope of updates should be pushed?

A) Stage transitions only — Push notifications only when a candidate moves to a new stage.

B) All status changes — Push on any candidate field update (status, medical result, payment, etc.)

C) Configurable — Notification Manager configures which events trigger real-time pushes per role/user.

D) Other (please describe after [Answer]: tag below)

[Answer]: B

---

## Question 5
For the schema-per-tenant PostgreSQL isolation, how should cross-tenant operations (system admin) work?

A) Admin connects to each schema sequentially — When admin needs to see all tenants, queries run against each schema in sequence and results are merged.

B) Public schema for cross-tenant — A shared "public" schema holds tenant metadata and system-wide config. Admin queries use this plus targeted schema access.

C) Admin schema with views — A dedicated admin schema with foreign-data-wrapper or views that span tenant schemas for consolidated reporting.

D) Other (please describe after [Answer]: tag below)

[Answer]: B

---
