# Unit of Work Plan

## Decomposition Strategy

Since SimbaFlow is a **monolithic** deployment (single .NET API + single Next.js frontend + single PostgreSQL in Docker Compose), units of work represent **logical modules within the monolith**, not independently deployable services. Each unit is a vertical slice with its own domain entities, handlers, API module, and frontend pages.

Units are sequenced by dependency — each unit builds on the foundation laid by previous units.

## Execution Plan

- [x] Step 1: Define Unit 1 — Core Infrastructure (foundation for all other units)
- [x] Step 2: Define Unit 2 — Candidate & Workflow Engine (core domain)
- [x] Step 3: Define Unit 3 — Embassy & LMIS Processing
- [x] Step 4: Define Unit 4 — Travel, Departure & Arrival
- [x] Step 5: Define Unit 5 — Finance & Commission (ERP)
- [x] Step 6: Define Unit 6 — Agency ERP (Staff, Office, Partners, Admin)
  - **Updated 2026-07-22**: Platform Partner catalog + tenant links; MoLS agency levels / license on TenantInfo — see `inception/requirements/partner-agency-and-tenant-licensing.md`
- [x] Step 7: Define Unit 7 — Bot & Notifications (Telegram/WhatsApp + SignalR)
- [x] Step 8: Define Unit 8 — Reporting & Analytics
- [x] Step 9: Generate unit-of-work.md
- [x] Step 10: Generate unit-of-work-dependency.md
- [x] Step 11: Generate unit-of-work-story-map.md
- [x] Step 12: Validate completeness — all stories assigned, all dependencies clear

---

## Clarifying Questions

## Question 1
For the construction phase, how should units be executed?

A) Strictly sequential — Complete one unit entirely (design → code → test) before starting the next

B) Parallel design, sequential code — Design all units first, then code them sequentially

C) Overlap allowed — Start coding a unit as soon as its design is approved, while designing the next unit

D) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 2
For the frontend, should each unit include its corresponding frontend pages, or should frontend be a separate unit?

A) Integrated — Each backend unit includes its corresponding frontend pages (full vertical slice)

B) Separate — Backend units first (all 8), then a single "Frontend" unit that builds all pages

C) Hybrid — Core backend units include basic frontend pages; complex UI (dashboards, workflow config editor, financial statements) as a separate frontend unit

D) Other (please describe after [Answer]: tag below)

[Answer]: A

---

## Question 3
The existing codebase has clinical code to delete. When should deletion happen?

A) Before Unit 1 — Clean slate first (delete all clinical code), then build new units on clean codebase

B) During Unit 1 — Unit 1 (Core Infrastructure) handles deletion as its first step, then sets up new foundation

C) Gradual — Each unit deletes only the clinical code it replaces (e.g., Unit 2 deletes Patient entity when creating Candidate entity)

D) Other (please describe after [Answer]: tag below)

[Answer]: A

---
