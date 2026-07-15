# Interaction Diagrams

## Key Business Transaction Flows

### 1. User Authentication Flow

```mermaid
sequenceDiagram
    participant U as User (Browser)
    participant FE as Next.js Frontend
    participant NA as next-auth
    participant API as .NET API
    participant JWT as JwtTokenService
    participant DB as PostgreSQL

    U->>FE: Enter credentials
    FE->>NA: signIn("credentials", {...})
    NA->>API: POST /api/auth/login
    API->>DB: Validate user + password
    DB-->>API: User found
    API->>JWT: Generate access + refresh tokens
    JWT-->>API: Tokens
    API-->>NA: { accessToken, refreshToken, user }
    NA-->>FE: Session established
    FE-->>U: Redirect to /overview

    Note over FE,API: Token refresh happens automatically
    FE->>NA: Token expired
    NA->>API: POST /api/auth/refresh
    API->>DB: Validate + rotate refresh token
    API-->>NA: New token pair
```

### 2. Patient Registration → Admission Flow (Analogous to Candidate Registration)

```mermaid
sequenceDiagram
    participant User as Staff User
    participant API as Carter Module
    participant MR as MediatR Pipeline
    participant Val as Validation
    participant Auth as Authorization
    participant H as Handler
    participant DB as PostgreSQL
    participant Evt as DomainEvents

    User->>API: POST /api/patients (RegisterPatientCommand)
    API->>MR: Send(command)
    MR->>Val: Validate fields
    Val->>Auth: Check "patient.write" permission
    Auth->>H: Execute handler
    H->>DB: Insert Patient + generate MRN
    H->>Evt: Raise PatientRegistered event
    DB-->>H: Patient created
    H-->>API: Result<Guid> (patient ID)
    API-->>User: 201 Created

    User->>API: POST /api/patients/{id}/admit
    API->>MR: Send(AdmitPatientCommand)
    MR->>Val: Validate admission fields
    Val->>Auth: Check permission
    Auth->>H: Execute handler
    H->>DB: Create PatientEncounter + AdtEvent
    H->>Evt: Raise PatientAdmitted event
    DB-->>H: Encounter created
    H-->>API: Result<Guid> (encounter ID)
    API-->>User: 201 Created
```

### 3. Order → Lab Fulfillment Flow (Analogous to Workflow Stage Transitions)

```mermaid
sequenceDiagram
    participant Doc as Ordering Provider
    participant Lab as Lab Technician
    participant API as API
    participant DB as PostgreSQL
    participant Alert as ClinicalAlerts

    Doc->>API: POST /api/orders/diagnostic (Lab order)
    API->>DB: Create DiagnosticOrder (status: Draft)
    Doc->>API: POST /api/orders/encounter/{id}/sign
    API->>DB: Update orders → Signed (triggers charge capture)

    Lab->>API: GET /api/lab/queue
    API->>DB: Fetch pending lab orders
    API-->>Lab: Lab worklist

    Lab->>API: POST /api/lab/orders/{id}/collect
    API->>DB: Update status → InProgress

    Lab->>API: POST /api/lab/orders/{id}/results
    API->>DB: Store LabResult entries

    Lab->>API: POST /api/lab/orders/{id}/verify
    API->>DB: Update status → Completed
    API->>Alert: Create clinical alert (if critical)
    API->>DB: Route to ordering provider's results inbox

    Doc->>API: GET /api/orders/results-inbox
    Doc->>API: POST /api/orders/{id}/acknowledge
```

### 4. Billing Auto-Capture Flow (Analogous to Commission Tracking)

```mermaid
sequenceDiagram
    participant H as Order Handler
    participant Billing as EncounterChargeService
    participant DB as PostgreSQL

    H->>Billing: CaptureCharge(encounterId, service, price)
    Billing->>DB: Lookup ChargemasterItem by code
    Billing->>DB: Create EncounterCharge (status: Pending)
    
    Note over DB: Charges accumulate per encounter

    participant Finance as Finance User
    Finance->>DB: GET /api/billing/invoices/encounter/{id}
    DB-->>Finance: Aggregated charges

    Finance->>DB: POST /api/billing/payments (collect payment)
    DB-->>Finance: Payment recorded, charges marked paid
```

### 5. Staff Lifecycle Flow (Directly Reusable)

```mermaid
sequenceDiagram
    participant HR as HR Manager
    participant IT as IT Admin
    participant Med as Medical Admin
    participant API as API
    participant DB as PostgreSQL

    HR->>API: POST /api/staff (DraftStaffProfile)
    API->>DB: Create StaffProfile (no user account yet)

    IT->>API: POST /api/staff/{id}/provision-account
    API->>DB: Create ApplicationUser linked to StaffProfile
    API->>DB: Assign roles + permissions

    Med->>API: POST /api/staff/{id}/privileges
    API->>DB: Create StaffDepartmentAffiliation

    Note over DB: Staff can now access clinical features
    Note over DB: Department affiliation enforced per request
```

### 6. Request Pipeline (Every Request)

```mermaid
flowchart LR
    REQ["HTTP Request"]
    JWT["JWT Validation"]
    STAFF["StaffContext<br/>Middleware"]
    CARTER["Carter<br/>Module"]
    MEDIATR["MediatR<br/>Send()"]
    VAL["Validation<br/>Behavior"]
    AUTHZ["Authorization<br/>Behavior"]
    CLIN["Clinical Auth<br/>Behavior"]
    PERF["Performance<br/>Behavior"]
    AUD["Audit<br/>Behavior"]
    HAND["Handler"]
    DB[("PostgreSQL")]

    REQ --> JWT --> STAFF --> CARTER --> MEDIATR
    MEDIATR --> VAL --> AUTHZ --> CLIN --> PERF --> AUD --> HAND --> DB

    style VAL fill:#FFF3E0
    style AUTHZ fill:#FFECB3
    style CLIN fill:#FFF9C4
    style PERF fill:#F1F8E9
    style AUD fill:#E8F5E9
```

## Patterns Applicable to Labour Export Workflow

The **Order → Lab Fulfillment** pattern directly maps to the SimbaFlow labour export workflow:

| Hospital Pattern | Labour Export Equivalent |
|-----------------|------------------------|
| Place Order | Register Candidate |
| Sign Order | Move to New Contract View |
| Collect Specimen | Book Medical / Tasheer |
| Enter Results | Update Medical Fit / Tasheer Done |
| Verify Results | Visa Issued |
| Route to Provider | Transfer to next view (LMIS, Ticket, etc.) |
| Acknowledge | Confirm Arrival |
| Auto-charge | Add to Commission |

The **pipeline behavior pattern** maps to:
- `AuthorizationBehavior` → Office/branch access control
- `ClinicalAuthorizationBehavior` → **WorkflowAuthorizationBehavior** (can user act on this stage?)
- `AuditBehavior` → Unchanged (audit all transitions)
- `ValidationBehavior` → Validate workflow transition rules
