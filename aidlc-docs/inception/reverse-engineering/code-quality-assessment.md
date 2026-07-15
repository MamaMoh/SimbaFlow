# Code Quality Assessment

## Test Coverage

| Area | Status | Notes |
|------|--------|-------|
| Overall | Fair | Test project exists but coverage unclear |
| Unit Tests | Present | `SimbaFlow.API.Tests` with Behaviors + Services tests |
| Integration Tests | None | No integration test project |
| Frontend Tests | None | No test framework configured |

## Code Quality Indicators

### Positive Indicators
- **Consistent architecture** — Clean Architecture strictly followed
- **CQRS everywhere** — Every operation goes through MediatR pipeline
- **Validation on every request** — FluentValidation + pipeline behavior
- **Soft delete** — No hard deletes; audit trail preserved
- **Optimistic concurrency** — PostgreSQL xmin-based row versioning
- **Domain events** — Decoupled side effects
- **Strong typing** — Nullable enabled, no `dynamic` usage (except one spot in BedBoard)
- **Security-first** — JWT + refresh rotation, MFA, IP restrictions, password history
- **Consistent response pattern** — `Result<T>` everywhere
- **XML documentation** — Key classes and interfaces documented

### Areas for Improvement
- **Some inline queries** — BedBoardModule has raw LINQ instead of going through MediatR
- **Mixed concerns** — Some Carter modules inject `IApplicationDbContext` directly alongside MediatR
- **`dynamic` cast** — BedBoardModule uses `(object as dynamic)?.Property` which is fragile
- **No frontend testing** — Zero test framework in Next.js project
- **TypeScript build errors ignored** — `ignoreBuildErrors: true` in next.config.mjs
- **`NODE_TLS_REJECT_UNAUTHORIZED=0`** — Dev script disables TLS verification

## Patterns Identified

### Good Patterns
1. **Vertical Slice + CQRS** — Features are self-contained, discoverable
2. **Pipeline Behaviors** — Cross-cutting concerns without polluting handlers
3. **Permission-based access** — Granular `IRequirePermission` marker interface
4. **Clinical authorization** — Department affiliation enforcement via `IRequireClinicalAuthorization`
5. **Audit trail** — Both write mutations and read access logged
6. **Result pattern** — No exception-based flow control
7. **Background job separation** — Token/session cleanup as hosted services
8. **Seed data strategy** — Comprehensive seeders for dev environment
9. **Security headers** — CSP, HSTS, X-Frame-Options in Next.js config

### Patterns to Preserve for Labour Export Domain
1. **Authentication infrastructure** (JWT, MFA, sessions) — directly reusable
2. **RBAC system** (permissions, roles, pipeline enforcement) — directly reusable
3. **Audit trail** (write + read) — directly reusable
4. **CQRS + MediatR pipeline** — directly reusable
5. **Result<T> pattern** — directly reusable
6. **BaseEntity** (audit fields, soft delete, concurrency) — directly reusable
7. **Staff management** — adaptable for agency employees
8. **Multi-tenancy** (TenantInfo) — maps to multi-agency support
9. **Department system** — maps to agency offices/branches
10. **Location hierarchy** — maps to overseas offices/partner agencies
11. **Domain events** — maps to workflow stage transitions

### Patterns to Replace
1. **Clinical entities** (Patient, Encounter, Vitals, Notes) → Candidate, WorkflowRecord
2. **Medical Orders** (CPOE, Lab, Imaging) → Remove entirely
3. **Pharmacy** → Remove entirely
4. **Clinical Authorization** (department affiliation) → Office/Branch authorization
5. **Billing charges** → Commission/Fee tracking

## Technical Debt

| Issue | Location | Severity | Notes |
|-------|----------|----------|-------|
| TypeScript errors ignored | next.config.mjs | Medium | TODO comment says "re-enable once medical-specific code is replaced" |
| Direct DbContext in modules | BedBoardModule.cs | Low | Should go through MediatR for consistency |
| Dynamic casts | BedBoardModule.cs | Low | Type-safe projections preferred |
| No integration tests | backend/tests/ | Medium | Only unit tests for behaviors |
| No frontend tests | frontend/ | Medium | No test framework at all |
| TLS disabled in dev | package.json scripts | Low | Dev-only, but risky habit |
| `latest` version pins | package.json | Low | zustand, immer, sonner unpinned |

## Reusability Score for Labour Export Pivot

| Layer | Reusability | Notes |
|-------|-------------|-------|
| Infrastructure (Auth/JWT/Identity) | **95%** | Directly reusable as-is |
| Application (Pipeline behaviors) | **90%** | Remove ClinicalAuthorizationBehavior, add WorkflowAuthorizationBehavior |
| Domain (Base classes, Identity) | **80%** | Keep base, replace clinical entities |
| API (Feature structure) | **30%** | Architecture reusable, content replaced |
| Frontend (Shell, Auth, UI) | **60%** | Layout, auth flow, UI components reusable; pages replaced |
| Database Schema | **40%** | Identity/Staff tables kept, clinical tables dropped |
