# Service Layer Design

## Service Architecture

Services are organized into three tiers:
1. **Domain Services** — Business logic that spans multiple aggregates
2. **Application Services** — Orchestration via MediatR handlers (CQRS)
3. **Infrastructure Services** — External concerns (DB, files, APIs, messaging)

---

## Domain Services

### DS-01: WorkflowEngineService
- **Purpose**: Core workflow state machine evaluation
- **Responsibilities**:
  - Evaluate transition preconditions (field values, role permissions)
  - Compute available actions for a candidate+user combination
  - Apply mirror view rules (determine multi-view visibility)
  - Validate parallel track completion conditions
  - Append workflow events to event store
  - Rebuild current state from event stream
- **Consumes**: WorkflowConfiguration, WorkflowEventStore
- **Emits**: WorkflowTransitioned, CandidateStageChanged, MirrorViewActivated domain events

### DS-02: AccountingService
- **Purpose**: Double-entry bookkeeping enforcement
- **Responsibilities**:
  - Validate journal entries balance (total debits = total credits)
  - Apply exchange rate conversions
  - Calculate account balances (debit-normal vs credit-normal)
  - Generate trial balance, P&L, balance sheet from journal entries
  - Enforce fiscal period rules
- **Consumes**: Account repository, JournalEntry repository, ExchangeRate repository
- **Emits**: JournalEntryPosted domain event

### DS-03: CommissionCalculationService
- **Purpose**: Bridge between candidate lifecycle and accounting
- **Responsibilities**:
  - Calculate fee breakdowns based on agency fee structure
  - Create accounting journal entries for new commissions
  - Create accounting journal entries for payments received
  - Handle currency conversions for cross-currency payments
  - Trigger dispute-related adjustments
- **Consumes**: Commission records, AccountingService, ExchangeRate
- **Emits**: CommissionInitialized, PaymentRecorded, DisputeLogged domain events

### DS-04: NotificationDispatchService
- **Purpose**: Evaluate notification rules and dispatch to appropriate channels
- **Responsibilities**:
  - Match domain events to notification rules
  - Resolve target users/roles for each rule
  - Select message template and language
  - Route to appropriate channel (SignalR, Telegram, WhatsApp, Email)
  - Track delivery status
- **Consumes**: NotificationRules, UserPreferences, TemplateEngine
- **Triggers on**: All domain events (listens via DomainEventDispatcher)

---

## Application Services (MediatR Pipeline)

### Pipeline Behaviors (Execution Order)

1. **ValidationBehavior** — FluentValidation on every request
2. **AuthorizationBehavior** — Permission check (IRequirePermission)
3. **WorkflowAuthorizationBehavior** — Office/stage access check (replaces ClinicalAuthorizationBehavior)
4. **PerformanceLogBehavior** — Slow request detection
5. **AuditBehavior** — Write audit logging

### AS-01: Command Handlers (Write Operations)
- One handler per command (CQRS write side)
- Handler orchestrates: validate → load aggregate → apply business logic → persist → emit events
- Pattern: `IRequestHandler<TCommand, Result<T>>`

### AS-02: Query Handlers (Read Operations)
- One handler per query (CQRS read side)
- Direct EF Core queries with projections (no domain logic)
- Pattern: `IRequestHandler<TQuery, Result<T>>`

### AS-03: Domain Event Handlers
- React to domain events asynchronously
- Trigger side effects: notifications, accounting entries, SignalR broadcasts
- Pattern: `INotificationHandler<TEvent>`

---

## Infrastructure Services

### IS-01: TenantSchemaResolver
- **Purpose**: Resolve PostgreSQL schema name from current request context
- **Pattern**: Scoped service, reads TenantId from authenticated JWT claims
- **Implementation**: Sets `SET search_path TO '{tenantSchema}', public` on DbContext connection

### IS-02: WorkflowEventStore (EF Core Implementation)
- **Purpose**: Persist workflow events in PostgreSQL
- **Implementation**: `WorkflowEvent` table with: Id, CandidateId, EventType, EventData (JSONB), Timestamp, UserId, SequenceNumber
- **Snapshotting**: Periodic snapshot every N events for performance

### IS-03: SignalRNotificationHub
- **Purpose**: WebSocket hub for real-time pushes
- **Groups**: Users grouped by `tenant:{tenantId}:office:{officeId}` for scoped broadcasts
- **Integration**: Domain event handlers call `IHubContext<NotificationHub>` to push updates

### IS-04: TelegramBotHostedService
- **Purpose**: Background service for Telegram long-polling
- **Pattern**: `BackgroundService` with cancellation token
- **Message Flow**: Telegram → Parse command → Create MediatR command → Send → Format response → Reply

### IS-05: WhatsAppWebhookService
- **Purpose**: Webhook endpoint for WhatsApp incoming messages
- **Pattern**: Carter module endpoint + message parsing + MediatR dispatch

### IS-06: FileStorageService
- **Purpose**: File system operations for document management
- **Pattern**: Interface `IFileStorageService` with local filesystem implementation
- **Path Structure**: `/data/tenants/{tenantId}/candidates/{candidateId}/{filename}`

### IS-07: PdfGenerationService
- **Purpose**: PDF rendering from templates
- **Library**: QuestPDF or similar .NET PDF library
- **Templates**: CV template, invoice template, report template, financial statement template

### IS-08: ExcelExportService
- **Purpose**: Excel file generation
- **Library**: ClosedXML or EPPlus
- **Pattern**: Accept data + column definitions → produce .xlsx stream

### IS-09: ScheduledReportHostedService
- **Purpose**: Background service checking report schedules
- **Pattern**: `BackgroundService` with timer, evaluates schedule rules, generates and emails reports

### IS-10: EmailService
- **Purpose**: Send emails (scheduled reports, notifications)
- **Pattern**: Interface `IEmailService` with SMTP implementation

---

## Service Interaction Patterns

### Pattern 1: Workflow Transition (Core Flow)
```
User Action → Carter Module → MediatR Command
  → ValidationBehavior (validate fields)
  → AuthorizationBehavior (check permission)  
  → WorkflowAuthorizationBehavior (check office/stage access)
  → Handler:
    1. Load workflow config for tenant
    2. WorkflowEngineService.EvaluateTransition()
    3. Append WorkflowEvent to EventStore
    4. Update candidate denormalized state
    5. Emit CandidateStageChanged domain event
  → AuditBehavior (log operation)
  
Domain Event → NotificationDispatchService:
  → Evaluate notification rules
  → SignalR broadcast to connected users
  → Telegram/WhatsApp push (if configured)
  
Domain Event → CommissionCalculationService (if financial trigger):
  → Create/update commission record
  → Post journal entries via AccountingService
```

### Pattern 2: Bot Command
```
Telegram Message → TelegramBotHostedService
  → Parse command + extract parameters
  → Resolve user from linked bot account
  → Create MediatR query/command
  → Send through pipeline (same validation/auth/audit)
  → Format response in user's language
  → Reply via Telegram API
```

### Pattern 3: Real-Time Update
```
Any Write Handler completes
  → Domain event emitted
  → SignalRNotificationHandler receives event
  → Resolve affected tenant + office scope
  → IHubContext.Clients.Group("tenant:X:office:Y").SendAsync("candidateUpdated", payload)
  → All connected frontends receive update
  → Frontend updates local state (SWR mutation / Zustand store)
```
