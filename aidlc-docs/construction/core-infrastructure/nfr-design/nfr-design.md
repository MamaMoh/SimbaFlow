# NFR Design — Unit 1: Core Infrastructure

## 1. Schema-Per-Tenant Isolation Pattern

### Design
```
┌─────────────────────────────────────────────────┐
│  HTTP Request (JWT with tenant_id claim)         │
├─────────────────────────────────────────────────┤
│  TenantSchemaInterceptor (EF Core)               │
│  ┌─────────────────────────────────────────────┐│
│  │ 1. Read TenantId from ICurrentUserService   ││
│  │ 2. Lookup SchemaName (IMemoryCache, 5min)   ││
│  │ 3. SET search_path TO '{schema}', 'public'  ││
│  └─────────────────────────────────────────────┘│
├─────────────────────────────────────────────────┤
│  ApplicationDbContext (tenant-scoped queries)     │
└─────────────────────────────────────────────────┘
```

### Implementation Approach
- **Connection Interceptor**: `IDbConnectionInterceptor` that fires `ConnectionOpenedAsync` to set `search_path`
- **Schema Resolution**: Cached in `IMemoryCache` with `TenantId` as key, 5-min sliding expiration
- **Migration Strategy**: Custom `IMigrationsSqlGenerator` that applies migrations to a specified schema
- **Tenant Context**: `ITenantContext` interface resolved per-request from JWT claims

### Key Classes
- `TenantConnectionInterceptor : DbConnectionInterceptor` — Sets search_path
- `TenantSchemaResolver : ITenantSchemaResolver` — Resolves TenantId → schema name (cached)
- `TenantMigrationService` — Applies pending migrations to a specific schema
- `MultiTenantDbContextFactory` — Creates DbContext for a specific tenant (used by admin operations)

---

## 2. SignalR Real-Time Pattern

### Design
```
┌─────────────────────┐     ┌─────────────────────┐
│  Domain Event        │────>│  SignalR Event       │
│  Handler             │     │  Broadcaster         │
└─────────────────────┘     └─────────┬───────────┘
                                       │
                       ┌───────────────┼───────────────┐
                       v               v               v
              ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
              │ tenant:abc   │ │ tenant:abc:  │ │ user:xyz     │
              │ (all users)  │ │ office:123   │ │ (personal)   │
              └──────────────┘ └──────────────┘ └──────────────┘
```

### Implementation Approach
- **Hub**: `SimbaFlowHub : Hub` — manages connections and group membership
- **Broadcaster**: `ISignalRBroadcaster` — service that domain event handlers call
- **Groups**: Three-level grouping: tenant, office, personal
- **Payload**: Strongly-typed messages using `IHubContext<SimbaFlowHub>`
- **Authentication**: JWT-based hub authentication (same token as API)

### Key Classes
- `SimbaFlowHub : Hub` — Connection lifecycle (OnConnectedAsync, OnDisconnectedAsync)
- `SignalRBroadcaster : ISignalRBroadcaster` — Broadcast methods (CandidateUpdated, NotificationReceived)
- `CandidateUpdatedNotificationHandler : INotificationHandler<CandidateStatusChanged>` — Bridges domain event to SignalR

### Message Types
```csharp
record CandidateUpdatedMessage(Guid CandidateId, string Field, string OldValue, string NewValue, string ChangedBy, DateTime Timestamp);
record PersonalNotificationMessage(string Title, string Body, string? ActionUrl, string Severity);
record SystemAlertMessage(string Title, string Body, string AlertType);
```

---

## 3. Resilience Patterns (Polly)

### Design
```
┌───────────────────────────────────────────────────────┐
│  External Call (Bot API, Future Gov API)                │
├───────────────────────────────────────────────────────┤
│  Polly Pipeline:                                       │
│  ┌─────────┐  ┌─────────┐  ┌──────────┐  ┌────────┐ │
│  │ Timeout │─>│ Retry   │─>│ Circuit  │─>│ Bulkhead│ │
│  │ 10s     │  │ 3x exp  │  │ Breaker  │  │ 10 conc│ │
│  └─────────┘  └─────────┘  └──────────┘  └────────┘ │
└───────────────────────────────────────────────────────┘
```

### Implementation Approach
- **HttpClientFactory + Polly**: Named clients with resilience pipelines
- **Timeout**: 10 seconds for external calls
- **Retry**: 3 attempts with exponential backoff (1s, 2s, 4s) for transient failures (5xx, timeout)
- **Circuit Breaker**: Open after 5 consecutive failures, half-open after 30 seconds
- **Bulkhead**: Max 10 concurrent calls to each external service

### Key Configuration
```csharp
services.AddHttpClient("TelegramBot")
    .AddResilienceHandler("bot-pipeline", builder => {
        builder.AddTimeout(TimeSpan.FromSeconds(10));
        builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage> {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromSeconds(1)
        });
        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage> {
            FailureRatio = 0.5,
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(30)
        });
    });
```

---

## 4. Structured Logging Pattern (Serilog)

### Design
```
┌──────────────────────────────────────────────────────┐
│  Request Pipeline                                     │
│  ┌────────────────────────────────────────────────┐  │
│  │ RequestId (correlation) enriched on every log  │  │
│  │ TenantId enriched from context                 │  │
│  │ UserId enriched from context                   │  │
│  └────────────────────────────────────────────────┘  │
│                                                       │
│  Sinks:                                               │
│  ├── Console (structured JSON, development)           │
│  ├── File (rolling, 100MB limit, 30-day retention)    │
│  └── (Future: centralized log service)                │
└──────────────────────────────────────────────────────┘
```

### Implementation Approach
- **Enrichers**: RequestId, TenantId, UserId, MachineName, Environment
- **PII Filter**: Destructuring policy that redacts `Password`, `Token`, `Secret`, `Email` fields
- **Log Levels**: Information (default), Warning (EF Core), Error (unhandled)
- **Performance Logging**: Requests > 500ms logged as Warning
- **File Rotation**: Daily rolling, max 100MB per file, 30-day retention

### Key Configuration
```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "SimbaFlow")
    .WriteTo.Console(new JsonFormatter())
    .WriteTo.File("logs/simbaflow-.json",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        fileSizeLimitBytes: 100_000_000)
    .CreateLogger();
```

---

## 5. Rate Limiting Pattern

### Design
- **Login endpoint**: Fixed window — 5 requests per minute per IP
- **Token refresh**: Fixed window — 30 requests per minute per IP
- **General API**: Fixed window — 100 requests per minute per user
- **File upload**: Fixed window — 10 requests per minute per user

### Implementation
- ASP.NET Core built-in `RateLimiter` middleware
- Policy names: `"login"`, `"refresh"`, `"general"`, `"upload"`
- Rejection: HTTP 429 Too Many Requests with Retry-After header

---

## 6. Health Check Pattern

### Design
```
GET /health          → Shallow (process alive, 200 OK)
GET /health/ready    → Deep (DB connected, schema resolution works, file system writable)
```

### Implementation
- `AddHealthChecks().AddNpgSql()` — PostgreSQL connectivity
- Custom `TenantSchemaHealthCheck` — Can resolve at least one tenant schema
- Custom `FileStorageHealthCheck` — /data directory writable
- Response: JSON format with individual check status

---

## 7. File Storage Pattern

### Design
```
/data/
├── tenants/
│   ├── {tenant-slug}/
│   │   └── candidates/
│   │       └── {candidate-id}/
│   │           ├── {guid}_{original-name}.pdf
│   │           └── {guid}_{original-name}_thumb.jpg
│   └── ...
└── backups/    (database backup destination)
```

### Implementation
- **Interface**: `IFileStorageService` (upload, download, delete, exists, getThumbnail)
- **Validation**: Magic byte check (not just extension), size limit enforcement
- **Thumbnail**: SixLabors.ImageSharp resize to 200x200, JPEG quality 80
- **Security**: Paths validated against traversal; no user-controlled path segments
- **Docker Volume**: Named volume `simbaflow-files` mounted at `/data`

---

## 8. Docker Compose Architecture

### Design
```yaml
services:
  api:
    image: simbaflow-api:${VERSION}
    ports: ["5000:8080"]
    environment: [ConnectionStrings, JWT, etc.]
    volumes: ["simbaflow-files:/data"]
    depends_on: [postgres]
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s

  frontend:
    image: simbaflow-frontend:${VERSION}
    ports: ["3000:3000"]
    environment: [NEXTAUTH_URL, API_URL, etc.]
    depends_on: [api]
    restart: unless-stopped

  postgres:
    image: postgres:16.4
    volumes: ["simbaflow-db:/var/lib/postgresql/data"]
    environment: [POSTGRES_PASSWORD, etc.]
    ports: ["5432:5432"]
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 10s

volumes:
  simbaflow-db:
  simbaflow-files:
```

---

## 9. Backup Strategy (RESILIENCY-12)

### Design
- **Tool**: `pg_dump` via cron job or Docker sidecar
- **Schedule**: Nightly at 02:00 UTC
- **Format**: Custom format (compressed)
- **Retention**: 30 days (delete older backups)
- **Storage**: `/data/backups/` directory (same volume, or separate mount)
- **Encryption**: GPG encrypt backup files at rest
- **Validation**: Weekly test restore to verify integrity (documented in runbook)

---

## 10. Security Hardening

### API Hardening
- Remove `Server` header from responses
- Disable directory listing
- Configure `X-Powered-By` removal
- Request body size limit: 15MB (Kestrel)
- HTTPS redirection (existing)

### Docker Hardening
- Non-root user in Dockerfiles (`USER app`)
- Read-only root filesystem where possible
- No `latest` tags — pin all image versions
- Minimal base images (Alpine or distroless)
- Health checks configured on all containers

### Database Hardening
- Strong password for PostgreSQL (from env var)
- `pg_hba.conf` restricts connections to Docker network only
- SSL mode for database connections (`SslMode=Require` in connection string)
- Regular VACUUM and maintenance via pg_cron or Docker exec
