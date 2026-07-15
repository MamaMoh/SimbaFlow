# Requirements Verification Questions

Your specification is detailed and comprehensive. The following questions address areas that need clarification to ensure correct implementation. Please answer each question by filling in the letter choice after the [Answer]: tag.

---

## Question 1
What is the migration strategy for the existing HIS (Hospital) codebase?

A) Complete replacement — Delete all clinical code and start fresh with labour export domain entities

B) Incremental pivot — Keep existing clinical code operational while building labour export features alongside it, then remove clinical code later

C) Parallel codebases — Fork the project and build labour export as a separate system reusing only shared infrastructure code

D) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 2
For multi-tenancy (multiple agencies), what isolation level is required?

A) Shared database, shared schema — All agencies in same tables, filtered by TenantId column

B) Shared database, separate schemas — Each agency gets its own PostgreSQL schema within the same database

C) Separate databases — Each agency gets its own database instance

D) Other (please describe after [Answer]: tag below)

[Answer]: B

---

## Question 3
How should the configurable workflow engine store its configuration?

A) Database-driven — Workflow stages, statuses, and transition rules stored in database tables (admin UI to configure)

B) JSON/YAML configuration files — Workflow definitions stored as config files per agency

C) Hybrid — Default workflow in code, per-agency overrides in database

D) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 4
For the "dynamic action buttons" (e.g., "To Embassy", "To LMIS", "To Ticket"), how should visibility rules be enforced?

A) Server-side only — API returns which actions are available for each record; frontend renders buttons based on API response

B) Client-side rules — Frontend evaluates conditions locally for faster UX, server validates on action execution

C) Both — Server provides available actions, client also evaluates for optimistic UI, server re-validates on submit

D) Other (please describe after [Answer]: tag below)

[Answer]: C

---

## Question 5
What is the scope of the Telegram/WhatsApp bot for the initial release?

A) MVP — Read-only status lookups and push notifications only

B) Standard — Status lookups + quick actions (update medical status, confirm arrival) + notifications

C) Full — All of Standard + CV generation + document uploads + multi-language (Amharic/English)

D) Defer entirely — Build bot integration later, focus on web platform first

E) Other (please describe after [Answer]: tag below)

[Answer]: C

---

## Question 6
What is the "mirror view" behavior when Medical=Fit AND Tasheer=BookDone?

A) The candidate record physically exists in one table but appears in BOTH Embassy View AND LMIS View queries simultaneously (single source of truth, multiple filtered views)

B) A copy/snapshot of the record is created in the LMIS tracking area while the original stays in Embassy View

C) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 7
For the Commission & Finance module, what financial tracking granularity is needed?

A) Simple — Track total fee per candidate, mark paid/unpaid, basic balance reporting

B) Standard — Fee breakdown by category (agency fee, government fee, medical fee, ticket cost), partial payments, per-office reporting

C) Full ERP — Double-entry accounting, multi-currency support, bank reconciliation, tax calculations, full financial statements

D) Other (please describe after [Answer]: tag below)

[Answer]: C

---

## Question 8
What reporting/export capabilities are needed for initial release?

A) Basic — Pipeline view (candidates per stage), simple counts and status dashboards

B) Standard — Pipeline + agency performance + overdue alerts + Excel export

C) Full — All of Standard + PDF generation + office comparisons + financial summaries + custom date ranges + scheduled reports

D) Other (please describe after [Answer]: tag below)

[Answer]: C

---

## Question 9
How should the system handle the "Returned" and "Runaway" exception cases after arrival?

A) Simple flag — Mark status and move to an exception list; manual follow-up

B) Structured workflow — Dedicated exception handling with required fields (reason, date, liability assignment, resolution tracking)

C) Full containment — Separate workspace with its own stages (Investigation → Resolution → Closed), financial impact tracking, agency notification

D) Other (please describe after [Answer]: tag below)

[Answer]: C

---

## Question 10
What is the target deployment environment for the initial release?

A) Local/self-hosted — Single server deployment (Docker Compose or bare metal)

B) Azure cloud — Azure App Service + Azure Database for PostgreSQL

C) AWS cloud — ECS/Fargate + RDS PostgreSQL

D) Hybrid — Cloud backend + local database (for data sovereignty)

E) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 11
What user roles are needed beyond those listed in the spec (Admin, Embassy Officer, Case Executive, Finance, Field Agent)?

A) Those five are sufficient for initial release

B) Add: Agency Owner (super-admin per tenant), Data Entry Clerk, Auditor (read-only)

C) Add: Office Manager (branch-level admin), Notification Manager, API Integration User

D) Other (please describe after [Answer]: tag below)

[Answer]: B,C

---

## Question 12
For candidate document management (passport, photos, CVs), what storage approach?

A) Database (BYTEA/BLOB) — Store files directly in PostgreSQL

B) Object storage — Azure Blob / AWS S3 with signed URLs, metadata in database

C) File system — Store on server disk with path references in database

D) Other (please describe after [Answer]: tag below)

[Answer]: C

---

## Question 13
Should the system support real-time updates (e.g., when a candidate moves to a new stage, other users see it immediately)?

A) No — Standard request/response; users refresh to see updates

B) Polling — Frontend polls API every 30-60 seconds for updates

C) Real-time — WebSocket/SignalR for live updates on stage transitions and notifications

D) Other (please describe after [Answer]: tag below)

[Answer]: C

---

## Question 14
What is the priority order for implementing the 10 feature modules? (Select the top 3 for MVP)

A) Candidate Management + Configurable Workflow Engine + Embassy & Visa Processing (core pipeline)

B) Candidate Management + Workflow Engine + LMIS + Travel/Logistics (end-to-end flow)

C) All modules simultaneously (full system, longer timeline)

D) Other (please describe after [Answer]: tag below)

[Answer]: C

---

## Question: Security Extensions
Should security extension rules be enforced for this project?

A) Yes — enforce all SECURITY rules as blocking constraints (recommended for production-grade applications)

B) No — skip all SECURITY rules (suitable for PoCs, prototypes, and experimental projects)

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question: Resiliency Extensions
Should the resiliency baseline be applied to this project?

A) Yes — apply the resiliency baseline as directional best practices and design-time guidance (recommended for business-critical workloads)

B) No — skip the resiliency baseline (suitable for PoCs, prototypes, and experimental projects)

X) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question: Property-Based Testing Extension
Should property-based testing (PBT) rules be enforced for this project?

A) Yes — enforce all PBT rules as blocking constraints (recommended for projects with business logic, data transformations, serialization, or stateful components)

B) Partial — enforce PBT rules only for pure functions and serialization round-trips

C) No — skip all PBT rules (suitable for simple CRUD applications or thin integration layers)

X) Other (please describe after [Answer]: tag below)

[Answer]: A
