# Infrastructure Design — Unit 4: Travel, Departure & Arrival

## Deployment context

Unit 4 runs in the **same Docker Compose stack** (api + frontend + postgres). No new containers, volumes, networks, or messaging brokers (bot = Unit 7).

Infrastructure work is:

1. Tenant-schema tables: ExceptionCase, InvestigationNote, LiabilityAssignment, Commission
2. Workflow definition upgrader (confirm Ticket/Departure/Arrival/Commission transitions + RemoveFromSource flags)
3. Platform permission seed (`travel.*`)
4. Carter modules + DI (NoOp notifier)
5. Frontend named routes (app code only)

---

## 1. Database schema delta (tenant)

### 1a. New tables

```sql
CREATE TABLE exception_cases (
  id UUID PRIMARY KEY,
  candidate_id UUID NOT NULL REFERENCES candidates(id),
  type INT NOT NULL,              -- Returned | Runaway
  status INT NOT NULL,            -- Open | UnderInvestigation | Resolved | Closed
  opened_at TIMESTAMPTZ NOT NULL,
  opened_by_user_id UUID NOT NULL,
  closed_at TIMESTAMPTZ NULL,
  resolution_summary TEXT NULL,
  financial_impact_amount NUMERIC(18,2) NULL,
  financial_impact_currency VARCHAR(8) NULL,
  created_at TIMESTAMPTZ NOT NULL,
  updated_at TIMESTAMPTZ NULL,
  created_by UUID NULL,
  updated_by UUID NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX ix_exception_cases_status
  ON exception_cases (status) WHERE is_deleted = FALSE;

CREATE INDEX ix_exception_cases_candidate
  ON exception_cases (candidate_id) WHERE is_deleted = FALSE;

-- At most one Open case per candidate (partial unique)
CREATE UNIQUE INDEX ux_exception_cases_candidate_open
  ON exception_cases (candidate_id)
  WHERE is_deleted = FALSE AND status = 0; -- Open

CREATE TABLE investigation_notes (
  id UUID PRIMARY KEY,
  exception_case_id UUID NOT NULL REFERENCES exception_cases(id),
  author_user_id UUID NOT NULL,
  body TEXT NOT NULL,
  created_at TIMESTAMPTZ NOT NULL,
  updated_at TIMESTAMPTZ NULL,
  created_by UUID NULL,
  updated_by UUID NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX ix_investigation_notes_case
  ON investigation_notes (exception_case_id) WHERE is_deleted = FALSE;

CREATE TABLE liability_assignments (
  id UUID PRIMARY KEY,
  exception_case_id UUID NOT NULL REFERENCES exception_cases(id),
  party INT NOT NULL,
  amount NUMERIC(18,2) NOT NULL,
  currency VARCHAR(8) NOT NULL,
  notes TEXT NULL,
  assigned_at TIMESTAMPTZ NOT NULL,
  created_at TIMESTAMPTZ NOT NULL,
  updated_at TIMESTAMPTZ NULL,
  created_by UUID NULL,
  updated_by UUID NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX ix_liability_assignments_case
  ON liability_assignments (exception_case_id) WHERE is_deleted = FALSE;

CREATE TABLE commissions (
  id UUID PRIMARY KEY,
  candidate_id UUID NOT NULL REFERENCES candidates(id),
  status INT NOT NULL DEFAULT 0,  -- Open
  country_of_travel VARCHAR(128) NULL,
  office_name VARCHAR(256) NULL,
  contract_date DATE NULL,
  opened_at TIMESTAMPTZ NOT NULL,
  opened_by_user_id UUID NOT NULL,
  created_at TIMESTAMPTZ NOT NULL,
  updated_at TIMESTAMPTZ NULL,
  created_by UUID NULL,
  updated_by UUID NULL,
  is_deleted BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE UNIQUE INDEX ux_commissions_candidate
  ON commissions (candidate_id) WHERE is_deleted = FALSE;
```

### 1b. Status tracks — no new Candidate columns

Ticket / Departure / Arrival fields remain in `current_status_values` JSONB:

`ticket_status`, `destination`, `flight_date`, `notification_status`, `departure_status`, `departure_outcome`, `non_departure_reason`, `canceled`, `notified_at`, `departed_at`, arrival status keys, `commission_linked`

Optional later: expression indexes on `canceled` / `flight_date` if SCALE-41 measured slow.

### 1c. EF Core migration

```
dotnet ef migrations add AddTravelArrivalExceptionCommission \
  --project src/SimbaFlow.Infrastructure \
  --context TenantDbContext \
  --output-dir Migrations/Tenant
```

Applied via existing `ITenantSchemaMigrator.MigrateAllActiveTenantsAsync()` / provision path.

Domain entities under `SimbaFlow.Domain` + `TenantDbContext` DbSets + Fluent configurations.

---

## 2. Workflow definition upgrader

### Problem

Existing tenants may already have Ticket/Departure/Arrival from seed, but Unit 4 must guarantee transition flags and Commission visibility.

### Solution

```
IWorkflowDefinitionUpgrader
  EnsureUnit4ArtifactsAsync(ITenantDbContext, tenantId, ct)
```

| Step | Action |
|------|--------|
| 1 | Load active definition + stages |
| 2 | Ensure stages: Ticket, Departure, Arrival, Commission (by name) |
| 3 | Ensure statuses on Ticket / Departure / Arrival tracks as FD |
| 4 | Ensure transitions: To Departure, To Arrival, Back to Ticket, Add to Commission |
| 5 | **Critical**: Add to Commission `RemoveFromSource = false` |
| 6 | To Departure / To Arrival / Back to Ticket `RemoveFromSource = true` |
| 7 | SaveChanges; idempotent |

Prefer also encoding Unit 4 status labels into seeder for new tenants.

**Hook**: after tenant EF migrate in `TenantSchemaMigrator` (same as Unit 3).

---

## 3. Platform permissions seed

```
travel.ticket
travel.departure
travel.arrival
travel.exception
travel.exception.view
```

Optional default role mappings (new tenants):

| Role | Permissions |
|------|-------------|
| Ticketing Officer | travel.ticket, workflow.execute, candidate.read |
| Departure Officer | travel.departure, workflow.execute, candidate.read |
| Arrival Officer | travel.arrival, travel.exception.view, workflow.execute, candidate.read |
| Investigator | travel.exception, travel.exception.view, travel.arrival (read via exception), candidate.read |
| Office Manager | all travel.* + workflow.view |

Existing tenants: additive codes; Admin assigns.

---

## 4. Application layer wiring

### DI

```
services.AddScoped<ICandidateNotifier, NoOpCandidateNotifier>();
// Travel/Arrival/Exception handlers via MediatR assembly scan
// ExceptionCase + Commission via TenantDbContext
```

### Carter modules

```
Features/Travel/TravelModule.cs
Features/Arrival/ArrivalModule.cs
Features/Exceptions/ExceptionModule.cs
```

Discovery via existing Carter scan — no Program.cs special-case if convention matches.

### Handler transaction pattern

Reuse Unit 3 ambient transaction / `Database.BeginTransactionAsync` for:

- ConfirmDeparted + To Arrival
- NotDeparted + optional Back to Ticket
- FlagException + ExceptionCase insert
- AddToCommission + Commission upsert

### Engine

No mandatory new engine API if handlers compose `UpdateStatusAsync` + `ExecuteTransitionAsync` in one transaction (as NFR Design). Optional helpers deferred unless duplication hurts.

---

## 5. File storage

No path changes. Investigation attachments (if any) reuse:

```
/data/tenants/{slug}/candidates/{candidateId}/{filename}
```

Note entity may store optional document id refs; upload via existing candidate document APIs.

---

## 6. Frontend infrastructure

```
app/(main)/workflow/ticket/page.tsx
app/(main)/workflow/departure/page.tsx
app/(main)/workflow/arrival/page.tsx
app/(main)/workflow/exceptions/page.tsx
app/(main)/workflow/exceptions/[id]/page.tsx
app/(main)/workflow/commission/page.tsx   // stub: shell list / visibility
```

Clients:

```
lib/api/travel.ts
lib/api/arrival.ts
lib/api/exceptions.ts
```

Nav + permission gates; BFF proxy unchanged.

---

## 7. Observability & ops

| Concern | Approach |
|---------|----------|
| Logging | Serilog; log reason enum + outcome, not passport |
| Health | Unchanged |
| Backups | `pg_dump` covers new tables |
| Config | No new env vars for Unit 4 |

---

## 8. Test infrastructure

| Suite | Location |
|-------|----------|
| Example tests | `SimbaFlow.API.Tests/Services/Travel*`, `Arrival*`, `Exception*` |
| PBT | `SimbaFlow.API.Tests/Properties/TravelArrivalProperties.cs` |
| Patterns | Unit 2/3 WorkflowEngine + EmbassyLmis tests |

No new CI jobs — existing `dotnet test`.

---

## 9. Rollback / risk

| Change | Risk | Mitigation |
|--------|------|------------|
| Four new tables | Medium | Additive migration; FK to candidates |
| Partial unique Open exception | Low | Matches BR-X05 |
| Unique Commission per candidate | Low | Idempotent Add to Commission |
| RemoveFromSource=false verify | Medium | Upgrader asserts flag; tests |
| Permission codes | Low | Additive |

No Docker contract changes beyond API rebuild.

---

## 10. Deliverable checklist (for code generation plan)

- [ ] Tenant migration: ExceptionCase, InvestigationNote, LiabilityAssignment, Commission
- [ ] Domain entities + TenantDbContext configuration
- [ ] `WorkflowSeeder` / `WorkflowDefinitionUpgrader` Unit 4 artifacts
- [ ] Platform permission seed `travel.*`
- [ ] `NoOpCandidateNotifier`
- [ ] TravelModule + ArrivalModule + ExceptionModule + validators
- [ ] Frontend boards + exception workspace + API clients
- [ ] Tests (example + FsCheck TEST-40–53)
- [ ] Code summary under `construction/travel-arrival/code/`
