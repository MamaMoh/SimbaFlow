# SimbaFlow — Agency SaaS Design Guide

**Purpose:** Review of the current codebase against real labour-export agency operations (Tango-style AppSheet workflow), and a clear target for **architecture**, **database design**, and **UI**.

**Verdict in one line:** Your platform foundation (multi-tenant SaaS, auth, roles, candidate intake) is correct. Do **not** copy AppSheet’s “one table per stage” model into PostgreSQL — keep one candidate record and drive stage boards with a workflow engine. That engine is designed in domain code but not yet implemented.

---

## 1. Verdict: What Is Correct vs What Needs Correction

### Correct (keep building on this)

| Decision | Why it fits labour-export agencies |
|----------|-------------------------------------|
| Multi-tenant SaaS with schema-per-agency | Agencies must never see each other’s candidates, fees, or documents |
| Clean Architecture + CQRS (Carter + MediatR) | Clear boundaries for ERP modules (pipeline, finance, HR) |
| Platform vs tenant DbContexts | Identity/staff live centrally; operational data stays per agency |
| Candidate as aggregate + denormalized stage fields | Fast stage-board queries without copying rows between tables |
| Configurable workflow (stages, transitions, conditions, mirror views) | Agencies differ slightly; AppSheet “buttons” map cleanly to transition rules |
| Permission codes already seeded (`embassy.*`, `lmis.*`, `travel.*`, …) | Matches departmental roles (Embassy officer, Case Executive, Finance) |
| Event stream + snapshots (designed) | Compliance and dispute resolution need full “who changed what” history |

### Incorrect / dangerous if you follow AppSheet literally

| AppSheet pattern | Why it fails as a SaaS DB | Correct SaaS equivalent |
|------------------|---------------------------|-------------------------|
| Separate “view tables” (New Contract, Embassy, LMIS, …) as data stores | Duplicate rows, sync bugs, lost history when “moved” | One `candidates` row; stage boards are **filtered queries** |
| “Copy to Commission” while keeping Arrival | Two source-of-truth rows | Same candidate; open a `commissions` **child record**; Arrival stays a stage visibility |
| Status fields scattered across many sheets | Hard to report, migrate, or audit | `current_status_values` JSONB + typed stage side-tables where needed |
| Action buttons as spreadsheet macros | Not reusable across tenants | `workflow_transition_rules` with conditions / required fields / roles |

### Incomplete (designed, not shipped)

- Workflow API handlers are stubs
- `IWorkflowEngineService` is not fully implemented / registered
- Tenant provisioning does not create full workflow / commission DDL
- Frontend `/workflow/[stageId]` is a placeholder
- Candidate detail page (`/candidates/[id]`) is linked but missing
- Offices, partners, accounting, reports are nav-only

**Bottom line:** Architecture direction is right. Product value for agencies is blocked until the workflow engine + stage boards ship.

---

## 2. Domain Model (How Agency Work Really Maps)

AppSheet uses **stage-gate views**. In SimbaFlow those are **workspaces**, not tables.

```
                    ┌─────────────────────────────────────────┐
                    │           CANDIDATE (1 row forever)      │
                    │  identity + contract + denormalized state │
                    └────────────────────┬────────────────────┘
                                         │
         ┌───────────────────────────────┼───────────────────────────────┐
         ▼                               ▼                               ▼
  WorkflowEvents (append-only)   Stage side-data (optional)      Documents
  who / when / from→to / payload medical, visa, ticket, fees     passport, contract PDF
```

### Operational pipeline (canonical)

```
INTAKE ──► EMBASSY ──► LMIS ──► TICKET ──► DEPARTURE ──► ARRIVAL ──► COMMISSION
 (queue)   (parallel)  (gov)   (flight)   (countdown)   (ground)     (finance)
              │
              ├── Medical track
              ├── Tasheer track
              └── Visa + Case Executive (role-filtered view of same stage)
```

### Critical behaviors from the Tango blueprint (must remain true)

| Behavior | Implementation rule |
|----------|---------------------|
| Intake lands in New Contract queue until manual “To Embassy” | Initial stage = Intake; transition rule with no auto-condition |
| Medical Fit **and** Tasheer Book Done → appear in LMIS **while still in Embassy** | Mirror: add LMIS to `visible_in_stages`; `remove_from_source = false` |
| Visa Ready → Case Executive sees the case | Same stage (Embassy); filter by `visa` status + role permission |
| Visa Issued → “To LMIS” removes from Embassy & Case Executive | Transition with `remove_from_source = true`; clear Embassy from `visible_in_stages` |
| Insurance Paid → LMIS Available | Status update rule / field side-effect, not a new table |
| LMIS Issued → unlock “To Ticket” | Transition condition on milestone status |
| Ticket fields complete → unlock “To Departure” | `required_fields`: book status, destination, flight date |
| Departure Notified vs not | Derived UI alert from `notification_status` + days-until-flight |
| Departed → Arrival; Not Departed → back to Ticket / Canceled | Branching transition rules |
| Returned / Runaway → containment workspace | Exception status + dedicated filtered view (or soft terminal stage) |
| Add to Commission **without** removing from Arrival | Create `commissions` child; keep Arrival in `visible_in_stages` |

---

## 3. Target Architecture

### 3.1 System shape

```
┌──────────────────────────────────────────────────────────────────────┐
│ Clients                                                               │
│  Next.js agency UI  │  (later) Telegram/WhatsApp bot  │  SuperAdmin  │
└───────────────┬──────────────────────────┬───────────────┬───────────┘
                │ BFF /api/proxy           │               │
┌───────────────▼──────────────────────────▼───────────────▼───────────┐
│ SimbaFlow.API (.NET)                                                  │
│  Auth │ Users │ Tenants │ Staff │ Candidates │ Workflow │ Finance     │
│  MediatR pipeline: Validate → Permission → Office → Audit → Handler   │
└───────────────┬──────────────────────────────────────────────────────┘
                │
┌───────────────▼──────────────────────────────────────────────────────┐
│ Domain                                                                │
│  Candidate aggregate │ Workflow engine │ Commission / ledger rules    │
└───────────────┬──────────────────────────────────────────────────────┘
                │
┌───────────────▼──────────────────────────────────────────────────────┐
│ PostgreSQL (single DB)                                                │
│  public          → platform identity, tenants, permissions, staff     │
│  tenant_{slug}   → candidates, workflow_*, commissions, offices…      │
└──────────────────────────────────────────────────────────────────────┘
```

### 3.2 Layer responsibilities (keep as-is)

| Layer | Owns |
|-------|------|
| **API** | Carter modules, validators, HTTP contracts |
| **Application** | Interfaces, `Result<T>`, MediatR behaviors |
| **Domain** | Entities, enums, workflow rules vocabulary, domain events |
| **Infrastructure** | EF contexts, Identity, search_path interceptor, SignalR, files, jobs |

Handlers today live under `Features/` in the API project — acceptable for velocity; move thick workflow logic into Application/Infrastructure services as the engine grows.

### 3.3 Workflow engine (the ERP core)

Treat the engine as the product, not as “another CRUD module.”

**Commands (write):**

1. `RegisterCandidate` → append `Registered` event → set stage = Intake, `visible_in_stages = [Intake]`
2. `UpdateStageStatus` → append `StatusUpdated` → merge into `current_status_values` → evaluate mirror rules
3. `ExecuteTransition` → evaluate rule (conditions, required fields, roles) → append `StageTransitioned` → update stage + visibility
4. `FlagException` → append `ExceptionFlagged` → route to containment visibility
5. `CreateCommissionFromArrival` → create finance child; do **not** remove Arrival visibility

**Queries (read):**

1. `GetStageBoard(stageId)` → candidates where `current_stage_id = stageId OR stageId = ANY(visible_in_stages)`
2. `GetAvailableActions(candidateId)` → transition rules whose conditions pass for current user
3. `GetCandidateTimeline` → events ordered by sequence
4. `GetDepartureCountdown` → flight date − today, notification status

**Transaction rule:** append event + update denormalized candidate fields **in the same DB transaction**. Never update stage columns without an event.

### 3.4 Multi-tenancy (confirmed correct)

```
JWT tenant_id
  → TenantSchemaResolver (Active tenants only)
  → SET search_path TO "tenant_xyz", "public"
```

- Tenant A never joins Tenant B.
- SuperAdmin stays on `public` for provisioning / platform admin.
- Finish dual-context migration: retire legacy `ApplicationDbContext` once Locations/seeders move to `PlatformDbContext`.

### 3.5 What “ERP” means in this product

| ERP area | Scope for v1 | Scope later |
|----------|--------------|-------------|
| Operations pipeline | Stages above | Country-specific branches |
| Document management | Passport, medical, contract, ticket | e-sign |
| People / org | Staff, roles, departments, offices | payroll |
| Partners | Destination offices / foreign agencies | contracts SLAs |
| Finance | Commission ledger + fee balances | full double-entry GL |
| Comms | In-app + SignalR | WhatsApp / Telegram bots |
| Reporting | Stage funnel, departures this week, unpaid insurance | BI exports |

Do **not** build a generic ERP first. Build the pipeline ERP; bolt finance onto Arrival → Commission.

### 3.6 Recommended module boundaries

```
Features/
  Auth, Users, Roles, Tenants, Staff, Departments, Locations   ← mostly done
  Candidates                                                   ← intake done; detail/docs next
  Workflow                                                     ← highest priority
  Offices, Partners                                            ← master data for Country/Office Name
  Commissions / Accounting                                     ← after Arrival works
  Reports, Notifications/Bot                                   ← after boards work
```

---

## 4. Target Database Design

### 4.1 Schema layout

```
simbaflow
├── public                          PlatformDbContext
│   ├── AspNetUsers / Roles / …
│   ├── Tenants, Permissions
│   ├── Departments, Locations (or move Locations → tenant if agency-owned)
│   ├── StaffProfiles (+ identifiers, affiliations)
│   └── AuditLogs
│
└── tenant_{slug}                   TenantDbContext (identical DDL per agency)
    ├── candidates
    ├── candidate_documents
    ├── workflow_definitions
    ├── workflow_stages
    ├── workflow_stage_statuses
    ├── workflow_transition_rules
    ├── parallel_track_definitions
    ├── mirror_view_rules
    ├── stage_mandatory_fields
    ├── workflow_events            (append-only)
    ├── workflow_snapshots
    ├── offices
    ├── partner_agencies
    ├── commissions
    ├── commission_payments        (optional v1.1)
    ├── accounts / journal_entries (v2 accounting)
    ├── tenant_roles
    ├── tenant_role_permissions
    └── tenant_user_roles
```

### 4.2 Core principle: one candidate, many views

**Wrong (AppSheet literal):**

```
new_contracts ──move──► embassy ──move──► lmis ──copy──► commissions
```

**Right:**

```sql
-- Stage board query (Embassy example)
SELECT *
FROM candidates
WHERE is_deleted = false
  AND (
    current_stage_id = :embassyStageId
    OR :embassyStageId = ANY (visible_in_stages)
  );
```

Denormalized fields on `candidates` (already in domain — keep):

| Column | Role |
|--------|------|
| `current_stage_id` / `current_stage_name` | Primary pipeline position |
| `current_status_values` JSONB | e.g. `{"medical":"Fit","tasheer":"BookDone","visa":"Issued","insurance":"Paid","lmis":"Issued"}` |
| `visible_in_stages` UUID[] | Mirror boards (Embassy + LMIS at once) |

Index recommendations:

```sql
CREATE INDEX ix_candidates_stage ON candidates (current_stage_id) WHERE NOT is_deleted;
CREATE INDEX ix_candidates_visible ON candidates USING GIN (visible_in_stages);
CREATE INDEX ix_candidates_status_gin ON candidates USING GIN (current_status_values);
CREATE UNIQUE INDEX ux_candidates_passport ON candidates (passport_number) WHERE NOT is_deleted;
```

### 4.3 Seeded default workflow (maps Tango → config)

Seed one active `workflow_definitions` row per new tenant.

| Stage | Type | Status tracks / milestones |
|-------|------|----------------------------|
| New Contract (Intake) | Simple | (none — queue only) |
| Embassy | ParallelTrack | Medical: Pending/Fit/Fail · Tasheer: Pending/BookDone/Expired · Visa: Pending/Ready/Submitted/Issued/Rejected |
| LMIS | MilestoneSequence | Insurance: Unpaid/Paid · LMIS: Available → Uploaded → CheckVerified → Issued |
| Ticket | Simple | TicketBookStatus, Destination, FlightDate (fields) |
| Departure | Simple | Notification: NotNotified/Notified · FlightState: Pending/Departured/NotDeparted/Canceled |
| Arrival | Simple | Deployment: Arrived/Returned/Runaway |
| Commission | Simple | (finance lives mainly in `commissions` table) |
| Exceptions (optional stage or virtual board) | Simple | Returned / Runaway containment |

**Transition rules (seed):**

| Button | From → To | Conditions | Required fields | Remove from source |
|--------|-----------|------------|-----------------|--------------------|
| To Embassy | Intake → Embassy | — | passport, labour_id, country, office, contract_date | Yes |
| (auto mirror) | Embassy → +LMIS visibility | medical=Fit AND tasheer=BookDone | — | No (mirror rule) |
| To LMIS | Embassy → LMIS | visa=Issued | — | Yes |
| To Ticket | LMIS → Ticket | lmis=Issued | — | Yes |
| To Departure | Ticket → Departure | — | ticket_book_status, destination, flight_date | Yes |
| To Arrival | Departure → Arrival | flight_state=Departured | — | Yes |
| Back to Ticket | Departure → Ticket | flight_state=NotDeparted | — | Yes |
| Add to Commission | Arrival → +Commission record | deployment=Arrived | — | No |

Case Executive is **not** a separate stage: it is Embassy board filtered by `visa IN ('Ready','Submitted')` and role `case_executive` / permission `embassy.update`.

### 4.4 When to use side tables vs JSON status

| Data | Storage | Why |
|------|---------|-----|
| Medical / Tasheer / Visa / Insurance / LMIS milestone labels | JSONB status values | Fast boards; matches AppSheet status cells |
| Flight destination, flight date, ticket PNR | Typed columns on candidate **or** `travel_bookings` 1:1 | Needed for countdown queries & validation |
| Commission amounts, paid/unpaid, disputes | `commissions` table | Financial truth; never only JSON |
| Document binaries | `candidate_documents` + blob/file store | Compliance |
| Appointment dates (medical/tasheer) | Optional `clearance_appointments` | If you need calendars/reminders beyond a status label |

**Rule of thumb:** if Finance or Reporting needs to SUM/GROUP BY it, use a real column/table. If ops only need a stage badge, JSONB is enough for v1.

### 4.5 Suggested `commissions` table (v1)

| Column | Type | Notes |
|--------|------|-------|
| id | UUID | PK |
| candidate_id | UUID | FK → candidates |
| country_of_travel | VARCHAR | Snapshot at creation |
| office_name | VARCHAR | Snapshot |
| contract_date | DATE | Snapshot |
| agency_fee | NUMERIC(18,2) | |
| currency | CHAR(3) | |
| balance_status | SMALLINT | Open / Partial / Settled / Disputed |
| notes | TEXT | |
| created_at / created_by | | |

Arrival row stays; commission is a related financial case file.

### 4.6 Offices & partners (replace free-text drift)

Today `office_name` / `country_of_travel` are free text on candidates — fine for intake MVP, bad for reporting.

Target:

- `offices` — agency branches that register candidates (`office_id` already on Candidate)
- `partner_agencies` — overseas receiving agents (AppSheet **OFFICE**; Directive 1126/2018 ወኪል)
- Candidate references `partner_agency_id` + `destination_country_code` (keep denormalized names for display)

**Compliance detail (levels, ትስስር caps, Art. 40, agreements):** see [`PARTNER_AGENCY_COMPLIANCE.md`](./PARTNER_AGENCY_COMPLIANCE.md).

### 4.7 Provisioning & migrations (fix this gap)

Current risk: `TenantDbContext` maps workflow tables, but tenant provision SQL only creates candidates + role tables.

**Required:**

1. EF (or SQL template) migrations for **all** tenant tables
2. On `ProvisionTenantCommand`: create schema → apply tenant DDL → seed default workflow + system roles
3. Stop relying on ad-hoc partial SQL

---

## 5. Target UI Design

### 5.1 Product information architecture

```
Dashboard
Candidates                    ← master list + register + detail
Workflow Pipeline
  New Contracts
  Embassy                     ← tabs: All | Medical | Tasheer | Visa | Case Executive
  LMIS
  Tickets
  Departures                  ← countdown emphasis
  Arrivals
  Commissions
  Exceptions                  ← Returned / Runaway
Finance
  Accounting (v2)
  Reports
Administration
  Staff & Users / Roles
  Offices / Partners
  Workflow Config             ← SuperAdmin or Agency Owner
  Tenants (platform only)
  Settings
```

Match existing `nav-items.ts`; fill the stubs rather than inventing a new IA.

### 5.2 Design system (keep current stack)

| Piece | Choice |
|-------|--------|
| Framework | Next.js App Router |
| Components | shadcn/ui + Radix |
| Tables | TanStack Table + existing `DataTable` |
| Forms | react-hook-form + Zod |
| Fetch | SWR + `/api/proxy` |
| Realtime | SignalR for board updates / departure alerts |

Preserve the Ethiopian brand accents already in CSS variables. Prefer dense operational tables over marketing dashboards.

### 5.3 Screen patterns

#### A. Stage board (replaces AppSheet “view tables”)

Every `/workflow/[stageId]` page shares one layout:

```
┌──────────────────────────────────────────────────────────────┐
│ Stage title + count     [Filters] [Search] [Export]          │
├──────────────────────────────────────────────────────────────┤
│ Optional tabs (Embassy: All / Medical / Visa / Case Exec)    │
├──────────────────────────────────────────────────────────────┤
│ DataTable                                                    │
│  Name │ Passport │ Country │ Office │ Status chips │ Days │  │
│  …    │ …        │ …       │ …      │ Fit · BookDone │ ── │  │
│                                              [Actions ▾]     │
└──────────────────────────────────────────────────────────────┘
```

**Row actions** come from `GetAvailableActions` — never hardcode buttons per page beyond stage-specific edit fields.

**Status chips** render keys from `current_status_values` with stage-configured colors.

#### B. Candidate detail (missing — build early)

`/candidates/[id]`:

```
┌────────────────────┬─────────────────────────────────────────┐
│ Profile header     │ Stage progress stepper (Intake→…→Comm) │
│ photo, passport,   ├─────────────────────────────────────────┤
│ labour ID, phone   │ Current stage panel (edit statuses)     │
├────────────────────┤ Available action buttons                │
│ Docs tabs          ├─────────────────────────────────────────┤
│ Timeline           │ Timeline (workflow_events)              │
│ Commission (if any)│ Documents gallery / upload              │
└────────────────────┴─────────────────────────────────────────┘
```

This is the single place staff resolve incomplete AppSheet “open the record” work.

#### C. Intake

Keep `/candidates/register` + create sheet. Mandatory fields aligned to Tango:

- Full name, passport, labour ID  
- Country of travel, office / partner  
- Contract date  
- Registering office (`office_id`)

On success → land in New Contracts board (not a generic “success toast only”).

#### D. Embassy parallel tracks

Inline editors or a side sheet with three sections:

1. Medical — appointment + Fit/Fail  
2. Tasheer — appointment + BookDone/Expired  
3. Visa — Ready / Submitted / Issued / Rejected  

When Fit + BookDone, show a subtle banner: “Also visible on LMIS (mirror)” — teaches users the AppSheet behavior without magic.

Case Executive tab: same table, filtered; primary action = set Visa → Submitted.

#### E. Departure countdown board

Special columns:

- Flight date  
- Remaining days (computed client or server)  
- Alert cell: if not Notified → “n days left, notify candidate” (destructive/warning tone)

#### F. Commission

Separate board + link from Arrival action “Add to Commission”. Columns: candidate, country, fee, balance, dispute flag. Opening a row edits finance fields without removing Arrival history.

### 5.4 Role-based UI

| Role example | Sees |
|--------------|------|
| Data entry | Intake, Candidates create |
| Embassy officer | Embassy board + medical/tasheer/visa updates |
| Case executive | Embassy → Case Executive tab |
| LMIS officer | LMIS board |
| Travel desk | Tickets + Departures |
| Arrival desk | Arrivals + Exceptions |
| Finance | Commissions + reports |
| Agency owner | Workflow config + all boards (read) |
| Platform SuperAdmin | Tenants provisioning only (not day-to-day cases) |

Hide nav items via existing claim filtering; disable actions server-side even if UI is bypassed.

### 5.5 UX rules for ops staff (non-negotiable)

1. **One primary action** per row state — match AppSheet’s gated buttons.  
2. **Never lose a candidate** — soft delete / Canceled / Exception, always queryable.  
3. **Optimistic UI only for status chips**; transitions wait for server result (conditions may fail).  
4. **Realtime refresh** on boards when another user transitions the same stage (SignalR).  
5. **Amharic/English** ready strings (user already has `PreferredLanguage`).

### 5.6 Dashboard (Overview)

Replace static zeros with:

- Funnel counts per stage  
- Departures in next 7 days (unnotified highlighted)  
- Unpaid insurance in LMIS  
- Open commission balances  
- Exceptions (Returned/Runaway) needing attention  

---

## 6. Gap Analysis (Code vs Target)

| Area | Status | Priority |
|------|--------|----------|
| Auth, MFA, JWT, refresh | Done | — |
| Tenant provision + schema isolation | Done (DDL incomplete) | P0 fix DDL |
| Users / tenant roles / permissions | Done | — |
| Staff HR module | Done (trim clinical leftovers later) | P3 |
| Candidate CRUD + docs API | Mostly done | P1 detail UI |
| Workflow domain model | Designed | — |
| Workflow engine + API | Stubs | **P0** |
| Default workflow seed | Missing | **P0** |
| Stage board UI | Stub route | **P0** |
| Candidate detail + timeline UI | Missing | **P1** |
| Offices / partners | Nav only | P1 |
| Commissions table + UI | Missing | P2 |
| Accounting GL | Planned only | P3 |
| Bots / notifications engine | Fields only | P3 |
| Reports | Placeholder | P2 |

---

## 7. Recommended Build Order

### Phase 0 — Make tenancy honest
1. Tenant EF migrations / provision template for all tenant tables  
2. Seed default Tango-equivalent workflow on provision  
3. Finish Platform vs Tenant context split; remove dual-write confusion  

### Phase 1 — Pipeline MVP (agency can run a case end-to-end)
1. Implement `IWorkflowEngineService` (append event + denormalize + evaluate mirrors)  
2. Wire Workflow commands/queries (no more stubs)  
3. Stage board UI + available actions  
4. Candidate detail + timeline  
5. Embassy parallel status editing + Case Executive filter  

### Phase 2 — Logistics & money
1. Ticket required fields + Departure countdown + notify action  
2. Arrival + exception board  
3. Commission create-from-arrival + commission board  

### Phase 3 — ERP depth
1. Offices & partner agencies master data  
2. Reports / exports  
3. Accounting (journal entries)  
4. WhatsApp/Telegram notify candidate  

---

## 8. Architecture Decision Records (short)

### ADR-1: Views are queries, not tables
Accepted. AppSheet view tables become workflow stage boards over one candidate aggregate.

### ADR-2: Configurable workflow over hardcoded stage services
Accepted. Agencies vary; transition rules + JSON status cover 90% of Tango logic without new C# per stage. Add side tables only for finance/travel facts.

### ADR-3: Schema-per-tenant over row-level `tenant_id` on all tables
Accepted for strong isolation and simpler agency backup. Cost: provision/migrate every schema — automate it.

### ADR-4: Event-sourced transitions with denormalized read model
Accepted. Events = audit; candidate columns = board performance.

### ADR-5: Commission is a child record, not a moved row
Accepted. Matches “stay on Arrival + appear in Commission.”

---

## 9. Summary Checklist for You

**Architecture — correct direction**
- [x] Multi-tenant SaaS  
- [x] Clean architecture / CQRS  
- [x] Permissioned modules  
- [ ] Finish workflow engine (blocker)  
- [ ] Complete tenant DDL on provision  

**Database — correct model if you avoid AppSheet duplication**
- [x] One candidate + visibility array + status JSON  
- [x] Workflow config tables designed  
- [ ] Seed Tango transitions  
- [ ] Commissions / offices / partners tables  
- [ ] GIN indexes for boards  

**UI — shell exists; ops product does not yet**
- [x] Nav for full pipeline  
- [x] Candidate list/register  
- [ ] Stage boards with gated actions  
- [ ] Candidate detail + timeline  
- [ ] Departure alerts + commission screens  

---

*This guide is the target design for labour-export agency SaaS/ERP work on SimbaFlow. Existing docs (`SYSTEM_ARCHITECTURE.md`, `DATABASE_DESIGN.md`) remain useful references; where they conflict with “AppSheet = separate tables,” this guide wins.*

*Last updated: July 2026*
