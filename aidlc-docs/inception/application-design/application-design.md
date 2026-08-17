# Application Design — Consolidated

## Architecture Summary

SimbaFlow is redesigned as a **labour export agency management platform** using:
- **Event-Sourced Workflow Engine** — Candidate transitions stored as immutable events, current state derived from replay
- **Hybrid Accounting** — Standalone double-entry bookkeeping bridged to candidate lifecycle via domain events
- **Schema-Per-Tenant Isolation** — Each agency gets a dedicated PostgreSQL schema; public schema for metadata
- **Real-Time Updates** — All candidate status changes broadcast via SignalR to connected users
- **Bot Integration** — Telegram/WhatsApp as in-process hybrid (handlers in-process, polling as background service)

## Key Architectural Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Workflow Engine | Event-Sourced | Full auditability, temporal queries, immutable history |
| Accounting | Hybrid bounded context | Clean separation with domain event bridge |
| Bot Deployment | In-process hybrid | Simpler Docker Compose, shared DB access |
| SignalR Scope | All status changes | Maximum real-time visibility |
| Cross-Tenant | Public schema | Tenant metadata + **shared Partner catalog** for Art. 40 |

## High-Level Component Map

### Domain Components (9)
1. **Candidate Aggregate** — Core entity with identity/biographical data
2. **Workflow Engine** — Event-sourced state machine
3. **Workflow Configuration** — Per-agency stage/status/transition definitions
4. **Accounting** — Double-entry bookkeeping (standalone)
5. **Commission Bridge** — Links candidate lifecycle to accounting
6. **Agency/Tenant** — Multi-tenant representation with offices/partners
7. **Staff/Employee** — Employee management (adapted from existing)
8. **Exception Containment** — Returned/Runaway handling workspace
9. **Notification** — Rule engine for multi-channel notifications

### Infrastructure Services (10)
1. **TenantSchemaDbContext** — Dynamic schema resolution per request
2. **WorkflowEventStore** — Append-only event persistence with snapshots
3. **SignalR Notification Hub** — WebSocket real-time broadcasting
4. **Telegram Bot Service** — Background long-polling + command handling
5. **WhatsApp Bot Service** — Webhook receiver + command handling
6. **PDF Generation Service** — CV, report, statement generation
7. **Excel Export Service** — Report data to .xlsx
8. **Scheduled Report Service** — Background report generation + email
9. **File Storage Service** — Local filesystem document management
10. **Email Service** — SMTP delivery for reports and notifications

### API Modules (18 Carter modules)
Auth, Candidate, Workflow, Embassy, LMIS, Travel, Arrival, Exception, Commission, Accounting, Office, Partner, Staff, User, Role, Notification, Report, Tenant, Dashboard

### Frontend Components (12 feature areas)
Auth Shell, Main Layout, Dashboard, Candidates, Workflow Views (7 views), Workflow Config (Admin), Finance, Exceptions, Staff/Office, Reports, Notification/Bot Config, Admin

## Service Interaction Summary

**Core Flow**: User Action → API → MediatR Pipeline → Handler → WorkflowEngineService → EventStore → Domain Events → (NotificationService + SignalR + Accounting)

**Bot Flow**: Message → BotService → MediatR Pipeline → Same handlers → Same side effects

**Financial Flow**: Domain Event (CandidateArrived) → CommissionService → AccountingService → JournalEntry

## Bounded Contexts

| Context | Aggregates | Communication |
|---------|-----------|---------------|
| Candidate & Workflow | Candidate, WorkflowInstance, WorkflowEvent, WorkflowConfig | Core — other contexts listen to its events |
| Finance | Account, JournalEntry, Commission, Dispute, ExchangeRate | Consumes candidate events, produces payment events |
| ERP | Tenant (+ license/level), Office, PartnerAgency (public), PartnerLink, Staff, Role, Permission | Provides reference data consumed by all |
| Notification | NotificationRule, Template, DeliveryRecord, BotUser | Listens to ALL domain events, dispatches notifications |
| Exception | ExceptionCase, InvestigationNote, LiabilityAssignment | Sub-domain of Candidate; triggers financial adjustments |

**Integration Rule**: Bounded contexts communicate exclusively via domain events. No cross-context database joins.

## Domain decision addendum (2026-07-22)

Partner agencies and tenant MoLS licensing: see  
`aidlc-docs/inception/requirements/partner-agency-and-tenant-licensing.md`  
(Shared public catalog + tenant PartnerLinks; agency levels 1–5; Art. 40 capacity.)

## Technology Additions (Beyond Existing Stack)

| Component | Library/Technology | Purpose |
|-----------|-------------------|---------|
| SignalR | Microsoft.AspNetCore.SignalR | Real-time WebSocket |
| PDF Generation | QuestPDF | PDF document creation |
| Excel Export | ClosedXML | .xlsx generation |
| Telegram | Telegram.Bot NuGet | Telegram Bot API client |
| WhatsApp | HTTP client to Business API | WhatsApp integration |
| Email | MailKit | SMTP email delivery |
| Event Store | Custom EF Core (PostgreSQL JSONB) | Workflow event persistence |
| Schema Resolution | Dynamic EF Core connection | Per-tenant schema isolation |
