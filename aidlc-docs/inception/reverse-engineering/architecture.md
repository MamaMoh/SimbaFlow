# System Architecture

## System Overview

SimbaFlow follows **Clean Architecture** with a vertical-slice feature organization in the API layer. The system uses **CQRS** (Command Query Responsibility Segregation) via MediatR for all business operations, with a robust pipeline of cross-cutting behaviors.

## Architecture Diagram

```mermaid
flowchart TB
    subgraph CLIENT["Client Layer"]
        FE["Next.js 15 Frontend<br/>(React 19, TypeScript)"]
        BOT["Telegram/WhatsApp Bot<br/>(Future)"]
    end

    subgraph API["API Layer (SimbaFlow.API)"]
        CARTER["Carter Modules<br/>(Minimal API Endpoints)"]
        MW["Middleware<br/>(Exception, StaffContext)"]
    end

    subgraph APP["Application Layer (SimbaFlow.Application)"]
        PIPELINE["MediatR Pipeline"]
        VAL["ValidationBehavior"]
        AUTH["AuthorizationBehavior"]
        CLIN["ClinicalAuthorizationBehavior"]
        PERF["PerformanceLogBehavior"]
        AUDIT["AuditBehavior"]
        CONC["ConcurrencyBehavior"]
    end

    subgraph DOMAIN["Domain Layer (SimbaFlow.Domain)"]
        ENT["Entities"]
        ENUM["Enums"]
        EVT["Domain Events"]
        BASE["BaseEntity<br/>(Audit + Soft Delete + Concurrency)"]
    end

    subgraph INFRA["Infrastructure Layer (SimbaFlow.Infrastructure)"]
        PERSIST["Persistence<br/>(EF Core + PostgreSQL)"]
        IDENT["Identity<br/>(JWT + ASP.NET Core Identity)"]
        JOBS["Background Jobs<br/>(Token/Session Cleanup)"]
        DEVT["Domain Event Dispatcher"]
        BILL["Billing Service"]
        AUDITL["Audit Service<br/>(Write + Read via Channel)"]
    end

    subgraph DATA["Data Layer"]
        PG[("PostgreSQL")]
    end

    FE -->|HTTP/JSON| CARTER
    BOT -->|HTTP/JSON| CARTER
    CARTER --> MW
    MW --> PIPELINE
    PIPELINE --> VAL --> AUTH --> CLIN --> PERF --> AUDIT
    PIPELINE --> CONC
    APP --> DOMAIN
    INFRA --> DOMAIN
    PERSIST --> PG
    IDENT --> PG

    style CLIENT fill:#E3F2FD,stroke:#1565C0
    style API fill:#E8F5E9,stroke:#2E7D32
    style APP fill:#FFF3E0,stroke:#E65100
    style DOMAIN fill:#FCE4EC,stroke:#C62828
    style INFRA fill:#F3E5F5,stroke:#6A1B9A
    style DATA fill:#ECEFF1,stroke:#37474F
```

## Component Descriptions

### SimbaFlow.API (Web Host)
- **Purpose**: ASP.NET Core Minimal API host with Carter module registration
- **Responsibilities**: HTTP endpoint routing, DI configuration, middleware pipeline, OpenAPI docs, CORS, health checks
- **Dependencies**: Application, Infrastructure, Shared
- **Type**: Application (Web API)

### SimbaFlow.Application (Business Orchestration)
- **Purpose**: Cross-cutting pipeline behaviors and interface contracts
- **Responsibilities**: Request validation (FluentValidation), RBAC enforcement, clinical authorization, performance logging, audit capture, optimistic concurrency
- **Dependencies**: Domain
- **Type**: Application (Library)

### SimbaFlow.Domain (Core Business)
- **Purpose**: Pure domain model with no external dependencies
- **Responsibilities**: Entity definitions, enum types, domain events, base entity patterns
- **Dependencies**: None
- **Type**: Model (Library)

### SimbaFlow.Infrastructure (External Concerns)
- **Purpose**: Implements all interfaces defined in Application layer
- **Responsibilities**: EF Core DbContext, Identity/JWT services, background jobs, event dispatching, billing calculations, audit persistence
- **Dependencies**: Application, Domain
- **Type**: Infrastructure (Library)

### SimbaFlow.Shared (Shared Models)
- **Purpose**: DTOs and models shared across boundaries
- **Dependencies**: None
- **Type**: Shared (Library)

## Data Flow — Typical Request

```mermaid
sequenceDiagram
    participant C as Client (Next.js)
    participant API as Carter Module
    participant P as MediatR Pipeline
    participant V as ValidationBehavior
    participant A as AuthorizationBehavior
    participant H as Command Handler
    participant DB as PostgreSQL

    C->>API: HTTP Request + JWT
    API->>P: Send(Command/Query)
    P->>V: Validate request (FluentValidation)
    V->>A: Check permissions (IRequirePermission)
    A->>H: Execute business logic
    H->>DB: EF Core query/persist
    DB-->>H: Result
    H-->>API: Result<T>
    API-->>C: HTTP Response (JSON)
```

## Integration Points

### External APIs
- **None currently** — self-contained system

### Databases
- **PostgreSQL** — Primary data store (EF Core + Npgsql)

### Third-party Services
- **None currently** — JWT auth is self-issued

## Infrastructure Components

### Deployment Model
- Single deployable .NET web application
- PostgreSQL database (single instance)
- Next.js frontend (separate deployment)

### Security
- JWT Bearer authentication with refresh token rotation
- ASP.NET Core Identity for user/role management
- Permission-based authorization (SuperAdmin bypass)
- Clinical authorization (department affiliation checks)
- IP restriction support (per-user CIDR allowlists)
- MFA (TOTP) support
