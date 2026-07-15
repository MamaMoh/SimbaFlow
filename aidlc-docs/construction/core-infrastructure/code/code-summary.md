# Code Summary — Unit 1: Core Infrastructure

## Phase A: Clinical Code Deletion (Complete)

### Deleted Directories
- `backend/src/SimbaFlow.Domain/Entities/Clinical/`
- `backend/src/SimbaFlow.Domain/Entities/Orders/`
- `backend/src/SimbaFlow.Domain/Entities/Pharmacy/`
- `backend/src/SimbaFlow.Domain/Entities/Billing/`
- `backend/src/SimbaFlow.Domain/Entities/Scheduling/`
- `backend/src/SimbaFlow.API/Features/ClinicalWorkspace/`
- `backend/src/SimbaFlow.API/Features/Laboratory/`
- `backend/src/SimbaFlow.API/Features/Imaging/`
- `backend/src/SimbaFlow.API/Features/Pharmacy/`
- `backend/src/SimbaFlow.API/Features/Billing/`
- `backend/src/SimbaFlow.API/Features/Orders/`
- `backend/src/SimbaFlow.API/Features/Inpatient/`
- `backend/src/SimbaFlow.API/Features/DiagnosisCodes/`
- `backend/src/SimbaFlow.API/Features/MedicalHistory/`
- `backend/src/SimbaFlow.API/Features/Scheduling/`
- `backend/src/SimbaFlow.API/Features/Reports/`
- `backend/src/SimbaFlow.API/Features/Patients/`
- `backend/src/SimbaFlow.Infrastructure/Billing/`
- `backend/src/SimbaFlow.Infrastructure/ClinicalAlerts/`
- `backend/src/SimbaFlow.Infrastructure/Scheduling/`
- `backend/src/SimbaFlow.Infrastructure/Migrations/`
- `frontend/app/(main)/clinical/`, `imaging/`, `lab-results/`, `orders/`, `medications/`, `inpatient/`, `pharmacy/`, `billing/`, `my-schedule/`, `schedule-blocks/`, `staff-scheduling/`, `inbox/`, `tasks/`, `master-data/`, `patients/`

### Deleted Files
- `ClinicalAuthorizationBehavior.cs`, `IEncounterChargeService.cs`, `IRequireClinicalAuthorization.cs`
- Clinical enums: `ClinicalEnums.cs`, `OrderEnums.cs`, `MedicalHistoryEnums.cs`, `SchedulingEnums.cs`, `AppointmentStatus.cs`, `ScheduleExceptionType.cs`, `VerificationStatus.cs`, `PatientEnums.cs`
- `Patient.cs` (domain entity)

### Modified Files
- `ApplicationDbContext.cs` — Removed all clinical DbSets, kept Identity/Staff/Location/Audit
- `DependencyInjection.cs` — Removed clinical service registrations
- `Program.cs` — Removed clinical seeders
- `ServiceExtensions.cs` — Removed ClinicalAuthorizationBehavior from pipeline
- `SimbaFlow.Infrastructure.csproj` — Added Serilog, Polly, ImageSharp packages

## Phase B: Backend Infrastructure (Complete)

### Created Files
- `backend/src/SimbaFlow.Domain/Enums/TenantStatus.cs` — Active/Suspended/Deactivated
- `backend/src/SimbaFlow.Domain/Entities/Tenancy/TenantSettings.cs` — Per-agency config (JSONB)
- `backend/src/SimbaFlow.Domain/Entities/Tenancy/SystemConfiguration.cs` — Global key-value config
- `backend/src/SimbaFlow.Domain/Entities/Tenancy/ExchangeRate.cs` — Currency rates
- `backend/src/SimbaFlow.Application/Common/Interfaces/ITenantContext.cs` — Tenant resolution contract
- `backend/src/SimbaFlow.Application/Common/Interfaces/ITenantSchemaResolver.cs` — Schema lookup contract
- `backend/src/SimbaFlow.Application/Common/Interfaces/IFileStorageService.cs` — File ops contract
- `backend/src/SimbaFlow.Application/Common/Interfaces/IRequireOfficeAccess.cs` — Office auth marker
- `backend/src/SimbaFlow.Application/Common/Behaviors/WorkflowAuthorizationBehavior.cs` — Office-level RBAC
- `backend/src/SimbaFlow.Infrastructure/Persistence/TenantSchemaResolver.cs` — Cached schema resolution
- `backend/src/SimbaFlow.Infrastructure/Persistence/TenantConnectionInterceptor.cs` — EF Core search_path
- `backend/src/SimbaFlow.Infrastructure/RealTime/SimbaFlowHub.cs` — SignalR hub
- `backend/src/SimbaFlow.Infrastructure/RealTime/ISignalRBroadcaster.cs` — Broadcast contract + DTOs
- `backend/src/SimbaFlow.Infrastructure/RealTime/SignalRBroadcaster.cs` — Hub context broadcaster
- `backend/src/SimbaFlow.Infrastructure/Services/LocalFileStorageService.cs` — File system storage
- `backend/src/SimbaFlow.API/Features/Tenants/TenantModule.cs` — Tenant CRUD endpoints
- `backend/src/SimbaFlow.API/Features/Tenants/Commands/ProvisionTenantCommand.cs` — Tenant provisioning
- `backend/src/SimbaFlow.API/Features/Tenants/Commands/UpdateTenantStatusCommand.cs` — Status change
- `backend/src/SimbaFlow.API/Features/Tenants/Queries/GetTenantsQuery.cs` — List tenants

## Phase C: Frontend Infrastructure (Partial)

### Created Files
- `frontend/lib/signalr/signalr-provider.tsx` — SignalR connection lifecycle + context
- `frontend/lib/tenant/tenant-provider.tsx` — Tenant context + permission helpers

## Phase D: Docker & Deployment (Complete)

### Created Files
- `docker-compose.yml` — 3-service stack (API, Frontend, PostgreSQL)
- `.env.example` — Environment variable template
- `scripts/init-db.sql` — PostgreSQL initialization
- `scripts/backup.sh` — Nightly backup with GPG encryption

## Remaining Work (Steps 18-23, 26-30, 32-34)
- Serilog configuration in Program.cs
- Rate limiting middleware configuration
- JWT claims update (tenant_id, office_id, permissions)
- Permission seeder update (new labour export permissions)
- Initial EF Core migration creation
- Custom health checks (TenantSchema, FileStorage)
- Frontend: layout update, notification listener, tenant admin page
- Backend unit tests + property-based tests
- Backend Dockerfile, Frontend Dockerfile

## Architecture Achieved
- Schema-per-tenant isolation via EF Core connection interceptor
- SignalR real-time with 3-level group hierarchy
- File storage with tenant-isolated directories and thumbnail generation
- Office-level authorization via WorkflowAuthorizationBehavior
- Tenant provisioning API (SuperAdmin only)
- Docker Compose deployment configuration
- Encrypted nightly backup strategy
