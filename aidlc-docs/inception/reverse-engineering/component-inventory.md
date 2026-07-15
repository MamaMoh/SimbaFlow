# Component Inventory

## Application Packages

| Package | Framework | Purpose |
|---------|-----------|---------|
| SimbaFlow.API | net10.0 (Web SDK) | Minimal API host with Carter modules, middleware, DI |
| SimbaFlow.Application | net10.0 (Library) | Pipeline behaviors, interfaces, models |
| SimbaFlow.Domain | net10.0 (Library) | Entities, enums, domain events, base classes |
| SimbaFlow.Infrastructure | net10.0 (Library) | EF Core, Identity, JWT, audit, background jobs |
| SimbaFlow.Shared | net10.0 (Library) | Shared DTOs and models |

## Frontend Package

| Package | Framework | Purpose |
|---------|-----------|---------|
| simba-flow (frontend) | Next.js 15 / React 19 | Full SPA with App Router, shadcn/ui components |

## Test Packages

| Package | Framework | Purpose |
|---------|-----------|---------|
| SimbaFlow.API.Tests | net10.0 (xunit) | Unit tests for behaviors and services |

## Feature Modules (Backend API)

| Module | Carter Module | Endpoints | Domain Focus |
|--------|---------------|-----------|--------------|
| Auth | AuthModule | 7 | Authentication, MFA, sessions |
| Patients | PatientModule | 6 | Patient CRUD, ADT |
| ClinicalWorkspace | ClinicalWorkspaceModule | 18 | Encounters, vitals, notes, diagnoses, alerts |
| Scheduling | SchedulingModule | 8 | Appointments, availability, calendar |
| Orders | OrdersModule | 9 | CPOE (lab + imaging + medication orders) |
| Laboratory | LaboratoryModule | 11 | Lab queue, results, verification, catalog |
| Imaging | ImagingModule | 4 | Radiology worklist, reports |
| Pharmacy | PharmacyModule | 9 | Dispensation, inventory, formulary |
| Billing | BillingModule | 11 | Charges, payments, chargemaster, invoices |
| Inpatient | BedBoardModule | 4 | Bed board, ward census, transfers |
| Staff | StaffModule | 8 | Staff lifecycle management |
| Users | (UserModule) | ~5 | User CRUD |
| Roles | (RoleModule) | ~5 | Role + permission management |
| Locations | (LocationModule) | ~4 | Location hierarchy CRUD |
| Departments | (DepartmentModule) | ~4 | Department CRUD |
| DiagnosisCodes | (DiagnosisCodeModule) | ~3 | ICD code reference |
| MedicalHistory | (MedicalHistoryModule) | ~14 | Patient medical history |
| Reports | (ReportsModule) | ~3 | Reporting |

## Infrastructure Services

| Service | Type | Purpose |
|---------|------|---------|
| ApplicationDbContext | DbContext | Primary data access |
| JwtTokenService | Scoped | JWT generation |
| RefreshTokenService | Scoped | Refresh token CRUD |
| CurrentUserService | Scoped | Resolve current user from HTTP context |
| StaffContext | Scoped | Resolve staff profile from user |
| AuditService | Scoped | Write audit logging |
| ReadAuditService | Singleton | High-throughput read audit (Channel) |
| DomainEventDispatcher | Scoped | Dispatch domain events post-save |
| EncounterChargeService | Scoped | Auto-capture billing charges |
| TokenCleanupService | Hosted | Background expired token cleanup |
| SessionCleanupService | Hosted | Background stale session cleanup |
| ReadAuditBatchWriter | Hosted | Batch-write read audit entries |
| PasswordHistoryValidator | Scoped | Prevent password reuse |
| StaffContextMiddleware | Middleware | Populate IStaffContext per request |

## Total Count
- **Total Backend Packages**: 5
- **Application**: 2 (API + Application)
- **Infrastructure**: 1
- **Domain/Model**: 2 (Domain + Shared)
- **Test**: 1
- **Frontend**: 1
- **Grand Total**: 6 deployable packages + 1 test package
