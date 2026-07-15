# Code Generation Plan — Unit 1: Core Infrastructure

## Unit Context

- **Unit**: Core Infrastructure (Unit 1)
- **Workspace Root**: /Users/mama/Dev/simbaflow
- **Project Type**: Brownfield (existing .NET 10 + Next.js 15 codebase)
- **Strategy**: Delete clinical code first (Pre-Unit), then build infrastructure
- **Stories**: US-11.01–US-11.06, US-8.07

## Dependencies
- None (foundation unit — all other units depend on this)

## Pre-Requisite: Clinical Code Deletion
Before generating new code, all clinical/medical domain code must be removed.

---

## Code Generation Steps

### Phase A: Clinical Code Deletion (Pre-Unit Cleanup)

- [ ] **Step 1**: Delete clinical domain entities
  - Delete: `backend/src/SimbaFlow.Domain/Entities/Clinical/` (entire directory)
  - Delete: `backend/src/SimbaFlow.Domain/Entities/Orders/` (entire directory)
  - Delete: `backend/src/SimbaFlow.Domain/Entities/Pharmacy/` (entire directory)
  - Delete: `backend/src/SimbaFlow.Domain/Entities/Billing/` (entire directory)
  - Delete: `backend/src/SimbaFlow.Domain/Entities/Scheduling/` (entire directory)
  - Delete: `backend/src/SimbaFlow.Domain/Entities/Patient.cs`
  - Delete clinical enums: `ClinicalEnums.cs`, `OrderEnums.cs`, `MedicalHistoryEnums.cs`, `SchedulingEnums.cs`, `AppointmentStatus.cs`, `ScheduleExceptionType.cs`, `VerificationStatus.cs`
  - Keep: `PatientEnums.cs` → rename/repurpose later, `EmploymentStatus.cs`, `LocationType.cs`, `StaffType.cs`, `IdentifierType.cs`, `ImpersonationReason.cs`

- [ ] **Step 2**: Delete clinical API feature modules
  - Delete: `backend/src/SimbaFlow.API/Features/ClinicalWorkspace/` (entire directory)
  - Delete: `backend/src/SimbaFlow.API/Features/Laboratory/` (entire directory)
  - Delete: `backend/src/SimbaFlow.API/Features/Imaging/` (entire directory)
  - Delete: `backend/src/SimbaFlow.API/Features/Pharmacy/` (entire directory)
  - Delete: `backend/src/SimbaFlow.API/Features/Billing/` (entire directory)
  - Delete: `backend/src/SimbaFlow.API/Features/Orders/` (entire directory)
  - Delete: `backend/src/SimbaFlow.API/Features/Inpatient/` (entire directory)
  - Delete: `backend/src/SimbaFlow.API/Features/DiagnosisCodes/` (entire directory)
  - Delete: `backend/src/SimbaFlow.API/Features/MedicalHistory/` (entire directory)
  - Delete: `backend/src/SimbaFlow.API/Features/Scheduling/` (entire directory)
  - Delete: `backend/src/SimbaFlow.API/Features/Reports/` (entire directory)
  - Delete: `backend/src/SimbaFlow.API/Features/Patients/` (entire directory)
  - Keep: `Auth/`, `Staff/`, `Users/`, `Roles/`, `Departments/`, `Locations/`

- [ ] **Step 3**: Delete clinical infrastructure services
  - Delete: `backend/src/SimbaFlow.Infrastructure/Billing/` (entire directory)
  - Delete: `backend/src/SimbaFlow.Infrastructure/ClinicalAlerts/` (entire directory)
  - Delete: `backend/src/SimbaFlow.Infrastructure/Scheduling/` (entire directory)
  - Delete: `backend/src/SimbaFlow.Application/Common/Behaviors/ClinicalAuthorizationBehavior.cs`
  - Delete: `backend/src/SimbaFlow.Application/Common/Interfaces/IEncounterChargeService.cs`
  - Delete: `backend/src/SimbaFlow.Application/Common/Interfaces/IRequireClinicalAuthorization.cs`

- [ ] **Step 4**: Delete all existing migrations (fresh start)
  - Delete: `backend/src/SimbaFlow.Infrastructure/Migrations/` (entire directory)

- [ ] **Step 5**: Clean up ApplicationDbContext — remove all clinical DbSets
  - Modify: `backend/src/SimbaFlow.Infrastructure/Persistence/ApplicationDbContext.cs`
  - Remove all clinical, scheduling, orders, pharmacy, billing DbSets
  - Keep: Identity, Locations, Staff, Audit DbSets

- [ ] **Step 6**: Clean up DependencyInjection.cs — remove clinical service registrations
  - Modify: `backend/src/SimbaFlow.Infrastructure/Persistence/DependencyInjection.cs`
  - Remove: `IEncounterChargeService` registration
  - Keep: Identity, JWT, audit, background jobs registrations

- [ ] **Step 7**: Clean up Program.cs — remove clinical seeders
  - Modify: `backend/src/SimbaFlow.API/Program.cs`
  - Remove: PatientSeeder, SchedulingSeeder, DiagnosisCodeSeeder, LabTestSeeder, ChargemasterSeeder, InsuranceSeeder, MedicationCatalogSeeder, PharmacyInventorySeeder
  - Keep: PermissionSeeder, AdminSeeder, DepartmentSeeder, LocationSeeder, RolePermissionSeeder

- [ ] **Step 8**: Clean up ServiceExtensions.cs — remove clinical behaviors
  - Modify: `backend/src/SimbaFlow.API/Extensions/ServiceExtensions.cs`
  - Remove: `ClinicalAuthorizationBehavior` from MediatR pipeline
  - Keep: ValidationBehavior, AuthorizationBehavior, PerformanceLogBehavior, AuditBehavior

- [ ] **Step 9**: Delete clinical frontend pages and components
  - Delete clinical pages: `frontend/app/(main)/clinical/`, `imaging/`, `lab-results/`, `orders/`, `medications/`, `inpatient/`, `pharmacy/`, `billing/`, `my-schedule/`, `schedule-blocks/`, `staff-scheduling/`, `inbox/`, `tasks/`
  - Delete: `frontend/app/(main)/master-data/` (entire directory)
  - Keep: `overview/`, `patients/` (rename later), `reports/`, `settings/`, `(admin)/`

- [ ] **Step 10**: Verify clean build compiles
  - Backend should compile without errors after cleanup
  - Frontend should build (even with `ignoreBuildErrors: true` temporarily)

### Phase B: Core Infrastructure — Backend

- [ ] **Step 11**: Add new NuGet packages
  - Add to `SimbaFlow.Infrastructure.csproj`: Serilog.AspNetCore, Serilog.Sinks.Console, Serilog.Sinks.File, SixLabors.ImageSharp, Polly (Microsoft.Extensions.Http.Resilience)
  - Add to `SimbaFlow.API.csproj`: Microsoft.AspNetCore.RateLimiting (built-in)
  - Add to test project: FsCheck, FsCheck.Xunit

- [ ] **Step 12**: Create new domain entities for multi-tenancy
  - Create: `backend/src/SimbaFlow.Domain/Entities/Tenancy/TenantSettings.cs` (value object)
  - Modify: `backend/src/SimbaFlow.Domain/Entities/Identity/TenantInfo.cs` (add SchemaName, SubscriptionStatus, Settings, etc.)
  - Create: `backend/src/SimbaFlow.Domain/Enums/TenantStatus.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Tenancy/SystemConfiguration.cs`
  - Create: `backend/src/SimbaFlow.Domain/Entities/Tenancy/ExchangeRate.cs`

- [ ] **Step 13**: Create tenant schema resolution infrastructure
  - Create: `backend/src/SimbaFlow.Infrastructure/Persistence/TenantConnectionInterceptor.cs`
  - Create: `backend/src/SimbaFlow.Infrastructure/Persistence/TenantSchemaResolver.cs`
  - Create: `backend/src/SimbaFlow.Application/Common/Interfaces/ITenantSchemaResolver.cs`
  - Create: `backend/src/SimbaFlow.Application/Common/Interfaces/ITenantContext.cs`
  - Modify: `ApplicationDbContext.cs` — add interceptor registration

- [ ] **Step 14**: Create tenant provisioning logic
  - Create: `backend/src/SimbaFlow.Infrastructure/Persistence/TenantMigrationService.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Tenants/TenantModule.cs` (Carter)
  - Create: `backend/src/SimbaFlow.API/Features/Tenants/Commands/ProvisionTenantCommand.cs`
  - Create: `backend/src/SimbaFlow.API/Features/Tenants/Queries/GetTenantsQuery.cs`

- [ ] **Step 15**: Create WorkflowAuthorizationBehavior
  - Create: `backend/src/SimbaFlow.Application/Common/Behaviors/WorkflowAuthorizationBehavior.cs`
  - Create: `backend/src/SimbaFlow.Application/Common/Interfaces/IRequireOfficeAccess.cs`
  - Register in ServiceExtensions.cs MediatR pipeline

- [ ] **Step 16**: Create SignalR hub infrastructure
  - Create: `backend/src/SimbaFlow.Infrastructure/RealTime/SimbaFlowHub.cs`
  - Create: `backend/src/SimbaFlow.Infrastructure/RealTime/ISignalRBroadcaster.cs`
  - Create: `backend/src/SimbaFlow.Infrastructure/RealTime/SignalRBroadcaster.cs`
  - Register SignalR services in DependencyInjection.cs
  - Map hub endpoint in Program.cs

- [ ] **Step 17**: Create file storage service
  - Create: `backend/src/SimbaFlow.Application/Common/Interfaces/IFileStorageService.cs`
  - Create: `backend/src/SimbaFlow.Infrastructure/Services/LocalFileStorageService.cs`
  - Register in DependencyInjection.cs

- [ ] **Step 18**: Configure Serilog structured logging
  - Modify: `backend/src/SimbaFlow.API/Program.cs` — add Serilog configuration
  - Create: `backend/src/SimbaFlow.Infrastructure/Logging/PiiDestructuringPolicy.cs`

- [ ] **Step 19**: Configure rate limiting
  - Modify: `backend/src/SimbaFlow.API/Program.cs` — add rate limiting middleware
  - Define policies: login (5/min), refresh (30/min), general (100/min), upload (10/min)

- [ ] **Step 20**: Update authentication — add tenant claims to JWT
  - Modify: `backend/src/SimbaFlow.Infrastructure/Identity/JwtTokenService.cs` — add tenant_id, office_id, permissions claims
  - Modify: `backend/src/SimbaFlow.Infrastructure/Identity/CurrentUserService.cs` — extract tenant/office from claims

- [ ] **Step 21**: Update permissions — add labour export permission codes
  - Modify: `backend/src/SimbaFlow.Infrastructure/Persistence/Seeds/PermissionSeeder.cs`
  - Add all new permission codes (candidate.*, workflow.*, embassy.*, etc.)

- [ ] **Step 22**: Create new database migration
  - Create initial migration for updated schema (TenantInfo changes, SystemConfiguration, ExchangeRate, removed clinical tables)

- [ ] **Step 23**: Add health checks (deep)
  - Create: `backend/src/SimbaFlow.Infrastructure/HealthChecks/TenantSchemaHealthCheck.cs`
  - Create: `backend/src/SimbaFlow.Infrastructure/HealthChecks/FileStorageHealthCheck.cs`
  - Register in ServiceExtensions.cs

### Phase C: Core Infrastructure — Frontend

- [ ] **Step 24**: Add frontend dependencies
  - Add: `@microsoft/signalr` to package.json
  - Add: `vitest`, `fast-check` to devDependencies
  - Pin: `zustand`, `immer`, `sonner` to exact versions

- [ ] **Step 25**: Create SignalR connection provider
  - Create: `frontend/lib/signalr/signalr-provider.tsx`
  - Create: `frontend/lib/signalr/use-signalr.ts` (hook)
  - Create: `frontend/lib/signalr/types.ts`

- [ ] **Step 26**: Create tenant context provider
  - Create: `frontend/lib/tenant/tenant-provider.tsx`
  - Create: `frontend/lib/tenant/use-tenant.ts` (hook)
  - Create: `frontend/lib/tenant/use-permissions.ts` (hook)

- [ ] **Step 27**: Update main layout — new navigation
  - Modify: `frontend/app/(main)/layout.tsx` — wrap with SignalRProvider, TenantProvider
  - Modify: Sidebar component — replace clinical menu with labour export menu items
  - Add permission gating to navigation items

- [ ] **Step 28**: Create notification listener component
  - Create: `frontend/components/notifications/notification-listener.tsx`
  - Integrate with Sonner toast system

- [ ] **Step 29**: Create tenant admin page (system admin)
  - Create: `frontend/app/(main)/(admin)/tenants/page.tsx`
  - Create: `frontend/components/tenants/tenant-list.tsx`
  - Create: `frontend/components/tenants/create-tenant-form.tsx`

- [ ] **Step 30**: Update next.config.mjs — remove `ignoreBuildErrors`
  - Modify: `frontend/next.config.mjs` — set `ignoreBuildErrors: false`
  - Add `output: 'standalone'` for Docker deployment

### Phase D: Docker & Deployment

- [ ] **Step 31**: Create Docker configuration files
  - Create: `backend/Dockerfile`
  - Create: `frontend/Dockerfile`
  - Create: `docker-compose.yml` (root)
  - Create: `.env.example` (root)
  - Create: `scripts/init-db.sql`
  - Create: `scripts/backup.sh`

### Phase E: Tests

- [ ] **Step 32**: Backend unit tests for tenant infrastructure
  - Create: `backend/tests/SimbaFlow.API.Tests/Services/TenantSchemaResolverTests.cs`
  - Create: `backend/tests/SimbaFlow.API.Tests/Behaviors/WorkflowAuthorizationBehaviorTests.cs`
  - Create: `backend/tests/SimbaFlow.API.Tests/Services/FileStorageServiceTests.cs`

- [ ] **Step 33**: Property-based tests (PBT)
  - Create: `backend/tests/SimbaFlow.API.Tests/Properties/TenantSchemaProperties.cs` (FsCheck)
  - Properties: schema name generation invariant, file path safety invariant, JWT claims round-trip

### Phase F: Documentation

- [ ] **Step 34**: Generate code summary documentation
  - Create: `aidlc-docs/construction/core-infrastructure/code/code-summary.md`
  - List all created/modified files with purposes

---

## Story Traceability

| Step(s) | Story | Coverage |
|---------|-------|----------|
| 12-14 | US-8.07 (Provision Tenant) | Full |
| 13, 20 | US-11.04 (Tenant Data Isolation) | Full |
| 16 | US-11.03 (Real-Time Notifications) | Infrastructure only (full in Unit 7) |
| 20 | US-11.01 (User Login) | Adaptation (existing auth + tenant claims) |
| 20 | US-11.02 (MFA) | Existing (no changes needed) |
| 20 | US-11.05 (Password Change) | Existing (no changes needed) |
| 20 | US-11.06 (Session Management) | Existing (no changes needed) |

## Estimated Scope
- **Total Steps**: 34
- **Files Deleted**: ~100+ (clinical code)
- **Files Created**: ~30
- **Files Modified**: ~10
- **Tests Created**: ~5 test files
