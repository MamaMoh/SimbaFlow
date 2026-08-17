# SimbaFlow — Complete Database Design

## Overview

SimbaFlow uses a **single PostgreSQL database** with **multiple schemas** for tenant isolation. The `public` schema holds platform-level shared data, while each agency gets its own schema containing their isolated business data.

---

## Schema Communication Model

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     PostgreSQL Database: simbaflow                        │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │  PUBLIC SCHEMA (Shared across all tenants)                          │ │
│  │                                                                     │ │
│  │  Identity & Auth:                                                   │ │
│  │  ┌─────────────┐  ┌──────────────┐  ┌─────────────────────────┐  │ │
│  │  │AspNetUsers   │  │AspNetRoles   │  │AspNetUserRoles          │  │ │
│  │  │• TenantId ───┼──┼──> Tenants   │  │(links users to system   │  │ │
│  │  │  (nullable)  │  │              │  │ roles like SuperAdmin)   │  │ │
│  │  └─────────────┘  └──────────────┘  └─────────────────────────┘  │ │
│  │                                                                     │ │
│  │  Platform:                                                          │ │
│  │  ┌─────────────┐  ┌──────────────┐  ┌─────────────────────────┐  │ │
│  │  │Tenants       │  │Permissions   │  │ExchangeRates            │  │ │
│  │  │(agency list) │  │(49 codes)    │  │(multi-currency)         │  │ │
│  │  └─────────────┘  └──────────────┘  └─────────────────────────┘  │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│         ▲                                                                │
│         │ SET search_path TO "tenant_xyz", "public"                      │
│         │ (allows tenant queries to also access public tables)           │
│         │                                                                │
│  ┌──────┴─────────────────────────────────────────────────────────────┐ │
│  │  TENANT SCHEMA: tenant_ethio_star                                   │ │
│  │                                                                     │ │
│  │  ┌──────────────┐  ┌─────────────────┐  ┌──────────────────────┐ │ │
│  │  │candidates     │  │workflow_events   │  │workflow_definitions  │ │ │
│  │  │candidate_docs │  │workflow_snapshots│  │workflow_stages       │ │ │
│  │  └──────────────┘  └─────────────────┘  │workflow_statuses     │ │ │
│  │                                          │transition_rules      │ │ │
│  │  ┌──────────────┐  ┌─────────────────┐  └──────────────────────┘ │ │
│  │  │tenant_roles   │  │commissions      │                           │ │
│  │  │role_perms     │  │journal_entries  │  ┌──────────────────────┐ │ │
│  │  │user_roles     │  │accounts         │  │offices               │ │ │
│  │  └──────────────┘  └─────────────────┘  │partner_agencies      │ │ │
│  │                                          └──────────────────────┘ │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │  TENANT SCHEMA: tenant_addis_manpower (same structure)              │ │
│  └────────────────────────────────────────────────────────────────────┘ │
│                                                                          │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │  TENANT SCHEMA: tenant_golden_gate (same structure)                 │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## How Schemas Communicate

### Key Principle: Schemas Do NOT Communicate Directly

Tenant schemas are **completely isolated** from each other. They never reference or join with other tenant schemas. Communication only happens through the application layer.

### Communication Patterns:

| From | To | How | Example |
|------|-----|-----|---------|
| Tenant → Public | Via `search_path` | Tenant queries can read `public.Permissions` (included in search_path) |
| Public → Tenant | Via application code | SuperAdmin API fetches from specific schema by switching context |
| Tenant A → Tenant B | **NEVER** | Complete isolation — no cross-tenant queries possible |
| Application → Any Schema | Via `TenantConnectionInterceptor` | Sets `search_path` based on JWT `tenant_id` |

### The `search_path` Mechanism:

```sql
-- When agency user "hana@addismanpower.com" makes a request:
SET search_path TO "tenant_addis_manpower", "public";

-- Now: SELECT * FROM candidates → queries tenant_addis_manpower.candidates
-- Now: SELECT * FROM "Permissions" → queries public."Permissions" (fallback)
-- Cannot access: tenant_ethio_star.candidates (not in search_path)
```

---

## Complete Table Catalog

### PUBLIC SCHEMA — Platform Tables

#### `AspNetUsers` (ASP.NET Core Identity)
All users across all tenants. The `TenantId` column determines which agency they belong to.

| Column | Type | Description |
|--------|------|-------------|
| Id | UUID | Primary key |
| UserName | VARCHAR | Login username |
| Email | VARCHAR | Email address |
| FirstName | VARCHAR(100) | First name |
| LastName | VARCHAR(100) | Last name |
| MiddleName | VARCHAR(100) | Middle name (optional) |
| TenantId | UUID (nullable) | **Links user to agency** (NULL = platform admin) |
| OfficeId | UUID (nullable) | Which office/branch |
| IsSuperAdmin | BOOLEAN | Platform administrator flag |
| IsActive | BOOLEAN | Account active/disabled |
| IsFirstLogin | BOOLEAN | Force password change |
| MustChangePassword | BOOLEAN | Force password change |
| RequireMfa | BOOLEAN | MFA required |
| PreferredLanguage | VARCHAR | "en" or "am" |
| BotLinked | BOOLEAN | Telegram/WhatsApp linked |
| TelegramChatId | VARCHAR | Telegram chat ID |
| LastLoginAt | TIMESTAMP | Last login time |
| PasswordHash | VARCHAR | Hashed password |
| ... | ... | Other Identity columns |

#### `Tenants`
Agency registry. Each row represents one labour export agency.

| Column | Type | Description |
|--------|------|-------------|
| Id | UUID | Primary key |
| Name | VARCHAR(200) | Agency display name |
| Slug | VARCHAR(50) | URL-safe identifier (unique) |
| SchemaName | VARCHAR(63) | PostgreSQL schema name (unique, immutable) |
| ContactEmail | VARCHAR | Primary contact email |
| ContactPhone | VARCHAR | Primary contact phone |
| SubscriptionStatus | SMALLINT | 0=Active, 1=Suspended, 2=Deactivated |
| MaxUsers | INT | Maximum user accounts allowed |
| Settings | JSONB | Per-agency config (languages, currencies, etc.) |
| ProvisionedAt | TIMESTAMP | When agency was created |
| ProvisionedBy | VARCHAR | Who created it |
| CreatedAt | TIMESTAMP | Record creation time |
| IsDeleted | BOOLEAN | Soft delete flag |

#### `Permissions`
Master list of all system permission codes. Agencies assign these to their custom roles.

| Column | Type | Description |
|--------|------|-------------|
| Id | UUID | Primary key |
| Code | VARCHAR(100) | Permission code (e.g., "candidate.read") |
| Name | VARCHAR(200) | Human-readable name |
| Module | VARCHAR(50) | Module group (candidate, workflow, embassy...) |
| IsActive | BOOLEAN | Whether this permission is available |

**Current Permission Codes (49 total):**
```
candidate.read, candidate.create, candidate.update, candidate.delete
workflow.view, workflow.execute, workflow.configure
embassy.read, embassy.update
lmis.read, lmis.update
travel.read, travel.update
arrival.read, arrival.update, arrival.exception
commission.read, commission.create, commission.update
accounting.read, accounting.post, accounting.reconcile
staff.read, staff.create, staff.update, staff.terminate
office.read, office.write
partner.read, partner.create, partner.update
notification.configure, notification.send
bot.configure, bot.use
report.view, report.export, report.schedule
tenant.provision, tenant.manage
audit.read
settings.read, settings.write
users.read, users.write
role.read, role.write
auth.login
system.admin
```

#### `Departments`
Organizational structure for staff grouping.

| Column | Type | Description |
|--------|------|-------------|
| Id | UUID | Primary key |
| Code | VARCHAR(20) | Department code |
| Name | VARCHAR(100) | Department name |
| Description | TEXT | Description |
| ParentDepartmentId | UUID (nullable) | Hierarchical parent |
| IsActive | BOOLEAN | Active flag |

#### `Locations`
Physical locations (offices, branches).

| Column | Type | Description |
|--------|------|-------------|
| Id | UUID | Primary key |
| Code | VARCHAR(20) | Location code |
| Name | VARCHAR(100) | Location name |
| Type | SMALLINT | Headquarters, BranchOffice, OverseasOffice, etc. |
| Description | TEXT | Description |
| ParentLocationId | UUID (nullable) | Hierarchical parent |
| SortOrder | INT | Display order |

#### `AuditLogs`
Every write operation recorded.

| Column | Type | Description |
|--------|------|-------------|
| Id | UUID | Primary key |
| UserId | VARCHAR | Who performed the action |
| Action | VARCHAR | What was done |
| EntityName | VARCHAR | Which entity was affected |
| EntityId | VARCHAR | ID of affected entity |
| OldValues | TEXT | Previous state (JSON) |
| NewValues | TEXT | New state (JSON) |
| Timestamp | TIMESTAMP | When it happened |

---

### TENANT SCHEMA — Per-Agency Tables

Each agency schema contains identical table structures. All data in these tables belongs exclusively to that agency.

#### `candidates`
The core entity — a person going through the labour export process.

| Column | Type | Description |
|--------|------|-------------|
| id | UUID | Primary key |
| first_name | VARCHAR(100) | First name |
| last_name | VARCHAR(100) | Last name |
| middle_name | VARCHAR(100) | Middle name |
| passport_number | VARCHAR(20) | Passport (unique per agency) |
| labour_id | VARCHAR(50) | Government labour registration ID |
| date_of_birth | DATE | Date of birth |
| gender | SMALLINT | 0=Male, 1=Female |
| nationality | VARCHAR(100) | Nationality |
| phone_number | VARCHAR(20) | Phone number |
| email | VARCHAR(200) | Email |
| address | VARCHAR(500) | Street address |
| city | VARCHAR(100) | City |
| country | VARCHAR(100) | Country |
| country_of_travel | VARCHAR(100) | Destination country |
| office_name | VARCHAR(200) | Overseas employer/office |
| contract_date | DATE | Contract date |
| office_id | UUID | Which branch registered this candidate |
| photo_path | VARCHAR(500) | Photo file path |
| status | SMALLINT | 0=Active, 1=Archived |
| current_stage_id | UUID | Current workflow stage (denormalized) |
| current_stage_name | VARCHAR(100) | Stage name (denormalized) |
| current_status_values | TEXT/JSONB | Current status per track (denormalized) |
| visible_in_stages | TEXT/ARRAY | Mirror view visibility |
| registered_at | TIMESTAMP | When registered |
| registered_by | VARCHAR | Who registered |
| created_at | TIMESTAMP | Record creation |
| updated_at | TIMESTAMP | Last update |
| is_deleted | BOOLEAN | Soft delete |

#### `candidate_documents` (planned)
Files attached to candidates.

| Column | Type | Description |
|--------|------|-------------|
| id | UUID | Primary key |
| candidate_id | UUID | FK → candidates |
| file_name | VARCHAR(255) | Stored filename |
| original_file_name | VARCHAR(255) | Original upload name |
| content_type | VARCHAR(100) | MIME type |
| file_path | VARCHAR(500) | File system path |
| document_type | SMALLINT | Passport, Photo, Contract, CV, etc. |
| file_size_bytes | BIGINT | File size |
| uploaded_at | TIMESTAMP | Upload time |

#### `workflow_events` (planned)
Immutable event stream for candidate state changes.

| Column | Type | Description |
|--------|------|-------------|
| id | UUID | Primary key |
| candidate_id | UUID | Which candidate |
| sequence_number | BIGINT | Order within candidate (unique per candidate) |
| event_type | SMALLINT | Registered, StageTransitioned, StatusUpdated, etc. |
| from_stage_id | UUID | Previous stage |
| from_stage_name | VARCHAR | Previous stage name |
| to_stage_id | UUID | New stage |
| to_stage_name | VARCHAR | New stage name |
| data | JSONB | Event-specific payload |
| user_id | UUID | Who triggered |
| user_name | VARCHAR | Username |
| timestamp | TIMESTAMP | When it happened |
| notes | TEXT | Optional notes |

#### `workflow_definitions` (planned)
Per-agency workflow configuration.

| Column | Type | Description |
|--------|------|-------------|
| id | UUID | Primary key |
| name | VARCHAR(200) | Workflow name |
| version | INT | Version number |
| is_active | BOOLEAN | Active workflow |

#### `workflow_stages` (planned)
Stages in the workflow pipeline.

| Column | Type | Description |
|--------|------|-------------|
| id | UUID | Primary key |
| workflow_definition_id | UUID | FK → workflow_definitions |
| name | VARCHAR(100) | Stage name |
| sort_order | INT | Display order |
| stage_type | SMALLINT | Simple, ParallelTrack, MilestoneSequence |
| is_initial_stage | BOOLEAN | Entry point |
| is_final_stage | BOOLEAN | Completion stage |

#### `workflow_transition_rules` (planned)
Conditions for moving between stages.

| Column | Type | Description |
|--------|------|-------------|
| id | UUID | Primary key |
| source_stage_id | UUID | From stage |
| target_stage_id | UUID | To stage |
| button_label | VARCHAR(100) | "To Embassy", "To LMIS" |
| conditions | JSONB | Field conditions that must be true |
| required_fields | TEXT[] | Fields that must have values |
| allowed_roles | TEXT[] | Which roles can execute |
| remove_from_source | BOOLEAN | Remove from source view on transition |

#### `tenant_roles`
Custom roles defined by this agency.

| Column | Type | Description |
|--------|------|-------------|
| id | UUID | Primary key |
| name | VARCHAR(100) | Role display name |
| code | VARCHAR(100) | Role code (unique per agency) |
| description | TEXT | What this role can do |
| is_system_role | BOOLEAN | System-created (cannot delete) |
| is_active | BOOLEAN | Active/disabled |
| sort_order | INT | Display order |

#### `tenant_role_permissions`
Which permissions are assigned to each role.

| Column | Type | Description |
|--------|------|-------------|
| tenant_role_id | UUID | FK → tenant_roles |
| permission_code | VARCHAR(100) | References public.Permissions.Code |
| granted_at | TIMESTAMP | When granted |
| granted_by | VARCHAR | Who granted |

#### `tenant_user_roles`
Which users have which roles within this agency.

| Column | Type | Description |
|--------|------|-------------|
| user_id | UUID | References public.AspNetUsers.Id |
| tenant_role_id | UUID | FK → tenant_roles |
| assigned_at | TIMESTAMP | When assigned |
| assigned_by | VARCHAR | Who assigned |

#### `commissions` (planned)
Financial tracking per candidate.

| Column | Type | Description |
|--------|------|-------------|
| id | UUID | Primary key |
| candidate_id | UUID | FK → candidates |
| total_fee | DECIMAL | Total amount due |
| amount_paid | DECIMAL | Amount received |
| balance_due | DECIMAL | Remaining |
| currency | VARCHAR(3) | ETB, USD, SAR, AED |
| status | SMALLINT | Pending, Partial, Settled, Disputed |

#### `accounts` (planned — double-entry accounting)
Chart of accounts for the agency.

| Column | Type | Description |
|--------|------|-------------|
| id | UUID | Primary key |
| code | VARCHAR(20) | Account code |
| name | VARCHAR(200) | Account name |
| type | SMALLINT | Asset, Liability, Revenue, Expense, Equity |
| currency | VARCHAR(3) | Account currency |
| balance | DECIMAL | Current balance |

#### `journal_entries` (planned)
Double-entry bookkeeping transactions.

| Column | Type | Description |
|--------|------|-------------|
| id | UUID | Primary key |
| date | DATE | Transaction date |
| description | TEXT | What this entry is for |
| debit_account_id | UUID | FK → accounts |
| credit_account_id | UUID | FK → accounts |
| amount | DECIMAL | Transaction amount |
| currency | VARCHAR(3) | Transaction currency |

#### `offices` (planned)
Agency branch offices.

| Column | Type | Description |
|--------|------|-------------|
| id | UUID | Primary key |
| name | VARCHAR(200) | Office name |
| address | TEXT | Physical address |
| city | VARCHAR(100) | City |
| country | VARCHAR(100) | Country |
| manager_id | UUID | Office manager user ID |

#### `partner_agencies` (planned)
Overseas employers/partners.

| Column | Type | Description |
|--------|------|-------------|
| id | UUID | Primary key |
| name | VARCHAR(200) | Partner name |
| country | VARCHAR(100) | Partner country |
| contact_person | VARCHAR(200) | Contact name |
| phone | VARCHAR(20) | Contact phone |
| email | VARCHAR(200) | Contact email |

---

## Cross-Schema Reference Rules

### ✅ ALLOWED References

| From (Tenant Schema) | To (Public Schema) | How |
|---|---|---|
| `tenant_user_roles.user_id` | `public.AspNetUsers.Id` | Logical FK (same DB, cross-schema) |
| `tenant_role_permissions.permission_code` | `public.Permissions.Code` | Validated at application level |
| `candidates.office_id` | Resolved via application | No DB-level FK |

### ❌ NEVER Allowed

| Pattern | Why |
|---|---|
| Tenant A → Tenant B | Complete isolation violation |
| `JOIN tenant_x.candidates ON tenant_y.candidates` | Impossible (different search_path) |
| Cross-tenant foreign keys | Not created |

### How SuperAdmin Accesses Tenant Data

```
1. SuperAdmin logs in (no tenant_id in JWT)
2. search_path stays as "public"
3. To view Agency X's data:
   └── Application explicitly queries with schema prefix:
       SELECT * FROM "tenant_ethio_star".candidates
   └── OR switches search_path temporarily per-request
```

---

## Table Count Summary

| Schema | Tables (Current) | Tables (Full System) |
|--------|-----------------|---------------------|
| public | ~15 | ~18 |
| Per tenant | 4 | ~15 |
| **Total (3 tenants)** | **27** | **63** |

---

## Data Volume Estimates (Per Agency)

| Table | Expected Rows | Growth Rate |
|-------|---------------|-------------|
| candidates | 10,000 - 50,000 | ~100/month |
| workflow_events | 50,000 - 500,000 | ~5 per candidate |
| candidate_documents | 20,000 - 100,000 | ~2-5 per candidate |
| commissions | 10,000 - 50,000 | 1 per arrived candidate |
| journal_entries | 50,000+ | ~5 per commission |
| tenant_roles | 5-20 | Rarely changes |
| workflow_definitions | 1-3 | Rarely changes |

---

*Document Version: 1.0 | Last Updated: July 2026*
