# Tech Stack Decisions — Unit 1: Core Infrastructure

## Confirmed Tech Stack (from Requirements Analysis)

| Layer | Technology | Version | Purpose |
|-------|-----------|---------|---------|
| Runtime | .NET | 10.0 | API host |
| Web Framework | ASP.NET Core Minimal API | 10.0 | HTTP framework |
| API Modules | Carter | 9.0.0 | Endpoint registration |
| CQRS | MediatR | 13.0.0 | Command/Query mediator |
| Validation | FluentValidation | 12.0.0 | Request validation |
| ORM | Entity Framework Core | 10.0.9 | Data access |
| Database | PostgreSQL | 16+ | Primary data store |
| DB Provider | Npgsql.EF | 10.0.2 | PostgreSQL EF Core provider |
| Identity | ASP.NET Core Identity | 10.0.9 | User/role management |
| Auth | JWT Bearer | 10.0.9 | Token authentication |
| Real-time | SignalR | (built-in ASP.NET Core) | WebSocket communication |
| Frontend | Next.js | 15.x | React framework |
| UI Library | React | 19 | Component library |
| CSS | Tailwind CSS | 4.x | Styling |
| Components | Radix UI (shadcn/ui) | Various | Accessible primitives |
| State | Zustand | latest → pin exact | Client state management |
| Data Fetching | SWR | 2.3.x | Server state caching |
| Forms | React Hook Form + Zod | 7.x / 3.x | Form management + validation |
| Containerization | Docker + Docker Compose | latest | Deployment |

## New Dependencies for Unit 1

| Package | Version | Layer | Purpose |
|---------|---------|-------|---------|
| Microsoft.AspNetCore.SignalR | (built-in) | Backend | Real-time hub |
| Polly | 8.x | Backend | Circuit breaker, retry, timeout policies |
| Serilog.AspNetCore | 9.x | Backend | Structured logging |
| Serilog.Sinks.Console | 6.x | Backend | Console log output |
| Serilog.Sinks.File | 6.x | Backend | File log output (rolling) |
| SixLabors.ImageSharp | 3.x | Backend | Image thumbnail generation |
| @microsoft/signalr | latest → pin | Frontend | SignalR client |

## Tech Stack Decisions Made in This Unit

### Decision 1: Structured Logging Framework
- **Choice**: Serilog
- **Rationale**: Most mature .NET structured logging; supports multiple sinks; correlation ID enrichment; SECURITY-03 compliant
- **Configuration**: JSON format to console + rolling file; correlation ID from HttpContext; no PII filter

### Decision 2: Resilience Library
- **Choice**: Polly v8 (Microsoft.Extensions.Http.Resilience)
- **Rationale**: Standard .NET resilience library; circuit breaker, retry, timeout; integrates with HttpClientFactory; RESILIENCY-10 compliant
- **Usage**: Applied to all external HTTP calls (bot APIs, future government APIs)

### Decision 3: Image Processing
- **Choice**: SixLabors.ImageSharp
- **Rationale**: Cross-platform, no native dependencies, MIT license; works in Docker Linux containers
- **Usage**: Thumbnail generation (200x200) for uploaded candidate photos

### Decision 4: SignalR Transport
- **Choice**: WebSocket with Long-Polling fallback
- **Rationale**: WebSocket for optimal real-time; long-polling as fallback for restricted networks
- **Scaling Note**: Single-server deployment doesn't need Redis backplane; add later if needed

### Decision 5: Database Connection Strategy
- **Choice**: Npgsql connection pooling (max 100 connections)
- **Rationale**: Single PostgreSQL instance serving multiple schemas; pool shared across tenants
- **Schema Switching**: `SET search_path` on connection checkout (connection interceptor)

### Decision 6: File Storage Strategy
- **Choice**: Local file system with Docker named volume
- **Rationale**: Self-hosted requirement; simplest deployment; expandable by mounting larger disk
- **Volume**: `/data` mounted as `simbaflow-files` Docker volume

### Decision 7: PBT Framework Selection (PBT-09)
- **Choice**: FsCheck 3.x (.NET) + fast-check 3.x (TypeScript)
- **Rationale**: FsCheck integrates with xUnit (existing test project); fast-check integrates with Vitest
- **Additions**: Add `FsCheck.Xunit` to test project; add `fast-check` + `vitest` to frontend dev dependencies

### Decision 8: Rate Limiting
- **Choice**: ASP.NET Core built-in rate limiting middleware (fixed window)
- **Rationale**: Built into .NET 10, no additional dependency; SECURITY-11 compliant
- **Configuration**: 5 requests/minute on login endpoint; 30 requests/minute on refresh; 100 requests/minute on general API

## Dependency Pinning (SECURITY-10)

All dependencies will use exact versions in project files:
- Backend: Exact version in `.csproj` (no floating `*`)
- Frontend: `package-lock.json` committed; pin `zustand`, `immer`, `sonner` to exact versions
- Docker: Pin base images to specific tags (e.g., `mcr.microsoft.com/dotnet/aspnet:10.0.0`)
- PostgreSQL: Pin to `postgres:16.x` in Docker Compose

## CI/CD Pipeline (RESILIENCY-04)

- **Tool**: GitHub Actions (proposed)
- **Pipeline**:
  1. Build .NET API + run tests (including PBT)
  2. Build Next.js frontend
  3. Build Docker images with version tags
  4. Push to container registry (Docker Hub or GitHub Container Registry)
- **Rollback**: Redeploy previous Docker image tag
- **Deployment Style**: Direct (docker compose pull + up)
