# Foreign Employment Agency ERP — Architecture & Database Design

**Stack:** Next.js (frontend) · .NET (backend API) · PostgreSQL (database)
**Scale target:** 1,500 offices, multi-tenant, single shared database

---

## 1. System Overview

The system is a multi-tenant ERP for foreign employment (maid/worker placement) agencies. A super-admin (you, the platform owner) manages 1,500+ **Offices**. Each office has its own **Admin**, who creates **Staff** accounts and assigns them **Roles**. Staff process **Candidates** through a linear placement pipeline (registration → medical/embassy → LMIS → ticket → departure → arrival → post-arrival outcomes), plus supporting modules (complaints, commission, office finance).

The core insight from your workflow: candidates move through a **staged pipeline**, where each stage has one or more **tasks**, each task has its **own status enum**, and moving a candidate to the next stage is an **explicit, confirmed handoff** — not just a filter. Certain status transitions are further restricted to specific roles (e.g., only Case Executive can set Embassy Status to SUBMITTED).

---

## 2. Tenancy Model

**Chosen approach: Shared database, shared schema, row-level multi-tenancy.**

- One PostgreSQL database, one set of tables, every tenant-owned row carries an `office_id`.
- Enforced two ways:
  1. **Application layer** — every query/repository method is automatically scoped by `office_id` (middleware injects the current user's office into the query context; this should be non-optional/impossible to forget, e.g. via a base repository class or EF Core global query filter in .NET).
  2. **Database layer (defense in depth)** — PostgreSQL **Row-Level Security (RLS)** policies on every tenant table, keyed to a session variable (`app.current_office_id`) set at the start of each request. Even if application code has a bug, the DB refuses to return rows outside the current office.
- Super-admin (platform) users bypass office scoping (a `is_platform_admin` flag) to see across all offices — this powers your central dashboard.

**Why not database-per-tenant or schema-per-tenant:** with 1,500 offices and a 2-person technical team, per-tenant migrations, connection pooling, and monitoring become an operational burden that shared-schema + RLS avoids, while still giving strong isolation guarantees.

---

## 3. High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        Clients                                │
│   Next.js Web App (Office staff, Admin, Super-Admin portal)   │
└───────────────────────────┬────────────────────────────────┘
                            │ HTTPS / REST (JSON)
┌───────────────────────────▼────────────────────────────────┐
│                    .NET Web API (ASP.NET Core)                │
│  ┌───────────────┐ ┌────────────────┐ ┌────────────────────┐ │
│  │ Auth &        │ │ Candidate &     │ │ Pipeline / Task     │ │
│  │ Identity       │ │ Office Module   │ │ Engine              │ │
│  │ (JWT, RBAC)    │ │ (CRUD, files)   │ │ (stage transitions, │ │
│  │                │ │                 │ │ permission checks)  │ │
│  └───────────────┘ └────────────────┘ └────────────────────┘ │
│  ┌───────────────┐ ┌────────────────┐ ┌────────────────────┐ │
│  │ Reporting /    │ │ Document Gen    │ │ Integration Layer   │ │
│  │ Dashboard API  │ │ (CV PDF, Excel  │ │ (Wafid, Tasheer,    │ │
│  │                │ │  exports)       │ │ Insurance, LMIS gov,│ │
│  │                │ │                 │ │  Musaned - future)  │ │
│  └───────────────┘ └────────────────┘ └────────────────────┘ │
└───────────────────────────┬────────────────────────────────┘
                            │
        ┌───────────────────┼────────────────────┐
        ▼                   ▼                     ▼
┌───────────────┐  ┌────────────────┐   ┌──────────────────────┐
│ PostgreSQL     │  │ Object Storage  │   │ Background Jobs      │
│ (RLS enabled)  │  │ (photos, scans, │   │ (SMS reminders,      │
│                │  │  PDFs, Excel)   │   │  day-count checks,   │
│                │  │  S3-compatible  │   │  future API polling) │
└───────────────┘  └────────────────┘   └──────────────────────┘
                                                    │
                                          ┌─────────▼─────────┐
                                          │ SMS Gateway         │
                                          │ (candidate notices) │
                                          └────────────────────┘
```

**Notes:**
- **Passport OCR/MRZ reading** and **Chrome extension for Wafid autofill** are client-adjacent features: OCR can run as a .NET backend service (e.g. via an MRZ-parsing library) triggered on passport image upload; the Chrome extension is a separate small codebase that calls a dedicated "get candidate data for autofill" API endpoint.
- **Background job runner** (e.g. Hangfire, since you're on .NET) handles: daily Tasheer/flight day-count calculations, SMS dispatch, and later, polling external APIs (Wafid, insurance, government LMIS) where webhooks aren't available.
- **Ethiopian calendar conversion** (needed for Depart tab check-in display) is a small utility library used both in the API (for stored/derived values) and frontend (for display).

---

## 4. Roles & Permissions Matrix

| Role | Key Screens | Core Actions |
|---|---|---|
| **Super-Admin (platform)** | Cross-office dashboard | Register/manage offices, view all data, no candidate editing |
| **Office Admin** | Staff management | Register staff, assign roles, assign task-column ownership |
| **Receptionist / Data Clerk** | New Application, Applicants List | Register candidates (incremental form), capture photos + passport OCR, generate CV PDF |
| **IT Personnel** | Applicants List, My Task (Embassy, LMIS) | Select candidate → mark Selected; update Medical, Tasheer, Embassy Status (Ready/Issued/Rejected), Insurance, LMIS Status; confirm stage handoffs; manage Arrived status (Manager can too) |
| **Case Executive** | My Task → Embassy Ready Files | Set Embassy Status = Submitted only |
| **Training & Guidance** | Depart (view-only) | View only, no edits |
| **Citizen Grievance & Monitoring** | Complaints | File free-text complaint against a specific candidate |
| **Deputy Manager** | Purchasing, Invoices, Payroll; Arrival & Commission (view); can send Arrived → Commission | Office financial administration |
| **Accountant** | Purchasing, Invoices, Payroll | Office financial administration (shared module with Deputy Manager) |
| **Manager (Office Admin's top role)** | Full dashboard, Arrival, Commission, Statistics | All statistics/reporting, update Arrived status, send Arrived → Commission, update Commission status |

**Design principle:** Role determines *screen access*; a separate **Task Assignment** table determines *which specific task-columns* a given staff member can act on within a shared screen (per your note that Admin chooses which IT Personnel handles Medical vs. Tasheer vs. Embassy Status, etc.), and a **Status Transition Permission** table governs which role can move a task from one specific status value to another (e.g. Embassy Status → SUBMITTED is Case-Executive-only).

---

## 5. The Pipeline / Task Engine (core concept)

Every candidate has a **current stage**. Stages, in order:

```
INTAKE → EMBASSY → LMIS → TICKET → DEPART → ARRIVED → (RETURNED | ON_DUTY | RUNAWAY)
                                                    ↘ COMMISSION (parallel branch from Arrived)
```

Rules encoded from your description:
- A candidate enters **EMBASSY** once Selected by IT Personnel.
- Within EMBASSY: **Medical** and **Tasheer** are independent tasks; **Embassy Status** field is locked/inactive until Medical = FIT and Tasheer = DONE.
- Embassy Status transitions: READY (IT Personnel) → SUBMITTED (Case Executive only) → ISSUED / REJECTED (IT Personnel only).
- When Embassy Status = ISSUED, system prompts "Send to LMIS?" — on confirm, candidate leaves Embassy view, appears only in LMIS view (**stage transition**, logged).
- Within LMIS: **Insurance** (PAID/UNPAID) and **LMIS Status** (SUBMITTED → PAYMENT VERIFIED → CHECKED → VERIFIED → ISSUED) are tracked; when ISSUED, prompt "Send to Ticket?".
- Within TICKET: booking fields + Ticket Status (NOT BOOKED → BOOKED); when complete, prompt "Send to Depart?".
- Within DEPART: Departure Status (NOTIFIED / DEPART / NOT DEPART), SMS day-count reminders, Ethiopian-calendar check-in display; when DEPART confirmed, prompt "Send to Arrived?". Visible read-only to Training & Guidance.
- **ARRIVED is terminal-but-persistent** — candidate record stays here permanently; Candidate Status (ON DUTY / RETURNED / RUNAWAY) is just a field update, not a stage move.
- From ARRIVED, Manager/Deputy Manager can additionally **send a copy/link to COMMISSION** (Commission Status: PAID / UNPAID / REQUESTED) — a parallel tracking list, not a removal.
- **RETURNED** is a genuinely separate screen (reason, date, return ticket, plus pulled-through candidate identity/sponsor fields), reached when Candidate Status = RETURNED.
- **Complaints** are free-text, tied to a candidate, filed by Citizen Grievance & Monitoring — independent of pipeline stage.

Every stage-move and status change is written to an **audit/history table**, which both protects you operationally and feeds the Manager dashboard statistics directly.

---

## 6. Database Design

Below is the core schema. Types are PostgreSQL. `office_id` appears on every tenant-scoped table for RLS. Not every field from your intake form is listed individually where a table's purpose is clear — the full ~50-field candidate form maps into the grouped tables below.

### 6.1 Platform & Office

```sql
offices (
  id UUID PK,
  name TEXT,
  email TEXT,
  phone TEXT,
  license_number TEXT UNIQUE,
  gov_system_username TEXT,
  gov_system_password_encrypted TEXT,
  logo_url TEXT,
  office_level SMALLINT CHECK (office_level BETWEEN 1 AND 5),
  manager_name TEXT,
  manager_phone TEXT,
  manager_email TEXT,
  status TEXT DEFAULT 'active',        -- active / suspended
  created_at TIMESTAMPTZ,
  updated_at TIMESTAMPTZ
)

roles (
  id UUID PK,
  code TEXT UNIQUE,      -- 'admin','receptionist','it','case_executive',
                          -- 'training','grievance','deputy_manager','accountant','manager'
  name TEXT
)

staff (
  id UUID PK,
  office_id UUID FK -> offices,
  username TEXT,
  password_hash TEXT,
  full_name TEXT,
  phone TEXT,
  email TEXT,
  is_active BOOLEAN DEFAULT true,
  created_at TIMESTAMPTZ,
  UNIQUE (office_id, username)
)

staff_roles (                          -- many-to-many: a staff member can hold >1 role if needed
  staff_id UUID FK -> staff,
  role_id UUID FK -> roles,
  PRIMARY KEY (staff_id, role_id)
)

task_assignments (                     -- admin-configured: which staff owns which task-column
  id UUID PK,
  office_id UUID FK -> offices,
  staff_id UUID FK -> staff,
  task_type TEXT,        -- 'medical','tasheer','embassy_status','insurance','lmis_status',
                          -- 'ticket','depart','arrived','commission', etc.
  created_at TIMESTAMPTZ
)

status_transition_permissions (        -- which role may set which task_type to which value
  id UUID PK,
  task_type TEXT,
  to_status TEXT,
  allowed_role_code TEXT FK -> roles.code
)
```

### 6.2 Candidate Core

```sql
candidates (
  id UUID PK,
  office_id UUID FK -> offices,
  file_number TEXT,                    -- App# / File No.
  application_no TEXT,
  photo_3x4_url TEXT,
  photo_full_url TEXT,
  passport_scan_url TEXT,
  full_name TEXT,
  passport_no TEXT,
  passport_type TEXT,
  date_of_birth DATE,
  place_of_birth TEXT,
  date_of_issue DATE,
  date_of_expiry DATE,
  place_of_issue TEXT,
  phone TEXT,
  phone2 TEXT,
  religion TEXT,
  gender TEXT,
  marital_status TEXT,
  occupation TEXT,
  qualification TEXT,
  city TEXT,
  region TEXT,
  subcity TEXT,
  woreda TEXT,
  house_no TEXT,
  nationality TEXT DEFAULT 'Ethiopia',
  labor_id TEXT,
  biometric_id TEXT,
  registered_by_staff_id UUID FK -> staff,
  current_stage TEXT,   -- INTAKE / EMBASSY / LMIS / TICKET / DEPART / ARRIVED
  is_active BOOLEAN DEFAULT true,
  created_at TIMESTAMPTZ,
  updated_at TIMESTAMPTZ
)

candidate_sponsor_visa (
  candidate_id UUID PK FK -> candidates,
  visa_number TEXT,
  visa_type TEXT,
  sponsor_id TEXT,
  sponsor_name TEXT,
  sponsor_name_arabic TEXT,
  sponsor_phone TEXT,
  sponsor_address TEXT,
  agent TEXT,
  national_id TEXT,
  email TEXT,
  contract_no TEXT,
  wakala_no TEXT,
  sticker_visa_no TEXT,
  signed_on DATE,
  cac_center TEXT,
  certified_date DATE,
  certificate_no TEXT,
  training_type TEXT
)

candidate_relatives (
  id UUID PK,
  candidate_id UUID FK -> candidates,
  relative_name TEXT,
  relative_phone TEXT,
  relative_kinship TEXT,
  region TEXT, city TEXT, subcity TEXT, house_no TEXT,
  gender TEXT,
  birth_date DATE
)

candidate_skills (
  candidate_id UUID PK FK -> candidates,
  english_level TEXT,
  arabic_level TEXT,
  experience_abroad TEXT,
  works_in TEXT,
  salary NUMERIC,
  children_count SMALLINT,
  height NUMERIC,
  weight NUMERIC,
  reference_no TEXT,
  remarks TEXT,
  can_iron BOOLEAN, can_sew BOOLEAN, can_babysit BOOLEAN, can_childcare BOOLEAN,
  can_art_cooking BOOLEAN, can_clean BOOLEAN, can_wash BOOLEAN, can_cook BOOLEAN
)

candidate_stage_history (
  id UUID PK,
  candidate_id UUID FK -> candidates,
  from_stage TEXT,
  to_stage TEXT,
  moved_by_staff_id UUID FK -> staff,
  moved_at TIMESTAMPTZ
)
```

### 6.3 Embassy Stage (Medical, Tasheer, Embassy Status)

```sql
candidate_medical (
  candidate_id UUID PK FK -> candidates,
  status TEXT,               -- BOOKED / FIT / ON_PROGRESS / UNFIT
  source TEXT DEFAULT 'manual',   -- manual / wafid_api
  appointment_date DATE,
  updated_by_staff_id UUID FK -> staff,
  updated_at TIMESTAMPTZ
)

candidate_tasheer (
  candidate_id UUID PK FK -> candidates,
  status TEXT,               -- BOOKED / DONE / EXPIRED
  appointment_date DATE,
  updated_by_staff_id UUID FK -> staff,
  updated_at TIMESTAMPTZ
)

candidate_embassy_status (
  candidate_id UUID PK FK -> candidates,
  status TEXT,               -- READY / SUBMITTED / ISSUED / REJECTED
  updated_by_staff_id UUID FK -> staff,
  updated_at TIMESTAMPTZ
)
```

### 6.4 LMIS Stage (Insurance, LMIS Status)

```sql
candidate_insurance (
  candidate_id UUID PK FK -> candidates,
  status TEXT,               -- PAID / UNPAID
  source TEXT DEFAULT 'manual',
  effective_date DATE,
  insured_form JSONB,        -- title, father/grandfather name, full form payload
  updated_at TIMESTAMPTZ
)

candidate_lmis (
  candidate_id UUID PK FK -> candidates,
  status TEXT,               -- SUBMITTED / PAYMENT_VERIFIED / CHECKED / VERIFIED / ISSUED
  source TEXT DEFAULT 'manual',
  issue_date DATE,
  updated_by_staff_id UUID FK -> staff,
  updated_at TIMESTAMPTZ
)
```

### 6.5 Ticket & Depart

```sql
candidate_ticket (
  candidate_id UUID PK FK -> candidates,
  status TEXT,                -- NOT_BOOKED / BOOKED
  flight_destination TEXT,
  flight_date TIMESTAMPTZ,
  arrival_date TIMESTAMPTZ,
  ticket_office TEXT,
  source TEXT DEFAULT 'manual',
  updated_at TIMESTAMPTZ
)

candidate_depart (
  candidate_id UUID PK FK -> candidates,
  status TEXT,                 -- NOTIFIED / DEPART / NOT_DEPART
  check_in_time TIMESTAMPTZ,
  notified_at TIMESTAMPTZ,
  source TEXT DEFAULT 'manual',
  updated_at TIMESTAMPTZ
)
```

### 6.6 Arrived / Returned / Commission / Complaints

```sql
candidate_arrival (
  candidate_id UUID PK FK -> candidates,
  arrival_date TIMESTAMPTZ,
  candidate_status TEXT,       -- ON_DUTY / RETURNED / RUNAWAY
  updated_by_staff_id UUID FK -> staff,
  updated_at TIMESTAMPTZ
)

candidate_returned (
  id UUID PK,
  candidate_id UUID FK -> candidates,
  return_reason TEXT,
  return_date DATE,
  return_ticket_info TEXT,
  created_by_staff_id UUID FK -> staff,
  created_at TIMESTAMPTZ
)

candidate_commission (
  candidate_id UUID PK FK -> candidates,
  status TEXT,                -- PAID / UNPAID / REQUESTED
  amount NUMERIC,
  sent_by_staff_id UUID FK -> staff,
  updated_at TIMESTAMPTZ
)

candidate_complaints (
  id UUID PK,
  candidate_id UUID FK -> candidates,
  office_id UUID FK -> offices,
  complaint_text TEXT,
  filed_by_staff_id UUID FK -> staff,
  created_at TIMESTAMPTZ
)
```

### 6.7 Notifications

```sql
sms_notifications (
  id UUID PK,
  candidate_id UUID FK -> candidates,
  trigger_type TEXT,          -- tasheer_reminder / flight_reminder
  scheduled_for TIMESTAMPTZ,
  sent_at TIMESTAMPTZ,
  status TEXT                 -- pending / sent / failed
)
```

### 6.8 Office Finance (Deputy Manager / Accountant)

```sql
purchases (
  id UUID PK, office_id UUID FK -> offices,
  item TEXT, amount NUMERIC, vendor TEXT,
  requested_by_staff_id UUID FK -> staff,
  status TEXT, created_at TIMESTAMPTZ
)

invoices (
  id UUID PK, office_id UUID FK -> offices,
  invoice_no TEXT, amount NUMERIC, due_date DATE,
  status TEXT, created_at TIMESTAMPTZ
)

payroll (
  id UUID PK, office_id UUID FK -> offices,
  staff_id UUID FK -> staff,
  period_month DATE, base_salary NUMERIC, bonuses NUMERIC,
  deductions NUMERIC, net_pay NUMERIC,
  status TEXT, created_at TIMESTAMPTZ
)
```

### 6.9 Documents & Exports (audit trail of generated files)

```sql
generated_documents (
  id UUID PK,
  candidate_id UUID FK -> candidates,
  doc_type TEXT,        -- cv_pdf / tasheer_excel / arrival_group_excel
  file_url TEXT,
  generated_by_staff_id UUID FK -> staff,
  generated_at TIMESTAMPTZ
)
```

---

## 7. Reporting / Manager Dashboard

All the statistics you listed (no-visa vs. visa, medical passed/in-process, ready-for-embassy, submitted, rejected, needs-ticket, ready-for-departure, arrived-by-period, returned, runaway, departed-by-office, sent-by-destination-country, commission breakdown) can be computed as **PostgreSQL views or materialized views** joining `candidates` + `current_stage` + the per-stage task tables, filtered by `office_id`, date range, and destination. Because every status change is timestamped and logged (`candidate_stage_history` + `updated_at` on each task table), these are straightforward aggregate queries — no separate "analytics" schema needed at this scale, though materialized views (refreshed every few minutes) will keep the dashboard fast once data volume grows.

---

## 8. Future Integration Points (designed for, not built yet)

| Integration | Where it plugs in |
|---|---|
| Wafid.com (medical) | `candidate_medical.source = 'wafid_api'`; Chrome extension autofill using a dedicated API endpoint that returns candidate data in Wafid's expected field order |
| Tasheer (visa stamping) | Excel export generator matching Tasheer's required column format, pulling from `candidates` + `candidate_sponsor_visa` |
| Insurance provider | `candidate_insurance.source = 'insurance_api'` |
| Government LMIS system | `candidate_lmis.source = 'gov_lmis_api'` |
| Musaned | Candidate intake auto-fill, once API access is granted |

Each integration is additive: the manual-entry path always remains the fallback, and a `source` column on each task table records whether the value came from a human or an API, without changing the schema shape.

---

## 9. Open Items to Confirm With Your Technical Co-Founders

1. Object storage choice for photos/scans/PDFs (S3-compatible — AWS S3, or a local/Ethiopian provider).
2. SMS gateway provider (local telecom API for Ethiopia/Gulf-region delivery).
3. Whether Super-Admin dashboard is a separate Next.js app or a role-gated section of the same app.
4. Exact list of any additional pipelines beyond Embassy/LMIS/Ticket/Depart/Arrived you mentioned existing "others" for — worth a follow-up session before backend work starts.
