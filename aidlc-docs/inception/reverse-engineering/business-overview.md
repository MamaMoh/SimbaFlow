# Business Overview

## Business Context

SimbaFlow is currently a **Hospital Information System (HIS)** / **Electronic Health Record (EHR)** platform modeled after enterprise clinical systems (Epic-style). It manages the full patient lifecycle from registration through clinical encounters, orders, lab/imaging results, pharmacy dispensation, billing, and inpatient management.

**Pivot Context**: The system is being transformed into a **Labour Export Agency Management System** that manages the end-to-end lifecycle of overseas worker deployment — from candidate registration through embassy processing, government labour clearances, travel logistics, and financial settlement.

## Current Business Transactions

### Clinical Operations
1. **Patient Registration** — Register patient demographics, generate MRN, assign identifiers
2. **Patient Admission (ADT)** — Admit patient to encounter, assign bed/department/physician
3. **Clinical Documentation** — Record vitals, draft SOAP notes, sign/cosign notes, add addenda
4. **Diagnosis Management** — Search ICD codes, assign diagnoses to encounters
5. **Order Entry (CPOE)** — Place diagnostic (lab/imaging) and medication orders, sign orders
6. **Lab Fulfillment** — Collect specimen, enter results, verify results, route to ordering provider
7. **Imaging Fulfillment** — Start study, enter report with impression, route critical findings
8. **Pharmacy Workflow** — Verify prescription, select stock batch, dispense medication
9. **Patient Discharge** — Complete encounter with disposition, generate charges
10. **Inpatient Transfer** — Transfer patient between beds/wards

### Administrative Operations
11. **Staff Lifecycle** — Draft profile → Provision account → Assign privileges → Suspend/Terminate
12. **Appointment Scheduling** — Search availability → Book → Check-in → No-show/Cancel
13. **Billing & Payments** — Auto-charge capture → Invoice generation → Payment collection
14. **Role & Permission Management** — Create roles → Assign permissions → Enforce via pipeline

### Support Operations
15. **Clinical Alerts** — Route critical results → Acknowledge alerts
16. **Audit Trail** — Write audit (mutations) + Read audit (PHI access logging)
17. **Session Management** — Login → Token refresh → Impersonation → Logout

## Business Dictionary

| Term | Meaning |
|------|---------|
| MRN | Medical Record Number — unique patient identifier |
| ADT | Admit-Discharge-Transfer — patient movement tracking |
| CPOE | Computerized Provider Order Entry — electronic ordering |
| LIS | Laboratory Information System — lab workflow module |
| RIS | Radiology Information System — imaging workflow module |
| SOAP | Subjective-Objective-Assessment-Plan — clinical note format |
| Encounter | A clinical interaction between patient and provider |
| Chargemaster | Master list of billable services and their prices |
| StaffProfile | Clinical/resource identity (separated from IT/auth identity) |

## Component Level Business Descriptions

### SimbaFlow.API
- **Purpose**: HTTP API gateway exposing all business operations as RESTful endpoints
- **Responsibilities**: Request routing, request/response serialization, endpoint documentation

### SimbaFlow.Application
- **Purpose**: Orchestration layer implementing cross-cutting concerns
- **Responsibilities**: Validation, authorization, audit logging, performance monitoring, concurrency control

### SimbaFlow.Domain
- **Purpose**: Core business domain model
- **Responsibilities**: Entity definitions, enums, value objects, domain events, business rules

### SimbaFlow.Infrastructure
- **Purpose**: External concerns implementation
- **Responsibilities**: Database access, identity/auth services, background jobs, event dispatching, billing calculations

### SimbaFlow.Shared
- **Purpose**: Shared data transfer models
- **Responsibilities**: DTOs and models shared between layers or with external consumers
