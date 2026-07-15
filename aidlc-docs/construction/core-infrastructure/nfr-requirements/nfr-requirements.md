# NFR Requirements — Unit 1: Core Infrastructure

## NFR-PERF: Performance Requirements

| ID | Requirement | Target | Measurement |
|----|-------------|--------|-------------|
| PERF-01 | API response time (simple queries) | < 200ms p95 | Health check, schema resolution |
| PERF-02 | API response time (complex queries) | < 500ms p95 | Tenant provisioning excluded |
| PERF-03 | Tenant schema resolution | < 5ms (cached) | Time to resolve TenantId → schema name |
| PERF-04 | SignalR message delivery | < 100ms | Time from event emit to client receipt |
| PERF-05 | File upload (10MB) | < 5 seconds | End-to-end including disk write |
| PERF-06 | Concurrent users per tenant | 50+ | Simultaneous active WebSocket connections |
| PERF-07 | Tenant provisioning | < 30 seconds | Full schema creation + seed + user |
| PERF-08 | JWT token validation | < 2ms | Per-request overhead |

## NFR-SCALE: Scalability Requirements

| ID | Requirement | Target | Strategy |
|----|-------------|--------|----------|
| SCALE-01 | Number of tenants | 100+ schemas | PostgreSQL schema management |
| SCALE-02 | Candidates per tenant | 50,000+ records | Indexed queries, pagination |
| SCALE-03 | Concurrent SignalR connections | 500+ total | Single server, managed groups |
| SCALE-04 | File storage | 100GB+ | Docker volume, expandable |
| SCALE-05 | Database connections | Connection pooling (max 100) | Npgsql connection pool |
| SCALE-06 | Horizontal scaling | Docker Compose replicas (future) | Stateless API, shared DB |

## NFR-AVAIL: Availability Requirements

| ID | Requirement | Target | Strategy |
|----|-------------|--------|----------|
| AVAIL-01 | Target availability | 99.5% (single server) | Restart policies in Docker |
| AVAIL-02 | RTO | 4 hours | Redeploy from Docker images + restore DB |
| AVAIL-03 | RPO | 24 hours | Nightly PostgreSQL backups |
| AVAIL-04 | Health checks | /health + /health/ready | Kubernetes-compatible probes |
| AVAIL-05 | Auto-restart on crash | Yes | Docker restart: unless-stopped |
| AVAIL-06 | Graceful degradation | Bot failure doesn't affect API | Circuit breaker on bot services |

## NFR-SEC: Security Requirements (Extension Enforced)

| ID | Requirement | SECURITY Rule | Implementation |
|----|-------------|---------------|----------------|
| SEC-01 | Encryption at rest | SECURITY-01 | PostgreSQL data-at-rest encryption (full disk or LUKS) |
| SEC-02 | Encryption in transit | SECURITY-01 | TLS 1.2+ for all connections (HTTPS, PostgreSQL SSL) |
| SEC-03 | Application logging | SECURITY-03 | Structured logging (Serilog), no PII in logs |
| SEC-04 | HTTP security headers | SECURITY-04 | CSP, HSTS, X-Frame-Options (existing in Next.js) |
| SEC-05 | Input validation | SECURITY-05 | FluentValidation on all commands, max-length constraints |
| SEC-06 | Least privilege | SECURITY-06 | Granular permissions, no wildcard access |
| SEC-07 | Access control | SECURITY-08 | AuthorizationBehavior + WorkflowAuthorizationBehavior |
| SEC-08 | Error handling | SECURITY-09/15 | GlobalExceptionHandler, no stack traces in production |
| SEC-09 | Dependency pinning | SECURITY-10 | Lock files committed, no `latest` tags in Docker |
| SEC-10 | Rate limiting | SECURITY-11 | Rate limiter on public endpoints (login, refresh) |
| SEC-11 | Authentication | SECURITY-12 | JWT + MFA + password policies + brute-force protection |
| SEC-12 | Audit integrity | SECURITY-13 | Append-only audit logs, no application-level delete |
| SEC-13 | Security alerting | SECURITY-14 | Log auth failures, privilege escalation attempts |
| SEC-14 | Fail-safe | SECURITY-15 | Deny access on error, resource cleanup in all paths |

## NFR-RES: Resiliency Requirements (Extension Enforced)

| ID | Requirement | RESILIENCY Rule | Implementation |
|----|-------------|-----------------|----------------|
| RES-01 | Workload classification | RESILIENCY-01 | API = Critical, Bot = Medium, Reports = Low |
| RES-02 | Recovery targets | RESILIENCY-02 | RTO: 4 hours, RPO: 24 hours (Backup & Restore) |
| RES-03 | Change management | RESILIENCY-03 | Proposed lightweight process (PR-based) |
| RES-04 | Automated deployment | RESILIENCY-04 | Docker Compose pull + up (version-pinned images) |
| RES-05 | Monitoring | RESILIENCY-05 | Structured logs + health checks (no cloud monitoring) |
| RES-06 | Health checks | RESILIENCY-06 | /health (shallow) + /health/ready (deep with DB check) |
| RES-07 | Timeouts | RESILIENCY-10 | All external calls have explicit timeouts |
| RES-08 | Circuit breakers | RESILIENCY-10 | Polly on bot API calls, external integrations |
| RES-09 | Graceful degradation | RESILIENCY-10 | Bot down → API unaffected; Report gen down → API unaffected |
| RES-10 | Backups | RESILIENCY-12 | pg_dump nightly, 30-day retention, encrypted |
| RES-11 | Failover runbook | RESILIENCY-13 | Documented restore procedure from backup |

## NFR-TEST: Testing Requirements (PBT Extension Enforced)

| ID | Requirement | PBT Rule | Implementation |
|----|-------------|----------|----------------|
| TEST-01 | PBT framework (.NET) | PBT-09 | FsCheck with xUnit integration |
| TEST-02 | PBT framework (TypeScript) | PBT-09 | fast-check with Vitest |
| TEST-03 | Property identification | PBT-01 | Document testable properties in functional design |
| TEST-04 | Round-trip properties | PBT-02 | Schema resolution: serialize→deserialize tenant config |
| TEST-05 | Invariant properties | PBT-03 | Tenant isolation: any query scoped to single schema |
| TEST-06 | Generator quality | PBT-07 | Domain generators for TenantInfo, Permissions |
| TEST-07 | Shrinking/reproducibility | PBT-08 | Seed logging in CI, shrinking enabled |
| TEST-08 | Complementary tests | PBT-10 | PBT + example-based for all critical paths |

## NFR-MAINT: Maintainability Requirements

| ID | Requirement | Implementation |
|----|-------------|----------------|
| MAINT-01 | Code documentation | XML doc comments on public APIs, README per module |
| MAINT-02 | Consistent patterns | CQRS + MediatR + Carter for all new features |
| MAINT-03 | Separation of concerns | Clean Architecture layers maintained |
| MAINT-04 | Configuration management | appsettings.json + env vars, no hardcoded values |
| MAINT-05 | Database migrations | EF Core migrations per-schema, idempotent |
| MAINT-06 | Logging strategy | Structured (JSON), correlation IDs, log levels |

## NFR-USAB: Usability Requirements

| ID | Requirement | Implementation |
|----|-------------|----------------|
| USAB-01 | Page load time | < 2 seconds initial, < 500ms subsequent (SPA navigation) |
| USAB-02 | Real-time feedback | Toast notifications for all background operations |
| USAB-03 | Error messages | User-friendly messages, no technical jargon |
| USAB-04 | Responsive design | Desktop-first, tablet-compatible (1024px+) |
| USAB-05 | Accessibility | WCAG 2.1 AA compliance (Radix UI provides this) |

## Testable Properties (PBT-01 Compliance)

### Properties Identified for Unit 1:

| Property | Category | Description |
|----------|----------|-------------|
| Schema resolution round-trip | Round-trip | `resolveTenant(createTenant(info)) == info.schemaName` |
| Tenant isolation invariant | Invariant | For any query Q and tenants A, B: `Q(schemaA)` never returns data from `schemaB` |
| Permission check idempotence | Idempotence | `hasPermission(user, perm) == hasPermission(user, perm)` (no side effects) |
| JWT claims round-trip | Round-trip | `decode(encode(claims)) == claims` |
| File path generation invariant | Invariant | Generated paths never contain `..` or absolute path prefixes |
| SignalR group membership invariant | Invariant | User always in exactly 3 groups: tenant, office, personal |
