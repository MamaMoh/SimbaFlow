# SimbaFlow — System Architecture & Design Document

## 1. What is SimbaFlow?

SimbaFlow is a **multi-tenant SaaS platform** designed for labour export agencies in Ethiopia. It manages the complete lifecycle of overseas worker deployment — from initial candidate registration through embassy clearances, government labour registrations, travel logistics, and financial settlement.

### The Problem It Solves

Labour export agencies currently manage candidate pipelines using:
- Manual Excel spreadsheets
- Paper-based tracking
- WhatsApp groups for coordination
- No visibility across offices

This leads to lost candidates, missed deadlines, financial discrepancies, and compliance failures.

### The Solution

SimbaFlow provides a **configurable workflow engine** that routes candidate records through stage-gate views, with each agency able to customize their process while maintaining a unified platform.

---

## 2. System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        SIMBAFLOW PLATFORM                        │
│                                                                  │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐  │
│  │ Agency A │    │ Agency B │    │ Agency C │    │ Agency N │  │
│  │(Schema A)│    │(Schema B)│    │(Schema C)│    │(Schema N)│  │
│  └────┬─────┘    └────┬─────┘    └────┬─────┘    └────┬─────┘  │
│       │               │               │               │         │
│  ┌────┴───────────────┴───────────────┴───────────────┴────┐   │
│  │              SHARED PLATFORM INFRASTRUCTURE               │   │
│  │  • Authentication (JWT + MFA)                             │   │
│  │  • User Management                                        │   │
│  │  • Permission System                                      │   │
│  │  • Audit Trail                                            │   │
│  │  • Real-time Notifications (SignalR)                       │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              PLATFORM ADMINISTRATION                       │   │
│  │  • Agency Provisioning                                    │   │
│  │  • Cross-agency Analytics                                 │   │
│  │  • System Configuration                                   │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 3. Core Workflow — Candidate Pipeline

Every candidate flows through a configurable pipeline of stages:

```
┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│  INTAKE  │───>│  EMBASSY │───>│   LMIS   │───>│  TICKET  │
│Register  │    │Medical   │    │Insurance │    │Book flight│
│candidate │    │Tasheer   │    │Milestone │    │Destination│
│          │    │Visa      │    │Gov. reg. │    │Date       │
└──────────┘    └──────────┘    └──────────┘    └──────────┘
                                                       │
┌──────────┐    ┌──────────┐    ┌──────────┐          │
│COMMISSION│<───│ ARRIVAL  │<───│DEPARTURE │<─────────┘
│Fee track │    │Confirm   │    │Countdown │
│Payments  │    │Exceptions│    │Notify    │
│Settlement│    │Ledger    │    │Depart/Not│
└──────────┘    └──────────┘    └──────────┘
```

### Stage Details

| Stage | Purpose | Key Actions |
|-------|---------|-------------|
| **Intake** | Register new candidate | Capture identity, passport, contact, country of travel |
| **Embassy** | Medical + Tasheer clearances | Book appointments, track parallel tracks, visa processing |
| **LMIS** | Government labour registration | Insurance, milestone progression (Uploaded→Verified→Issued) |
| **Ticket** | Flight booking | Book status, destination, flight date |
| **Departure** | Pre-flight countdown | Notify candidate, countdown timer, departure confirmation |
| **Arrival** | Deployment tracking | Confirm arrival, handle exceptions (Returned/Runaway) |
| **Commission** | Financial settlement | Fee breakdown, payments, dispute resolution |

---

## 4. Architecture Layers

### Clean Architecture (Backend)

```
┌─────────────────────────────────────────────────────────┐
│  API Layer (SimbaFlow.API)                               │
│  • Carter Modules (Minimal API endpoints)                │
│  • Request/Response DTOs                                 │
│  • FluentValidation validators                           │
└────────────────────────┬────────────────────────────────┘
                         │ MediatR (CQRS)
┌────────────────────────▼────────────────────────────────┐
│  Application Layer (SimbaFlow.Application)               │
│  • Pipeline Behaviors (Validation, Auth, Audit, Perf)    │
│  • Interface contracts                                   │
│  • Result<T> pattern                                     │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│  Domain Layer (SimbaFlow.Domain)                         │
│  • Entities (Candidate, WorkflowEvent, TenantRole...)    │
│  • Enums, Value Objects                                  │
│  • Domain Events                                         │
│  • BaseEntity (audit, soft-delete, concurrency)          │
└────────────────────────┬────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────┐
│  Infrastructure Layer (SimbaFlow.Infrastructure)         │
│  • EF Core DbContext (PostgreSQL)                        │
│  • Tenant schema isolation (interceptor)                 │
│  • JWT + Identity services                               │
│  • SignalR hub                                           │
│  • File storage, background jobs                         │
└─────────────────────────────────────────────────────────┘
```

### Frontend Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Next.js 15 (App Router)                                 │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Pages (app/)                                       │ │
│  │  • (auth)/login, change-password                    │ │
│  │  • (main)/candidates, workflow, staff, tenants...   │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  Components                                         │ │
│  │  • UI (shadcn/ui + Radix primitives)               │ │
│  │  • DataTable (TanStack React Table)                │ │
│  │  • Side Sheets (Create/Edit forms)                 │ │
│  │  • Country/Phone selectors with flags              │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │  State & Data                                       │ │
│  │  • SWR (server state, caching, revalidation)       │ │
│  │  • Zustand (client UI state)                       │ │
│  │  • SignalR Provider (real-time)                     │ │
│  │  • next-auth (JWT session management)              │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

---

## 5. Multi-Tenant Database Design

### Schema Isolation Strategy

Each agency gets a **dedicated PostgreSQL schema**. This provides:
- Complete data isolation between agencies
- No risk of cross-tenant data leaks
- Independent backup/restore per agency
- Easy compliance with data sovereignty requirements

```
PostgreSQL Database: simbaflow
│
├── Schema: public (Platform-level)
│   ├── AspNetUsers          ← All users across all agencies
│   ├── AspNetRoles          ← System roles (SuperAdmin, AgencyOwner...)
│   ├── AspNetUserRoles      ← User-to-system-role mapping
│   ├── Tenants             ← Agency metadata (name, slug, schema, status)
│   ├── Permissions         ← System permission codes (49 permissions)
│   ├── RolePermissions     ← Legacy (being replaced by tenant_role_permissions)
│   ├── Departments         ← Department structure
│   ├── Locations           ← Office/branch hierarchy
│   ├── StaffProfiles       ← Staff details
│   ├── AuditLogs          ← Write audit trail
│   └── ReadAuditLogs      ← Read access audit
│
├── Schema: tenant_ethio_star (Agency: Ethio Star Labour Export)
│   ├── candidates           ← Agency's candidates ONLY
│   ├── tenant_roles         ← Custom roles for this agency
│   ├── tenant_role_permissions ← Which permissions each role has
│   └── tenant_user_roles    ← User-to-tenant-role mapping
│
├── Schema: tenant_addis_manpower (Agency: Addis Manpower PLC)
│   ├── candidates
│   ├── tenant_roles
│   ├── tenant_role_permissions
│   └── tenant_user_roles
│
└── Schema: tenant_golden_gate (Agency: Golden Gate Employment)
    ├── candidates
    ├── tenant_roles
    ├── tenant_role_permissions
    └── tenant_user_roles
```

### How Schema Isolation Works

```
1. User logs in
   └── Backend validates credentials
   └── JWT token generated with "tenant_id" claim

2. User makes API request
   └── JWT validated by middleware
   └── TenantConnectionInterceptor fires:
       └── Reads "tenant_id" from JWT claims
       └── Resolves schema name (cached: "tenant_id" → "tenant_ethio_star")
       └── Executes: SET search_path TO "tenant_ethio_star", "public"
   └── All EF Core queries now target the tenant's schema
   └── User can ONLY see their agency's data

3. SuperAdmin (no tenant_id)
   └── search_path stays as "public"
   └── Can manage all agencies, users, and system config
   └── Cannot accidentally modify tenant data (different tables)
```

### Entity Relationship Diagram

```
┌─────────────────────────────────────────────────────────────┐
│  PUBLIC SCHEMA                                               │
│                                                              │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐  │
│  │ AspNetUsers   │───>│ Tenants      │    │ Permissions  │  │
│  │ • Id          │    │ • Id         │    │ • Id         │  │
│  │ • UserName    │    │ • Name       │    │ • Code       │  │
│  │ • Email       │    │ • Slug       │    │ • Name       │  │
│  │ • TenantId ──────>│ • SchemaName │    │ • Module     │  │
│  │ • OfficeId    │    │ • Status     │    │ • IsActive   │  │
│  │ • IsSuperAdmin│    │ • Settings   │    └──────────────┘  │
│  └──────────────┘    └──────────────┘                       │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  TENANT SCHEMA (per agency)                                  │
│                                                              │
│  ┌──────────────────┐    ┌──────────────────┐              │
│  │ candidates        │    │ tenant_roles      │              │
│  │ • id              │    │ • id              │              │
│  │ • first_name      │    │ • name            │              │
│  │ • last_name       │    │ • code            │              │
│  │ • passport_number │    │ • description     │              │
│  │ • labour_id       │    │ • is_active       │              │
│  │ • gender          │    └────────┬─────────┘              │
│  │ • nationality     │             │                         │
│  │ • country_of_travel│    ┌───────▼─────────┐              │
│  │ • office_name     │    │ tenant_role_      │              │
│  │ • status          │    │ permissions       │              │
│  │ • current_stage_id│    │ • tenant_role_id  │              │
│  │ • current_stage   │    │ • permission_code │              │
│  │ • registered_at   │    └──────────────────┘              │
│  └──────────────────┘                                        │
└─────────────────────────────────────────────────────────────┘
```

---

## 6. Authentication & Authorization

### Authentication Flow

```
┌──────┐   credentials    ┌─────────┐   validate    ┌──────────┐
│Client├─────────────────>│  /login  ├──────────────>│ Identity │
└──┬───┘                  └────┬────┘               └──────────┘
   │                           │
   │   JWT + RefreshToken      │
   │<──────────────────────────┘
   │
   │   Bearer token on every request
   │─────────────────────────────────────────────>│ API Endpoint │
                                                   │              │
                                        ┌──────────┤  Pipeline:   │
                                        │          │  1. Validate │
                                        │          │  2. Authorize│
                                        │          │  3. Execute  │
                                        │          │  4. Audit    │
                                        │          └──────────────┘
```

### Permission System

| Level | Description |
|-------|-------------|
| **System Permissions** | 49 fixed codes (e.g., `candidate.read`, `workflow.execute`, `accounting.post`) |
| **Tenant Roles** | Agency-defined roles (e.g., "Embassy Officer", "Finance Manager") |
| **Role-Permission Mapping** | Each role has a set of assigned permissions |
| **User-Role Assignment** | Users get roles within their agency |

### User Hierarchy

```
Platform SuperAdmin
    └── Can manage ALL agencies
    └── Can create/suspend/delete agencies
    └── Can view cross-agency analytics

Agency Owner (per tenant)
    └── Full control within their agency
    └── Creates custom roles
    └── Manages agency staff

Custom Roles (per tenant)
    └── Embassy Officer: candidate.read, embassy.update, workflow.execute
    └── Finance Manager: commission.*, accounting.*, report.*
    └── Data Entry Clerk: candidate.create, candidate.update
    └── (Agency defines their own)
```

---

## 7. Request Pipeline (MediatR Behaviors)

Every API request goes through this ordered pipeline:

```
HTTP Request
    │
    ▼
┌─────────────────────────────┐
│ 1. ValidationBehavior       │  ← FluentValidation rules
│    Rejects invalid requests │
└─────────────┬───────────────┘
              │
┌─────────────▼───────────────┐
│ 2. AuthorizationBehavior    │  ← Check IRequirePermission
│    Verify user has permission│
└─────────────┬───────────────┘
              │
┌─────────────▼───────────────┐
│ 3. WorkflowAuthorizationBhv │  ← Check IRequireOfficeAccess
│    Verify office-level access│
└─────────────┬───────────────┘
              │
┌─────────────▼───────────────┐
│ 4. PerformanceLogBehavior   │  ← Log slow requests (>500ms)
└─────────────┬───────────────┘
              │
┌─────────────▼───────────────┐
│ 5. AuditBehavior            │  ← Record who did what, when
└─────────────┬───────────────┘
              │
┌─────────────▼───────────────┐
│ 6. Handler (business logic) │  ← Actual command/query execution
└─────────────────────────────┘
```

---

## 8. Event-Sourced Workflow Engine

The workflow engine uses **event sourcing** for complete auditability:

```
Every candidate state change is stored as an immutable event:

WorkflowEvent {
    CandidateId: "abc-123"
    SequenceNumber: 1
    EventType: "StageTransitioned"
    FromStage: "Intake"
    ToStage: "Embassy"
    Data: { "triggeredBy": "To Embassy button" }
    UserId: "user-456"
    Timestamp: "2026-07-15T10:30:00Z"
}

Current state = Replay all events for a candidate
(Snapshots every 20 events for performance)
```

### Benefits
- Complete audit trail (who moved what, when, why)
- Temporal queries (what was the state at any point in time)
- No data loss (events are append-only, never deleted)
- Debugging (replay events to reproduce any state)

---

## 9. API Endpoints Summary

| Module | Base Path | Operations |
|--------|-----------|-----------|
| Auth | `/api/auth` | Login, refresh, logout, MFA, change-password |
| Candidates | `/api/candidates` | CRUD, documents, timeline, CV generation |
| Workflow | `/api/workflow` | Transitions, status updates, available actions |
| Tenants | `/api/tenants` | CRUD agencies (SuperAdmin only) |
| Users | `/api/users` | CRUD, role assignment, toggle status |
| Roles | `/api/roles` | CRUD custom roles, permission assignment |

---

## 10. Deployment Architecture

### Docker Compose (Self-Hosted)

```
┌─────────────────────────────────────────────────┐
│  Docker Host                                     │
│                                                  │
│  ┌─────────────┐  ┌─────────────┐  ┌────────┐ │
│  │  api         │  │  frontend   │  │postgres│ │
│  │  .NET 10     │  │  Next.js 15 │  │  16    │ │
│  │  Port 5000   │  │  Port 3000  │  │  5432  │ │
│  └──────────────┘  └─────────────┘  └────────┘ │
│         │                │               │       │
│  ┌──────┴────────────────┴───────────────┴────┐ │
│  │  Docker Network: simbaflow-net              │ │
│  └────────────────────────────────────────────┘ │
│                                                  │
│  Volumes:                                        │
│  • simbaflow-db (PostgreSQL data)               │
│  • simbaflow-files (Document uploads)           │
│  • simbaflow-logs (Application logs)            │
└─────────────────────────────────────────────────┘
```

---

## 11. Security Features

| Feature | Implementation |
|---------|---------------|
| Authentication | JWT Bearer + Refresh Token rotation |
| MFA | TOTP (Google Authenticator compatible) |
| Password Policy | 8+ chars, upper, lower, digit, special, history |
| Brute Force Protection | Account lockout after 5 attempts |
| Data Isolation | PostgreSQL schema-per-tenant |
| Audit Trail | Every mutation logged (who, what, when) |
| Input Validation | FluentValidation on every request |
| Rate Limiting | 5/min login, 100/min general API |
| CORS | Restricted to configured origins |
| Security Headers | CSP, HSTS, X-Frame-Options, X-Content-Type |
| Encryption | TLS in transit, encrypted backups |

---

## 12. Future Roadmap

| Module | Status | Description |
|--------|--------|-------------|
| Candidate Management | ✅ Complete | CRUD, documents, search |
| Multi-Tenancy | ✅ Complete | Schema isolation, provisioning |
| Role & Permissions | ✅ Complete | Custom per-agency roles |
| User Management | ✅ Complete | CRUD, tenant association |
| Workflow Engine | 🔨 In Progress | Event-sourced state machine |
| Embassy Processing | 📋 Planned | Medical, Tasheer, visa tracking |
| LMIS Integration | 📋 Planned | Government registration |
| Travel & Logistics | 📋 Planned | Ticketing, departure countdown |
| Commission & Finance | 📋 Planned | Double-entry accounting |
| Telegram/WhatsApp Bot | 📋 Planned | Field agent mobile access |
| Reporting & Analytics | 📋 Planned | Dashboards, Excel/PDF export |
| Real-time Updates | 🔨 In Progress | SignalR WebSocket |

---

*Document Version: 1.0 | Last Updated: July 2026*
