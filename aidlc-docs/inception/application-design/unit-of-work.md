# Units of Work

## Execution Strategy
- **Sequential execution**: Complete each unit fully (design → code → test) before starting next
- **Vertical slices**: Each unit includes backend + frontend (full stack)
- **Clean slate first**: Delete all clinical/medical code before Unit 1 begins
- **Monolithic deployment**: Single .NET API + Next.js frontend + PostgreSQL in Docker Compose

---

## Pre-Unit: Clinical Code Deletion

**Purpose**: Remove all hospital/medical domain code to create a clean foundation.

**Scope**:
- Delete: All clinical domain entities (Patient, Encounter, Vitals, Notes, Diagnoses, Orders, Lab, Imaging, Pharmacy, Billing)
- Delete: All clinical API feature modules (ClinicalWorkspace, Laboratory, Imaging, Pharmacy, Billing, Orders, Inpatient, DiagnosisCodes, MedicalHistory, Scheduling)
- Delete: All clinical frontend pages and components
- Delete: All clinical-specific migrations
- Delete: Clinical enums (ClinicalEnums, OrderEnums, MedicalHistoryEnums, SchedulingEnums, AppointmentStatus)
- Delete: Clinical infrastructure services (EncounterChargeService, ClinicalAlerts)
- Delete: ClinicalAuthorizationBehavior
- **Keep**: Auth module, Identity entities, Staff entities, Location entities (adapt), BaseEntity, Result pattern, all pipeline behaviors (except Clinical), DomainEvents infrastructure, Audit services, Background jobs, ServiceExtensions structure, Program.cs structure, Frontend auth shell, layout, UI components

**Deliverable**: Clean codebase with working auth, RBAC, audit trail, and empty feature area ready for new domain.

---

## Unit 1: Core Infrastructure

**Purpose**: Establish the foundational infrastructure that all other units depend on.

**Scope**:
- Schema-per-tenant database architecture (TenantSchemaDbContext, TenantSchemaResolver)
- Public schema for tenant metadata, exchange rates, system config
- Tenant provisioning (create schema, seed default data)
- WorkflowAuthorizationBehavior (replaces ClinicalAuthorizationBehavior)
- SignalR hub infrastructure (connection management, group management)
- File storage service setup (Docker volume mount, upload/download)
- Updated DI registration (ServiceExtensions)
- Docker Compose configuration (API + PostgreSQL + Next.js containers)
- Frontend: SignalR connection provider, tenant context provider, updated navigation shell

**Stories Covered**: US-11.01 through US-11.06 (cross-cutting), US-8.07 (tenant provisioning)

**Key Entities**: TenantInfo (adapt), TenantSchema, SystemConfiguration

**Dependencies**: None (foundation unit)

---

## Unit 2: Candidate & Workflow Engine

**Purpose**: Core domain — candidate management and the event-sourced configurable workflow engine.

**Scope**:
- Candidate aggregate (registration, CRUD, documents, CV generation, timeline)
- Workflow event store (append events, replay state, snapshots)
- Workflow configuration (stages, statuses, transitions, rules, parallel tracks, mirror views)
- Workflow engine service (evaluate transitions, compute available actions, enforce rules)
- Default 8-stage workflow template seeder
- API: CandidateModule, WorkflowModule
- Frontend: Candidate list/detail/registration pages, workflow configuration admin pages, generic workflow view component

**Stories Covered**: US-1.01 through US-1.10 (Candidate), US-2.01 through US-2.10 (Workflow Engine)

**Key Entities**: Candidate, CandidateDocument, WorkflowDefinition, WorkflowStage, WorkflowStatus, WorkflowTransitionRule, WorkflowEvent, WorkflowSnapshot, ParallelTrackConfig, MirrorViewRule

**Dependencies**: Unit 1 (schema-per-tenant, file storage, SignalR)

---

## Unit 3: Embassy & LMIS Processing

**Purpose**: Embassy clearance workflow (medical, Tasheer, visa) and LMIS government registration.

**Scope**:
- Embassy stage logic (medical tracking, Tasheer tracking, parallel track evaluation)
- Mirror view activation (Medical=Fit + Tasheer=BookDone → LMIS visibility)
- Case Executive view and handoff
- Visa outcome tracking (Issued/Rejected/Resubmission)
- LMIS stage logic (insurance, milestone progression: Uploaded → Verified → Issued)
- Transfer actions ("To Embassy", "To LMIS", "To Ticket")
- API: EmbassyModule, LmisModule
- Frontend: Embassy View page, Case Executive View page, LMIS View page

**Stories Covered**: US-3.01 through US-3.11 (Embassy), US-4.01 through US-4.05 (LMIS)

**Key Entities**: (Workflow events for embassy/LMIS transitions — no separate entities needed; tracked via WorkflowEvent with JSONB data for medical status, Tasheer status, insurance status, LMIS milestone)

**Dependencies**: Unit 2 (Workflow Engine, Candidate)

---

## Unit 4: Travel, Departure & Arrival

**Purpose**: Travel logistics, departure countdown, arrival confirmation, and exception handling.

**Scope**:
- Ticket booking (ticket status, destination, flight date)
- Departure countdown (remaining days calculation, notification alerts)
- Departure/non-departure handling (success path + "Back to Ticket" + "Canceled")
- Arrival confirmation and permanent arrival ledger
- Exception containment workspace (Returned/Runaway → Investigation → Resolution → Closed)
- Financial impact tracking for exceptions
- Transfer actions ("To Departure", "To Arrival")
- API: TravelModule, ArrivalModule, ExceptionModule
- Frontend: Ticket View page, Departure View page (countdown), Arrival View page (ledger), Exception Containment workspace

**Stories Covered**: US-5.01 through US-5.08 (Travel), US-6.01 through US-6.07 (Arrival)

**Key Entities**: ExceptionCase, InvestigationNote, LiabilityAssignment (all stored in tenant schema; travel/departure/arrival data tracked via WorkflowEvent JSONB)

**Dependencies**: Unit 2 (Workflow Engine), Unit 3 (LMIS transfers to Ticket)

---

## Unit 5: Finance & Commission (ERP)

**Purpose**: Double-entry accounting system and commission management bridging candidate lifecycle.

**Scope**:
- Chart of Accounts (asset, liability, revenue, expense, equity accounts)
- Journal entries (double-entry with debit/credit enforcement)
- Multi-currency support with exchange rate tracking
- Commission records (fee breakdown, payment tracking, dispute management)
- Domain event handlers: CandidateArrived → InitializeCommission, PaymentReceived → PostJournalEntry
- Bank reconciliation
- Financial statements (P&L, Balance Sheet, Trial Balance)
- Tax calculations
- API: CommissionModule, AccountingModule
- Frontend: Commission View page, Payment recording, Bank reconciliation page, Financial statements page

**Stories Covered**: US-7.01 through US-7.09 (Commission & Finance)

**Key Entities**: Account, JournalEntry, JournalLine, Commission, CommissionFee, Payment, Dispute, ExchangeRate, FiscalPeriod, TaxRate, BankReconciliation

**Dependencies**: Unit 2 (Candidate — domain events), Unit 4 (Arrival triggers commission)

---

## Unit 6: Agency ERP (Staff, Office, Partners, Admin)

**Purpose**: Agency operational management — employees, offices, partners, roles, dashboard.

**Scope**:
- Staff/employee management (adapt existing StaffProfile patterns)
- Office/branch CRUD with hierarchical structure
- Partner agency/employer directory
- Role and permission management (adapt existing, add new labour export permissions)
- KPI dashboard (pipeline funnel, trend charts, quick metrics)
- Audit trail viewer (existing audit data, new UI)
- API: OfficeModule, StaffModule (adapt), DashboardModule
- Frontend: Staff management pages, Office management page, Partner directory page, Dashboard/overview page, Audit trail viewer

**Stories Covered**: US-8.01 through US-8.06 (ERP), US-10.01 (Pipeline view — dashboard)

**Key Entities**: Office, PartnerAgency, (StaffProfile adapted from existing), Permission (new permissions added)

**Dependencies**: Unit 1 (Tenant, RBAC), Unit 2 (Candidate counts for dashboard)

---

## Unit 7: Bot & Notifications

**Purpose**: Telegram/WhatsApp bot integration, notification rule engine, and SignalR real-time broadcasting.

**Scope**:
- Telegram Bot hosted service (long-polling, command parsing, response formatting)
- WhatsApp Business API integration (webhook receiver, message sending)
- Bot user registration and linking to system accounts
- Bot commands: /status, /medical, /arrived, /cv, /lang, /register
- Multi-language response templates (English + Amharic)
- Notification rule engine (event type → channels → roles → templates)
- Notification configuration admin UI
- Delivery status tracking
- SignalR event broadcasting (connect all domain events to SignalR hub)
- API: NotificationModule (config), Bot webhook endpoint
- Frontend: Notification configuration page, Bot configuration page, Delivery monitoring

**Stories Covered**: US-9.01 through US-9.09 (Bot), US-11.03 (SignalR real-time)

**Key Entities**: NotificationRule, NotificationTemplate, NotificationDelivery, BotUser, BotSession

**Dependencies**: Unit 1 (SignalR infrastructure), Unit 2 (Candidate queries for status lookup), Unit 3-4 (Events to broadcast)

---

## Unit 8: Reporting & Analytics

**Purpose**: Comprehensive reporting with multiple export formats and scheduled delivery.

**Scope**:
- Pipeline report (candidates per stage with funnel visualization)
- Agency performance dashboard (processing times, success rates, trends)
- Office comparison report (side-by-side metrics)
- Overdue/stuck candidates detection and alerting
- Financial summary reports
- Excel export service (ClosedXML)
- PDF export service (QuestPDF with agency branding)
- Scheduled report configuration and background execution
- Email delivery of generated reports
- API: ReportModule
- Frontend: Report viewer pages, Export buttons, Scheduled report configuration page

**Stories Covered**: US-10.01 through US-10.07 (Reporting), US-8.06 partial (KPIs detailed)

**Key Entities**: ScheduledReport, ReportExecution, ReportTemplate

**Dependencies**: Unit 2 (Candidate/Workflow data), Unit 5 (Financial data), Unit 6 (Office data)

---

## Summary

| Unit | Name | Key Focus | Entities | Stories |
|------|------|-----------|----------|---------|
| Pre | Clinical Deletion | Remove medical code | — | — |
| 1 | Core Infrastructure | Schema-per-tenant, SignalR, Docker | TenantSchema | 7 |
| 2 | Candidate & Workflow | Core domain + event store | Candidate, WorkflowEvent, WorkflowConfig | 20 |
| 3 | Embassy & LMIS | Clearance workflow + gov registration | (via WorkflowEvent) | 16 |
| 4 | Travel & Arrival | Logistics + exceptions | ExceptionCase | 15 |
| 5 | Finance & Commission | Double-entry + commissions | Account, JournalEntry, Commission | 9 |
| 6 | Agency ERP | Staff, offices, dashboard | Office, PartnerAgency | 7 |
| 7 | Bot & Notifications | Telegram/WhatsApp + rules engine | NotificationRule, BotUser | 10 |
| 8 | Reporting | Reports, exports, scheduling | ScheduledReport | 7 |
| **Total** | | | | **~91 stories** |
