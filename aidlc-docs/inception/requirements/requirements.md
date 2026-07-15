# SimbaFlow — Labour Export Agency Management System Requirements

## Intent Analysis

- **User Request**: Complete implementation of a full-stack platform for labour export agencies managing overseas worker deployment lifecycle
- **Request Type**: Migration/Pivot — Major domain transformation from Hospital IS to Labour Export Management
- **Scope Estimate**: System-wide — All layers affected (domain, API, frontend, database)
- **Complexity Estimate**: Complex — Multi-module, configurable workflow engine, multi-tenancy, real-time updates, bot integration, full ERP financial tracking
- **Migration Strategy**: Complete replacement (delete clinical code, rebuild with labour export domain)

---

## Functional Requirements

### FR-01: Candidate Management
- FR-01.1: Register candidates with full biometric/identity data (name, passport number, photo, nationality, date of birth, contact info)
- FR-01.2: Track passport details and expiration dates
- FR-01.3: Upload and store documents (passport scans, photos, contracts) on server file system with path references in database
- FR-01.4: Auto-generate CVs from candidate profile data
- FR-01.5: Search and filter candidates by name, passport number, labour ID, status, country of travel, office
- FR-01.6: Display complete status history timeline per candidate (every stage transition logged with timestamp and user)
- FR-01.7: Assign Labour ID linking to cross-border regulatory channels
- FR-01.8: Record Country of Travel and Office Name (overseas partner agency/employer)
- FR-01.9: Record Contract Date as legal timeline baseline

### FR-02: Configurable Workflow Engine
- FR-02.1: Store workflow configuration in database tables (stages, statuses, transitions) — admin UI to configure per agency
- FR-02.2: Support admin-defined stages (add/remove/reorder) per agency
- FR-02.3: Support custom status values per stage
- FR-02.4: Support configurable transition rules (field conditions that unlock action buttons)
- FR-02.5: Support parallel track execution (e.g., Medical + Tasheer simultaneously in Embassy stage)
- FR-02.6: Automatic visibility rules — candidates appear/disappear in views based on status combinations
- FR-02.7: Dynamic action buttons rendered based on server-calculated available actions + client-side optimistic evaluation with server re-validation on submit
- FR-02.8: Per-agency field visibility and mandatory field rules
- FR-02.9: Default workflow template (8-stage flow as described in spec) seeded for new agencies

### FR-03: Embassy & Visa Processing
- FR-03.1: Transfer candidates from New Contract View to Embassy View via "To Embassy" action
- FR-03.2: Track Medical status (book appointment, log Fit/Unfit)
- FR-03.3: Track Tasheer status (book appointment, log Book Done/Expired)
- FR-03.4: Mirror view trigger: when Medical=Fit AND Tasheer=BookDone → candidate appears in BOTH Embassy View AND LMIS View simultaneously (single record, multiple filtered views)
- FR-03.5: Operational status progression: Ready → candidate auto-populates Case Executive View
- FR-03.6: Case Executive updates status to Submitted
- FR-03.7: Embassy user marks visa as Issued or Rejected
- FR-03.8: On Issued: "To LMIS" button appears; clicking transfers candidate OUT of Embassy and Case Executive views
- FR-03.9: Rejection handling and resubmission flows

### FR-04: LMIS (Government Labour Registration)
- FR-04.1: Insurance tracking (Paid/Unpaid); Paid triggers Available status
- FR-04.2: LMIS document upload and verification
- FR-04.3: Milestone progression: Uploaded → Check Verified → Issued
- FR-04.4: On Issued: "To Ticket" action button appears
- FR-04.5: Future-ready for government API integration

### FR-05: Travel & Logistics
- FR-05.1: Flight ticket booking management (Ticket Book Status, Destination, Flight Date)
- FR-05.2: All three fields required before "To Departure" button appears
- FR-05.3: Departure countdown view with Remaining Days formula
- FR-05.4: Notification engine: if not "Notified", show "$n$ days left, notify candidate" alert
- FR-05.5: Path A (Success): "Departured" → "To Arrival" button appears
- FR-05.6: Path B (Disruption): "Not Departed" → defensive loops ("Back to Ticket" or "Canceled")

### FR-06: Arrival & Deployment Tracking
- FR-06.1: Arrival confirmation workflow
- FR-06.2: Exception tracking: Returned/Runaway → full containment workspace
- FR-06.3: Containment workspace with stages: Investigation → Resolution → Closed
- FR-06.4: Financial impact tracking for exceptions
- FR-06.5: Agency notification for exception cases
- FR-06.6: Permanent arrival ledger (candidate never disappears from Arrival View)
- FR-06.7: "Add to Commission" button on confirmed arrival → copies to Commission View

### FR-07: Commission & Finance (Full ERP)
- FR-07.1: Double-entry accounting system
- FR-07.2: Multi-currency support (ETB, USD, SAR, AED, etc.)
- FR-07.3: Fee breakdown by category (agency fee, government fee, medical fee, ticket cost, insurance)
- FR-07.4: Partial payment tracking
- FR-07.5: Per-office commission reporting
- FR-07.6: Bank reconciliation
- FR-07.7: Tax calculations
- FR-07.8: Full financial statements (P&L, balance sheet)
- FR-07.9: Dispute resolution tracking
- FR-07.10: Agency revenue reports and analytics

### FR-08: Agency ERP
- FR-08.1: Employee/staff management
- FR-08.2: Office/branch management (physical locations)
- FR-08.3: Partner agency (overseas) management — employer directory
- FR-08.4: Role-based access control with roles: Admin, Embassy Officer, Case Executive, Finance, Field Agent, Agency Owner (super-admin per tenant), Data Entry Clerk, Auditor (read-only), Office Manager (branch-level admin), Notification Manager, API Integration User
- FR-08.5: Full audit trail for all operations (write + read)
- FR-08.6: Dashboard and KPIs

### FR-09: Telegram/WhatsApp Bot (Full Scope)
- FR-09.1: Field employee access to candidate status lookup
- FR-09.2: Push notifications for stage transitions
- FR-09.3: CV and document generation on demand
- FR-09.4: Quick actions (update medical status, confirm arrival, mark departure)
- FR-09.5: Multi-language support (Amharic/English)
- FR-09.6: Telegram Bot API integration
- FR-09.7: WhatsApp Business API integration

### FR-10: Reporting & Analytics
- FR-10.1: Candidates per stage (pipeline view)
- FR-10.2: Agency performance dashboards
- FR-10.3: Office-level comparisons
- FR-10.4: Overdue/stuck candidates alerts
- FR-10.5: Financial summary reports
- FR-10.6: Export to Excel
- FR-10.7: Export to PDF
- FR-10.8: Custom date ranges
- FR-10.9: Scheduled reports (automated periodic generation)

---

## Non-Functional Requirements

### NFR-01: Multi-Tenancy
- Shared database, separate PostgreSQL schemas per agency
- Schema-per-tenant isolation for data security
- Shared infrastructure code, tenant-specific data
- Admin can provision new tenants (new schema creation)

### NFR-02: Real-Time Updates
- WebSocket/SignalR for live updates on stage transitions
- Push notifications to connected clients when candidate moves stages
- Real-time notification delivery to bot channels

### NFR-03: Performance
- API response time < 500ms for standard queries
- Support 50+ concurrent users per agency
- File upload support up to 10MB per document
- Efficient query filtering across workflow views (indexed by stage/status)

### NFR-04: Security (Extension Enabled — All SECURITY rules enforced)
- JWT + Refresh Token authentication (existing)
- MFA support for admin accounts
- Password policies (8+ chars, history, expiry)
- Per-user IP restrictions
- RBAC with granular permissions
- Audit trail (write + read)
- Encryption at rest (database) and in transit (TLS 1.2+)
- Input validation on all endpoints (FluentValidation)
- Rate limiting on public endpoints
- No hardcoded credentials
- Security headers (CSP, HSTS, X-Frame-Options, etc.)

### NFR-05: Resiliency (Extension Enabled — All RESILIENCY rules enforced)
- Target: Self-hosted Docker Compose deployment
- RTO/RPO: Hours (Backup & Restore strategy appropriate for self-hosted)
- Automated database backups with retention
- Health checks on all services
- Structured logging to centralized location
- Graceful degradation when bot services unavailable
- Circuit breakers on external API calls (government APIs, Telegram/WhatsApp)

### NFR-06: Testing (PBT Extension Enabled — Full enforcement)
- Property-based testing using FsCheck (.NET) and fast-check (TypeScript)
- Round-trip properties for all serialization (API DTOs, workflow state)
- Invariant properties for workflow transitions (stage/status state machine)
- Stateful PBT for workflow engine (command sequences)
- Complementary example-based tests for all critical paths

### NFR-07: Deployment
- Docker Compose for local/self-hosted deployment
- .NET API container + PostgreSQL container + Next.js container
- File storage mounted as Docker volume
- Environment-based configuration (no secrets in code)

### NFR-08: Internationalization
- Multi-language support: English (primary) + Amharic
- Bot messages in both languages
- UI language toggle (future — English first for web)

### NFR-09: Scalability
- Horizontal scaling support via Docker Compose replicas
- Database connection pooling
- Background job processing for notifications and reports

---

## Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Backend Framework | .NET 10, CQRS + MediatR, Carter | Existing infrastructure, proven patterns |
| Frontend Framework | Next.js 15, React 19, TypeScript, shadcn/ui | Existing infrastructure |
| Database | PostgreSQL with per-tenant schemas | Multi-tenancy isolation with single DB |
| Real-time | SignalR (ASP.NET Core) | Native .NET integration, WebSocket support |
| File Storage | Server file system (Docker volume) | Self-hosted, simple, cost-effective |
| Bot Framework | Telegram Bot API + WhatsApp Business API | Direct API integration |
| Auth | JWT + Refresh Tokens + MFA (existing) | Keep existing proven implementation |
| Testing (PBT) | FsCheck (.NET) + fast-check (TypeScript) | Language-native PBT frameworks |
| Deployment | Docker Compose | Self-hosted requirement |
| Financial | Double-entry accounting | Full ERP requirement |

---

## Extension Configuration

| Extension | Enabled | Decided At |
|-----------|---------|------------|
| Security Baseline | Yes | Requirements Analysis |
| Resiliency Baseline | Yes | Requirements Analysis |
| Property-Based Testing | Yes (Full) | Requirements Analysis |

---

## Resiliency Decisions (Self-Hosted Context)

| RESILIENCY Rule | Decision | Notes |
|-----------------|----------|-------|
| RESILIENCY-02 (RTO/RPO) | Hours — Backup & Restore | Self-hosted; nightly DB backups, redeploy from Docker images |
| RESILIENCY-03 (Change Mgmt) | Propose lightweight process | No existing formal process |
| RESILIENCY-04 (CI/CD) | Propose pipeline (GitHub Actions) | No existing pipeline |
| RESILIENCY-04 (Rollback) | Redeploy previous Docker image version | Version-pinned rollback |
| RESILIENCY-04 (Deploy Style) | Direct/in-place | Acceptable for self-hosted single-server |
| RESILIENCY-08 (Topology) | Single-server (N/A for multi-zone) | Self-hosted; no cloud zone concept |
| RESILIENCY-14 (Testing) | Defer to Operations | Capture scenarios now, execute later |
| RESILIENCY-15 (Incident Response) | Propose lightweight process | No existing formal process |

---

## User Roles

| Role | Access Level | Description |
|------|-------------|-------------|
| Admin | System-wide | Full system access, all agencies |
| Agency Owner | Tenant super-admin | Full access within their agency |
| Office Manager | Branch-level admin | Manage their office's candidates and staff |
| Embassy Officer | Stage-specific | Embassy View operations (medical, tasheer, visa) |
| Case Executive | Stage-specific | Visa documentation processing |
| Finance | Module-specific | Commission, payments, financial reports |
| Field Agent | Limited | Bot access, status updates, quick actions |
| Data Entry Clerk | Limited | Candidate registration and basic updates |
| Auditor | Read-only | View all data, generate reports, no mutations |
| Notification Manager | Module-specific | Manage notification rules, bot configuration |
| API Integration User | System | Service account for external API integrations |

---

## Module Priority

All 10 modules simultaneously (full system implementation). No phased MVP — deliver complete system.

---

## Scope Summary

This is a **complete system rewrite** of the domain layer while preserving the proven architectural infrastructure. The implementation will:
1. Delete all clinical/medical domain code (entities, features, migrations, frontend pages)
2. Preserve: Auth, RBAC, audit trail, MediatR pipeline, BaseEntity, Result pattern, domain events, background jobs
3. Build 10 new feature modules covering the full candidate deployment lifecycle
4. Add: Configurable workflow engine, double-entry accounting, SignalR real-time, Telegram/WhatsApp bot, multi-language
5. Restructure multi-tenancy from TenantId column to schema-per-tenant isolation
6. Deploy as Docker Compose stack (API + DB + Frontend + Bot)
