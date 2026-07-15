# ET Medical System UI — Medical Process Flow

This document describes the **medical and operational processes** from the **user and UI perspective**: main screens, user actions, and how they map to API calls and data flow. It complements the API’s `medical-process-flow.md` by focusing on what the user sees and does in the application.

---

## 1. Authentication

| User action | UI | API / Data |
|-------------|----|------------|
| Log in | **`(auth)/login`** — Login form (credentials) | POST to auth endpoint; receive JWT; store (e.g. cookie/session). |
| Post-login | Redirect to main app (e.g. overview or default) | Subsequent requests send JWT (e.g. in header). |

---

## 2. Patient Registration and Demographics

| User action | UI | API / Data |
|-------------|----|------------|
| List patients | **`/patients`** — Patient list (table shell, search/filters) | GET patients (paginated). |
| Create patient | **`/patients/new`** — Patient form | POST `/api/patients`. |
| View patient summary | **`/patients/[id]`** — Patient summary page (vitals, allergies, conditions, consultations, medications) | Server fetches patient, vitals, allergies, conditions, consultations, prescriptions via server actions / services. |
| Edit patient | **`/patients/[id]/edit`** — Edit patient form | GET patient; PUT `/api/patients/{id}`. |
| Patient info tab | **`/patients/[id]/info`** | Patient demographics and info. |
| Patient insurance | **`/patients/[id]/insurance`** | Patient’s insurance assignments and policy data. |

**Flow**: User opens **Patients** → selects or creates patient → lands on **Patient summary** or **Info**; can edit demographics and manage insurance from patient-scoped pages.

---

## 3. Scheduling and Appointments

| User action | UI | API / Data |
|-------------|----|------------|
| View all appointments | **`/appointments/all`** — Appointments list/calendar | GET appointments (by date, doctor, etc.). |
| Create appointment | **`/appointments/new`** — Appointment form (e.g. calendar form: physician, date, time, patient, type) | POST `/api/appointments` (CreateAppointment). |
| Doctor’s patient list | **`/doctor/patients`** | GET patients (e.g. for current doctor). |

**Flow**: User opens **Appointments** → **All** to see schedule or **New** to book; selects physician, date, slot, patient, and type (e.g. follow-up, telemedicine) → submits → appointment created.

---

## 4. Visit and Check-In (Reception / Consultation Start)

| User action | UI | API / Data |
|-------------|----|------------|
| View active visits (queue) | Reception/waiting list (e.g. from clinical-visits or appointments context) | GET `/api/clinical-visits/visits/active`. |
| Check in appointment | Action to “start visit” or “check in” for an appointment | POST `/api/clinical-visits/visits/ensure-for-appointment` (EnsureVisitForAppointment). |
| Create walk-in visit | Form or action to create visit without appointment | POST `/api/clinical-visits/visits`. |
| Update visit status | e.g. CheckedIn → InConsultation → Completed | PATCH `/api/clinical-visits/visits/{visitId}/status`. |
| Patient visits list | **`/patients/[id]/visits`** — List of visits for patient | GET `/api/clinical-visits/visits?patientId=...`. |

**Flow**: Reception opens **active visits** or appointment list → selects appointment → **Ensure visit for appointment** (or create walk-in) → visit appears in queue; status updated as patient moves (e.g. to consultation, then completed).

---

## 5. Clinical Documentation (Consultation, Vitals, Forms)

| User action | UI | API / Data |
|-------------|----|------------|
| Start new consultation | **`/patients/[id]/consultation/new`** — New consultation | Ensure visit; create consultation (API). |
| Open existing consultation | **`/patients/[id]/consultation/[consultationId]`** — Consultation workspace | GET consultation and related data; consultation form. |
| Record vitals | **`/patients/[id]/vitals`** or patient summary vitals | POST/GET vitals API. |
| Fill dynamic form | **`/forms`** — Form list; **`/forms/fill/[formId]`** — Fill form | GET form definition; GET choice groups/tables; POST submit. |
| Record vaccination | Vaccination form (e.g. from patient/visit context) | POST `/api/vaccinations`. |
| Record procedure | Procedure form | Procedure API. |
| Issue sick leave | Sick leave action/form | Sick leave API. |
| Create accident report | Accident form (e.g. **components/forms/accident-form**) | POST `/api/accident-report-forms/...` (injured party, doctor, supervisor); PDF generation. |

**Flow**: From **Patient summary** or **Visits**, user opens **Consultation** (new or existing) → documents via consultation form, vitals, procedures, vaccinations; can open **Forms** → **Fill** for occupational forms, or complete **Accident report** and generate PDF/insurance notice.

---

## 6. Orders: Lab and Imaging

| User action | UI | API / Data |
|-------------|----|------------|
| View lab orders | **`/lab/lab-orders`** — Lab orders table | GET lab orders. |
| Sample collection | **`/lab/lab-orders/sample-collection`** | Update specimen/order status (API). |
| Technician assigning | **`/lab/lab-orders/technician-assigning`** | Assign technician (API). |
| Enter results | **`/lab/lab-results`** — Results worklist; result entry dialog (e.g. **lab-result-entry-dialog**) | GET results; POST/PUT result entry and amendments. |
| View patient orders | **`/patients/[id]/orders`** — Orders for patient | GET orders by patient/visit. |
| Master data: lab tests | **`/master-data/lab-tests`**, **`/master-data/lab-tests/[id]/edit`** | Lab test CRUD. |
| Lab machines / test mappings | **`/lab/lab-machines`**, **`/lab/lab-machines/[id]/test-mappings`** | Lab machine and mapping APIs. |
| Imaging procedures | **`/master-data/imaging-procedures`** (new, edit) | Imaging procedure master data. |

**Flow**: Provider orders lab (from consultation/orders context) → **Lab orders** list and **Sample collection** / **Technician assigning** used by lab staff → **Lab results** used to enter/amend results; patient’s **Orders** page shows orders for that patient.

---

## 7. Pharmacy: Prescriptions and Dispensations

| User action | UI | API / Data |
|-------------|----|------------|
| View prescriptions | **`/pharmacy/prescriptions`** — Prescriptions table | GET prescriptions. |
| Create prescription | From consultation or prescription form (e.g. **prescription-form**) | POST prescription API. |
| View dispensations | **`/pharmacy/dispensations`** — Dispensations table | GET dispensations. |
| Record dispensation | Dispense dialog (e.g. **dispense-external-dialog**, **edit-dispensation-dialog**) | POST `/api/pharmacy/dispensations`; update as needed. |
| Record return | **`/pharmacy/returns`**; **record-return-dialog** | Record return API. |
| View patient medications | **`/patients/[id]/medications`** | GET prescriptions by patient. |
| Pharmacy drugs | **`/pharmacy/drugs`** | Drug list (operational/master). |

**Flow**: Provider creates **Prescription** from consultation → Pharmacy sees **Prescriptions** list → **Record dispensation** (internal/external); later **Pharmacy returns** for returns. Patient’s **Medications** shows prescription history.

---

## 8. Billing and Finance

| User action | UI | API / Data |
|-------------|----|------------|
| Billing overview | **`/billing`** — Billing dashboard/list | GET invoices or summary. |
| View invoice | **`/billing/invoice/[id]`** — Invoice detail | GET billing invoice by id. |
| Finance tracking | **`/billing/finance-tracking`** — Charges and tracking | Charge tracking and invoice APIs. |
| Patient finance summary | From patient context or billing by patient | GET `/api/billing-invoices/patient/{patientId}/finance-summary`. |

**Flow**: Billing staff use **Billing** and **Finance tracking** to view invoices and charges; charges are often created automatically when clinical actions (e.g. procedures, dispensations) are recorded via the API (ChargeTrackingService). User can open **Invoice** by id for details.

---

## 9. Insurance and Master Data

| User action | UI | API / Data |
|-------------|----|------------|
| Insurance policies | **`/master-data/insurance-policies`**, **`/master-data/insurance-policies/[id]`** — Policy form | GET/POST/PUT insurance policies. |
| Service limits | **`/master-data/insurance-policies/service-limits`** (list, new, edit) | Insurance policy service limits API. |
| Cost sharing | e.g. **insurance-policy-cost-sharing** component on policy page | Cost-sharing APIs under policy. |
| Other master data | **Cost centers**, **Employee categories**, **Industrial affiliates**, **Accident types**, **Injuries**, **Chargemaster items**, **Chargemaster item prices**, **Forms** (CRUD), etc. | Corresponding master-data APIs. |

**Flow**: Admin configures **Insurance policies**, **Service limits**, and **Cost sharing**; these drive how billing applies insurance and cost-sharing when recording charges (see API medical-process-flow).

---

## 10. Administration: Users and Staff

| User action | UI | API / Data |
|-------------|----|------------|
| User list | **`/(admin)/users`** | GET users. |
| Change password | **`/users/[id]/change-password`** | Change password API. |
| Staff edit | **`/staff/edit/[id]`** — Edit staff form | GET/PUT staff and user APIs. |

**Flow**: Admin manages **Users** and **Staff** (roles, permissions, profile); can **Change password** for a user.

---

## End-to-End Summary (UI Side)

1. **Login** → User authenticates and enters the main app.
2. **Patients** → List, create, open **Patient summary** (info, vitals, allergies, conditions, consultations, medications); **Edit** and **Insurance**.
3. **Appointments** → **All** appointments; **New** appointment (physician, slot, patient, type).
4. **Visit/check-in** → **Ensure visit for appointment** or create walk-in; update **visit status** through workflow; **Patient visits** list.
5. **Consultation** → **New** or open existing **Consultation**; document with **Vitals**, **Procedures**, **Vaccinations**, **Forms** (fill), **Accident report**, **Sick leave**.
6. **Lab** → **Lab orders** (and sample collection, technician assigning); **Lab results** (enter/amend); **Patient orders**; master data **Lab tests** and **Imaging procedures**.
7. **Pharmacy** → **Prescriptions** (create from consultation); **Dispensations** (record dispense/return); **Patient medications**.
8. **Billing** → **Billing** overview, **Invoice** detail, **Finance tracking**; charges often created by API when clinical actions are performed.
9. **Master data & admin** → **Insurance policies**, **Service limits**, **Cost sharing**; **Users** and **Staff** management.

For the **API** side of these flows (endpoints, entities, charge tracking, encounter creation), see **ETmedicalAPI/doc/medical-process-flow.md**.
