# API Documentation

## REST API Overview

All endpoints follow a consistent pattern:
- Base URL: `/api/{module}`
- Authentication: JWT Bearer token (except `/api/auth/login`, `/api/auth/refresh`)
- Response format: `{ "isSuccess": true, "data": {...}, "statusCode": 200 }` or `{ "isSuccess": false, "error": "...", "statusCode": 400 }`
- Pagination: `?page=1&pageSize=20`
- Search: `?search=term`

## Authentication Module (`/api/auth`)

| Method | Path | Purpose | Auth Required |
|--------|------|---------|---------------|
| POST | `/api/auth/login` | Authenticate user, return JWT + refresh token | No |
| POST | `/api/auth/refresh` | Exchange refresh token for new JWT | No |
| POST | `/api/auth/logout` | Invalidate refresh token | Yes |
| POST | `/api/auth/change-password` | Change user password | Yes |
| POST | `/api/auth/login/mfa` | Verify MFA code (second factor) | No |
| POST | `/api/auth/mfa/setup` | Generate MFA secret + QR code | Yes |
| GET | `/api/auth/me` | Get current user profile + permissions | Yes |

## Patient Module (`/api/patients`)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/patients` | List patients (paginated, searchable, filterable) |
| GET | `/api/patients/{id}` | Get patient detail |
| POST | `/api/patients` | Register new patient |
| PUT | `/api/patients/{id}` | Update patient |
| DELETE | `/api/patients/{id}` | Soft delete patient |
| POST | `/api/patients/{patientId}/admit` | Admit patient (create encounter) |
| POST | `/api/patients/encounters/{encounterId}/discharge` | Discharge patient |

## Clinical Workspace (`/api/clinical`)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/clinical/encounters/active` | Active encounters dashboard |
| GET | `/api/clinical/provider-dashboard` | Provider landing page |
| GET | `/api/clinical/patients/{patientId}/chart` | Patient chart review |
| POST | `/api/clinical/encounters/walkin` | Start walk-in encounter |
| GET | `/api/clinical/encounters/{id}` | Full encounter workspace |
| POST | `/api/clinical/encounters/{id}/send-to-provider` | Route to provider |
| POST | `/api/clinical/encounters/{id}/claim` | Claim patient |
| PUT | `/api/clinical/encounters/{id}/status` | Update encounter status |
| POST | `/api/clinical/encounters/{id}/complete` | Complete encounter |
| POST | `/api/clinical/encounters/{id}/vitals` | Record vital signs |
| POST | `/api/clinical/encounters/{id}/notes` | Draft clinical note |
| POST | `/api/clinical/notes/{id}/sign` | Sign note |
| POST | `/api/clinical/notes/{id}/cosign` | Co-sign note |
| POST | `/api/clinical/notes/{id}/addendum` | Append addendum |
| GET | `/api/clinical/diagnosis-codes` | Search ICD codes |
| POST | `/api/clinical/encounters/{id}/diagnoses` | Add diagnosis |
| DELETE | `/api/clinical/diagnoses/{id}` | Remove diagnosis |
| GET | `/api/clinical/alerts` | Clinical alerts inbox |
| POST | `/api/clinical/alerts/{id}/acknowledge` | Acknowledge alert |

## Scheduling (`/api/appointments`)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/appointments/availability` | Search available slots |
| GET | `/api/appointments` | List appointments for a day |
| POST | `/api/appointments` | Book appointment |
| PUT | `/api/appointments/{id}/status` | Update status (state machine) |
| PUT | `/api/appointments/{id}/transfer` | Transfer to different provider |
| GET | `/api/appointments/my-schedule` | Current provider's schedule |
| GET | `/api/appointments/range` | Calendar range query |
| GET | `/api/appointment-types` | Appointment type reference data |

## Orders — CPOE (`/api/orders`)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/orders/encounter/{encounterId}` | Orders for encounter |
| POST | `/api/orders/diagnostic` | Place lab/imaging order |
| POST | `/api/orders/medication` | Place medication order |
| POST | `/api/orders/encounter/{id}/sign` | Sign all draft orders |
| POST | `/api/orders/{id}/sign` | Sign single order |
| GET | `/api/orders/queue` | Global orders worklist |
| POST | `/api/orders/{id}/cancel` | Cancel order |
| GET | `/api/orders/results-inbox` | Provider results inbox |
| POST | `/api/orders/{id}/acknowledge` | Acknowledge results |

## Laboratory (`/api/lab`)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/lab/queue` | Lab pending orders |
| GET | `/api/lab/catalog` | Lab test catalog |
| GET | `/api/lab/catalog/categories` | Lab test categories |
| POST | `/api/lab/catalog` | Create lab test |
| PUT | `/api/lab/catalog/{id}` | Update lab test |
| DELETE | `/api/lab/catalog/{id}` | Delete lab test |
| GET | `/api/lab/orders/{id}/results` | Get results for order |
| POST | `/api/lab/orders/{id}/collect` | Collect specimen |
| POST | `/api/lab/orders/{id}/results` | Enter results |
| POST | `/api/lab/orders/{id}/verify` | Verify results |

## Imaging (`/api/imaging`)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/imaging/queue` | Radiology worklist |
| POST | `/api/imaging/orders/{id}/start` | Start imaging study |
| POST | `/api/imaging/orders/{id}/report` | Enter report |
| GET | `/api/imaging/orders/{id}/report` | View report |

## Pharmacy (`/api/pharmacy`)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/pharmacy/queue` | Pharmacy queue |
| POST | `/api/pharmacy/orders/{id}/verify` | Verify prescription |
| POST | `/api/pharmacy/orders/{id}/dispense` | Dispense medication |
| GET | `/api/pharmacy/inventory` | Inventory levels |
| POST | `/api/pharmacy/inventory/receive` | Receive stock |
| GET | `/api/pharmacy/orders/{id}/batches` | Available batches |
| GET | `/api/pharmacy/catalog` | Drug formulary |
| POST | `/api/pharmacy/catalog` | Add catalog item |
| PUT | `/api/pharmacy/catalog/{id}` | Update catalog item |

## Billing (`/api/billing`)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/billing/charges` | List charges |
| POST | `/api/billing/charges` | Add manual charge |
| GET | `/api/billing/summary` | Billing summary |
| POST | `/api/billing/charges/{id}/pay` | Mark charge paid |
| GET | `/api/billing/invoices/encounter/{id}` | Encounter invoice |
| POST | `/api/billing/invoices/encounter/{id}/pay` | Pay invoice |
| POST | `/api/billing/payments` | Collect payment |
| GET | `/api/billing/payments` | Payment ledger |
| GET | `/api/billing/chargemaster` | Chargemaster items |
| POST | `/api/billing/chargemaster` | Create chargemaster item |
| PUT | `/api/billing/chargemaster/{id}` | Update chargemaster item |

## Inpatient (`/api/inpatient`)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/inpatient/bed-board` | Bed occupancy board |
| GET | `/api/inpatient/ward-census` | Ward census summary |
| GET | `/api/inpatient/wards/{id}/patients` | Patients in ward |
| POST | `/api/inpatient/transfer` | Transfer patient to bed |

## Staff (`/api/staff`)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/staff` | List staff profiles |
| GET | `/api/staff/stats` | Staff statistics |
| GET | `/api/staff/{id}` | Staff profile detail |
| POST | `/api/staff` | Draft staff profile |
| POST | `/api/staff/{id}/provision-account` | Provision IT account |
| POST | `/api/staff/{id}/privileges` | Assign clinical privileges |
| POST | `/api/staff/{id}/suspend` | Suspend staff |
| POST | `/api/staff/{id}/terminate` | Terminate staff |

## Data Models (Key Response Shapes)

### Result Wrapper
```json
{
  "isSuccess": true,
  "data": { ... },
  "error": null,
  "statusCode": 200
}
```

### Paginated Response
```json
{
  "isSuccess": true,
  "data": {
    "items": [...],
    "totalCount": 100,
    "page": 1,
    "pageSize": 20,
    "totalPages": 5
  }
}
```
