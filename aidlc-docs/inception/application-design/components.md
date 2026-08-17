# Application Components

## Component Architecture Overview

The system follows Clean Architecture with an event-sourced workflow engine at its core. Components are organized by bounded context.

---

## Domain Layer Components

### DC-01: Candidate Aggregate
- **Purpose**: Core entity representing a labour export candidate throughout their lifecycle
- **Responsibilities**:
  - Store candidate biographical/identity data
  - Maintain document references
  - Track current workflow position (stage + status)
  - Emit domain events on state changes
- **Bounded Context**: Candidate Management

### DC-02: Workflow Engine (Event-Sourced)
- **Purpose**: Configurable state machine managing candidate progression through stages
- **Responsibilities**:
  - Store workflow transitions as an event stream
  - Derive current state from event replay
  - Evaluate transition rules (field conditions, role permissions)
  - Support parallel tracks within stages
  - Generate available actions per record
  - Enforce mandatory field requirements before transitions
- **Bounded Context**: Workflow
- **Key Aggregates**: WorkflowDefinition, WorkflowInstance, WorkflowEvent

### DC-03: Workflow Configuration
- **Purpose**: Per-agency workflow definition (stages, statuses, transitions, rules)
- **Responsibilities**:
  - Store stage definitions with ordering
  - Store status values per stage
  - Store transition rules with conditions
  - Store action button configurations
  - Store parallel track definitions
  - Store mirror view rules
  - Store mandatory field rules per transition
- **Bounded Context**: Workflow

### DC-04: Accounting (Standalone Bounded Context)
- **Purpose**: Double-entry bookkeeping system
- **Responsibilities**:
  - Maintain chart of accounts (assets, liabilities, revenue, expenses, equity)
  - Record journal entries (debit/credit pairs)
  - Enforce accounting equation (Assets = Liabilities + Equity)
  - Support multi-currency with exchange rate tracking
  - Generate trial balance, P&L, balance sheet
- **Bounded Context**: Finance
- **Key Aggregates**: Account, JournalEntry, FiscalPeriod

### DC-05: Commission Bridge
- **Purpose**: Connect candidate lifecycle events to accounting entries
- **Responsibilities**:
  - Define fee structures per agency/office
  - Calculate commission breakdowns (agency fee, gov fee, medical, ticket, insurance)
  - Create journal entries on financial events (fee defined, payment received, dispute)
  - Track payment status per candidate
  - Handle disputes and adjustments
- **Bounded Context**: Finance (bridges to Candidate via domain events)

### DC-06: Agency/Tenant
- **Purpose**: Multi-tenant agency representation + MoLS licensing + partner catalog/links
- **Responsibilities**:
  - Store agency metadata (name, contact, SaaS subscription status)
  - Store MoLS license metadata (agency level 1–5, license number/dates/status, licensed countries)
  - Maintain office/branch hierarchy (ቅርንጫፍ — registering branch)
  - Maintain **platform PartnerAgency catalog** (public) and **tenant PartnerLinks** (agreements)
  - Hold tenant-level configuration
- **Bounded Context**: ERP
- **Reference**: `inception/requirements/partner-agency-and-tenant-licensing.md`

### DC-07: Staff/Employee
- **Purpose**: Agency employee management (reusing existing StaffProfile pattern)
- **Responsibilities**:
  - Store employee details and role assignments
  - Track office assignments
  - Manage employment status
- **Bounded Context**: ERP (reuse existing Identity + Staff infrastructure)

### DC-08: Exception Containment
- **Purpose**: Manage Returned/Runaway cases with dedicated workflow
- **Responsibilities**:
  - Track investigation lifecycle (Open → Under Investigation → Resolved → Closed)
  - Store investigation notes and documents
  - Track liability assignment
  - Calculate financial impact
  - Trigger financial adjustments on resolution
- **Bounded Context**: Candidate Management (sub-domain)

### DC-09: Notification
- **Purpose**: Notification rule engine and delivery tracking
- **Responsibilities**:
  - Store notification rules (which events → which channels → which users)
  - Track delivery status
  - Support multi-language templates (English/Amharic)
  - Manage user channel preferences
- **Bounded Context**: Notification

---

## Infrastructure Layer Components

### IC-01: TenantSchemaDbContext
- **Purpose**: EF Core DbContext with dynamic schema resolution per request
- **Responsibilities**:
  - Resolve tenant schema from authenticated user's TenantId
  - Set PostgreSQL search_path per connection
  - Apply schema-scoped migrations
  - Provide public schema access for cross-tenant metadata

### IC-02: WorkflowEventStore
- **Purpose**: Persist and replay workflow events
- **Responsibilities**:
  - Append workflow events to event stream (append-only)
  - Replay events to derive current state
  - Support snapshotting for performance
  - Provide temporal queries (state at any point in time)
  - Integration with EF Core (events stored in PostgreSQL)

### IC-03: SignalR Notification Hub
- **Purpose**: Real-time WebSocket communication with connected clients
- **Responsibilities**:
  - Maintain user connections grouped by tenant + office
  - Push all candidate status changes to connected users
  - Push stage transition notifications
  - Support reconnection with missed-event delivery

### IC-04: Telegram Bot Service (Background)
- **Purpose**: Handle Telegram Bot API communication
- **Responsibilities**:
  - Long-polling for incoming messages (background hosted service)
  - Parse commands (/status, /medical, /arrived, /cv, /lang, /register)
  - Route commands to appropriate MediatR handlers
  - Send responses and file attachments
  - Manage bot connection lifecycle

### IC-05: WhatsApp Bot Service (Background)
- **Purpose**: Handle WhatsApp Business API communication
- **Responsibilities**:
  - Webhook receiver for incoming messages
  - Parse commands similar to Telegram
  - Send template-based messages
  - Manage API credentials and rate limits

### IC-06: PDF Generation Service
- **Purpose**: Generate PDF documents (CVs, reports, invoices, financial statements)
- **Responsibilities**:
  - Render templates with candidate/financial data
  - Support agency branding (logo, header)
  - Return PDF as byte stream

### IC-07: Excel Export Service
- **Purpose**: Generate Excel exports for reports and data tables
- **Responsibilities**:
  - Convert report data to .xlsx format
  - Apply column headers and formatting
  - Support large datasets with streaming

### IC-08: Scheduled Report Service (Background)
- **Purpose**: Execute configured scheduled reports
- **Responsibilities**:
  - Evaluate schedule rules (daily/weekly/monthly)
  - Generate reports using PDF/Excel services
  - Send via email to configured recipients
  - Track execution history

### IC-09: File Storage Service
- **Purpose**: Manage document upload/download on server file system
- **Responsibilities**:
  - Store files in tenant-specific directory structure
  - Generate unique filenames (prevent collisions)
  - Validate file type and size (max 10MB)
  - Return file paths for database references
  - Generate thumbnails for images

---

## API Layer Components (Carter Modules)

### AM-01: AuthModule (Keep existing)
- Login, refresh, logout, MFA, profile

### AM-02: CandidateModule
- Candidate CRUD, search, filter, document management, CV generation

### AM-03: WorkflowModule
- Workflow configuration CRUD (stages, statuses, transitions, rules)
- Workflow actions (execute transition, get available actions)

### AM-04: EmbassyModule
- Embassy view queries, medical tracking, Tasheer tracking, visa status, transfers

### AM-05: LmisModule
- LMIS view queries, insurance tracking, milestone progression, transfers

### AM-06: TravelModule
- Ticket view, booking, departure view, countdown, departure/non-departure actions

### AM-07: ArrivalModule
- Arrival view, confirmation, exception flagging, arrival ledger

### AM-08: ExceptionModule
- Exception containment workspace, investigation, resolution

### AM-09: CommissionModule
- Commission view, fee breakdown, payments, disputes

### AM-10: AccountingModule
- Chart of accounts, journal entries, trial balance, financial statements

### AM-11: OfficeModule
- Office/branch CRUD (registering branch; not overseas partner)

### AM-11b: PartnerModule
- SuperAdmin: PartnerAgency catalog CRUD (public schema, Art. 40 capacity tier)
- Agency Owner: PartnerLink create/renew/deactivate; list Active partners for intake
- Enforce ደረጃ ትስስር caps and Art. 40 inbound limits

### AM-12: StaffModule (Adapt existing)
- Staff profiles, provisioning, roles (adapt from current implementation)

### AM-13: UserModule (Keep existing)
- User management

### AM-14: RoleModule (Keep existing)
- Role and permission management

### AM-15: NotificationModule
- Notification rules configuration, delivery status, templates

### AM-16: ReportModule
- Report generation, export, scheduled reports configuration

### AM-17: TenantModule
- Tenant provisioning + MoLS license fields (level, license, countries)
- Auto-create HQ office on provision; metadata management (admin-only)

### AM-18: DashboardModule
- KPI data, pipeline funnel, trend charts

---

## Frontend Components

### FC-01: Auth Shell (Keep)
- Login page, MFA flow, password change, session management

### FC-02: Main Layout (Adapt)
- Sidebar navigation (new menu items), header, notification bell

### FC-03: Dashboard Page
- KPI cards, pipeline funnel chart, trend charts, quick links

### FC-04: Candidate Pages
- List/search/filter, detail view, document upload, timeline, CV generation

### FC-05: Workflow View Pages
- New Contract View, Embassy View, LMIS View, Ticket View, Departure View, Arrival View, Commission View
- Each view: data table with filters + action buttons per record

### FC-06: Workflow Configuration Pages (Admin)
- Stage editor, status editor, transition rule builder, action button configurator

### FC-07: Finance Pages
- Commission view, payment recording, bank reconciliation, financial statements

### FC-08: Exception Pages
- Exception containment workspace, investigation timeline

### FC-09: Staff/Office Pages (Adapt)
- Staff management, office management, partner directory

### FC-10: Report Pages
- Report viewer, export buttons, scheduled report configuration

### FC-11: Notification/Bot Config Pages
- Notification rule editor, bot configuration, delivery monitoring

### FC-12: Admin Pages
- Tenant provisioning, system settings, user management, role management
