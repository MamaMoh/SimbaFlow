# ET Medical System UI — Architecture

## Overview

The UI is a **Next.js** application (App Router) for the MRCIS Plus medical/clinical system. It provides the front-end for patients, appointments, visits, consultations, lab, pharmacy, billing, and administration. It consumes the MRCIS Plus API (REST) and uses **React** components with **TypeScript**.

## Project Structure

```
ETMedicalUI/
├── ET Medical system/     # Next.js app (app/, components/, lib/, etc.)
│   ├── app/                # App Router: routes, layouts, pages
│   ├── components/         # React components (forms, shells, UI, patient, lab, pharmacy, etc.)
│   ├── lib/                # Services, server actions, schemas, utils
│   └── ...
└── doc/                    # Documentation (this folder)
```

- **app/**: Route segments; `(main)` and `(auth)` are route groups. Pages are `page.tsx`; layouts are `layout.tsx`.
- **components/**: Reusable and feature-specific components (forms, table shells, dialogs, patient summary, etc.).
- **lib/**: API client logic, server actions, validation schemas (e.g. Zod), and utilities (e.g. patient ID encoding).

## High-Level Architecture

| Layer | Responsibility |
|-------|----------------|
| **Routes** | `app/(main)/...` and `app/(auth)/...` — URL structure and page-level data loading. |
| **Layouts** | Main layout (sidebar, auth), patient-scoped layout; shared shell and navigation. |
| **Pages** | Server or client components that fetch data (server actions, services) and render sections. |
| **Components** | Forms, tables, dialogs, and feature-specific widgets (consultation, lab, pharmacy, billing). |
| **Data** | Server actions under `lib/server/actions/`, API services under `lib/services/`, and optional client state (e.g. Zustand). |

## Core Technologies

- **Next.js** — App Router, Server Components, Server Actions, API routes (if any).
- **React** — UI components; hooks for client interactivity.
- **TypeScript** — Typing for props, API responses, and schemas.
- **API communication** — Fetch to MRCIS Plus API (base URL from env); typically via server actions or `lib/services/*` that call the backend.
- **State** — Server state via async components and server actions; client state (e.g. Zustand) where needed (e.g. patient context in layout).
- **Auth** — Login page and session/token handling for API calls (e.g. JWT in headers).

## Route Structure (App Router)

- **`(auth)/login`** — Login page.
- **`(main)/`** — Authenticated area:
  - **overview** — Dashboard.
  - **patients** — Patient list; **patients/[id]** — Patient summary (info, visits, consultation, orders, medications, insurance, etc.); **patients/[id]/edit**, **patients/[id]/vitals**, **patients/[id]/consultation/new**, **patients/[id]/consultation/[consultationId]**, **patients/[id]/visits**, **patients/[id]/orders**, **patients/[id]/medications**, **patients/[id]/insurance**, **patients/[id]/conditions**, **patients/[id]/attachments**, **patients/[id]/programs**.
  - **appointments** — **appointments/all**, **appointments/new**.
  - **doctor/patients** — Doctor’s patient list.
  - **billing** — Billing overview; **billing/invoice/[id]**; **billing/finance-tracking**.
  - **forms** — Form list; **forms/fill/[formId]** — Fill form.
  - **lab** — **lab/lab-orders**, **lab/lab-orders/sample-collection**, **lab/lab-orders/technician-assigning**; **lab/lab-results**; **lab/lab-machines** (and test mappings).
  - **pharmacy** — **pharmacy/prescriptions**, **pharmacy/dispensations**, **pharmacy/drugs**, **pharmacy/returns**.
  - **master-data/** — Insurance policies, service limits, forms (CRUD), lab tests, imaging procedures, cost centers, employee categories, industrial affiliates, accident types, injuries, chargemaster items, chargemaster item prices, etc.
  - **`(admin)/users`**, **users/[id]/change-password**; **staff/edit/[id]** — User and staff management.

Patient IDs in URLs are often **encoded** (e.g. secure token) and decoded via `lib/utils/patient-id-encoder` before calling the API.

## Data Flow

- **Server Components**: Pages and layouts are async; they call server actions or services that `fetch()` the API. Data is passed as props to client components when needed.
- **Server Actions**: Under `lib/server/actions/` (e.g. `patientActions`, `billingInvoiceActions`, `labOrderActions`, `vaccinationActions`, `accidentActions`). Used for mutations and server-side reads; return data or redirect.
- **Services**: Under `lib/services/` (e.g. `consultationService`, `prescriptionService`, `formService`, `labOrderService`). Encapsulate API endpoints and are used from server code or server actions.
- **Client state**: Used for UI state (e.g. selected patient in sidebar, modals). Patient context may be stored (e.g. Zustand) so the layout and child pages can access current patient without re-fetching.

## Key UI Patterns

- **Table shells** — Reusable data tables with filters/pagination (e.g. `patient-table-shell`, `lab-orders-table-shell`, `prescriptions-table-shell`, `accidents-table-shell`).
- **Forms** — Feature forms (patient, appointment, consultation, vaccination, procedure, prescription, accident, insurance, etc.) with validation (e.g. Zod schemas in `lib/schemas/`).
- **Dialogs** — Modal forms (e.g. form preview, pharmacy dispense/return, lab result entry).
- **Dynamic forms** — Form definitions from API; rendered by a dynamic form renderer for fill/submit flows.
- **Patient-centric layout** — When navigating under a patient, sidebar/layout can show patient context and quick links (info, visits, consultation, orders, etc.).

## Styling and UI Library

- Styling approach (Tailwind, CSS modules, or component library) is defined in the project; shared primitives (buttons, tables, inputs) live under `components/ui/`.

## Security and Configuration

- **API base URL** and auth (e.g. token) are configured via environment or auth layer; server actions and services send credentials as required (e.g. cookies or Authorization header).
- **Patient ID encoding** — Sensitive IDs may be encoded in URLs and decoded only on the server to avoid exposing raw GUIDs.

---

This document describes the UI only. For end-to-end medical process flows (screens and user actions), see `medical-process-flow.md`. For API architecture and flows, see the API doc folder: `ETmedicalAPI/doc/`.
