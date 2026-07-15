# Business Logic Model — Unit 1: Core Infrastructure

## BL-01: Tenant Provisioning

### Process Flow
```
ProvisionTenantCommand received
  1. Validate: agency name unique, admin email valid, slug available
  2. Generate schema name: "tenant_{slug}" (lowercase, underscores only)
  3. Create TenantInfo record in public schema
  4. Create PostgreSQL schema: CREATE SCHEMA IF NOT EXISTS "{schema_name}"
  5. Apply all tenant migrations to new schema
  6. Seed default workflow template (8-stage)
  7. Seed default roles and permissions for tenant
  8. Create Agency Owner user account (linked to tenant)
  9. Create default office record
  10. Emit TenantProvisioned domain event
```

### Rollback Strategy
If any step fails after schema creation:
- Drop the created schema (CASCADE)
- Delete TenantInfo record
- Return error to caller

---

## BL-02: Tenant Schema Resolution (Per-Request)

### Process Flow
```
HTTP Request arrives
  1. JWT middleware extracts token
  2. StaffContext middleware resolves user
  3. TenantSchemaResolver:
     a. Read TenantId from JWT claims
     b. If TenantId is null → user is system admin → use public schema only
     c. If TenantId present → lookup SchemaName from tenants table (cached)
     d. Set EF Core connection: SET search_path TO '{schema}', 'public'
  4. All subsequent DbContext queries scoped to tenant schema
```

### Caching Strategy
- Cache tenant→schema mapping in `IMemoryCache` (5 min expiry)
- Invalidate on tenant update/deactivation
- System admin requests bypass cache (always use public)

---

## BL-03: SignalR Connection Management

### Connection Lifecycle
```
User opens web app
  1. Frontend establishes SignalR WebSocket connection
  2. Hub.OnConnectedAsync():
     a. Extract userId, tenantId, officeId from JWT
     b. Add to group: "tenant:{tenantId}"
     c. Add to group: "tenant:{tenantId}:office:{officeId}"
     d. Add to group: "user:{userId}"
  3. Connection maintained with auto-reconnect
  4. Hub.OnDisconnectedAsync():
     a. Remove from all groups (automatic by SignalR)
```

### Broadcasting Rules
- **All status changes** (per user decision) → broadcast to tenant group
- **Personal notifications** → broadcast to user group
- **Office-specific** → broadcast to office group
- **Payload**: `{ candidateId, changeType, field, oldValue, newValue, changedBy, timestamp }`

---

## BL-04: File Storage Management

### Upload Flow
```
UploadDocumentCommand received
  1. Validate: file size ≤ 10MB, file type allowed (PDF, JPG, PNG, DOCX)
  2. Resolve tenant file path: /data/tenants/{tenantSlug}/candidates/{candidateId}/
  3. Generate unique filename: {guid}_{original_filename}
  4. Create directory if not exists
  5. Write file to disk
  6. If image: generate thumbnail (200x200)
  7. Create CandidateDocument record in DB with file path
  8. Return document ID
```

### Download Flow
```
GetDocumentQuery received
  1. Load CandidateDocument record
  2. Verify user has access (same tenant, appropriate permission)
  3. Resolve full file path
  4. Stream file bytes to response
  5. Log read audit
```

### Storage Structure
```
/data/
└── tenants/
    └── {tenant-slug}/
        └── candidates/
            └── {candidate-id}/
                ├── passport_abc123.pdf
                ├── photo_def456.jpg
                ├── photo_def456_thumb.jpg
                └── cv_generated_2026-07-13.pdf
```

---

## BL-05: Authentication Adaptations

### JWT Claims (Updated for Multi-Tenancy)
```json
{
  "sub": "{userId}",
  "email": "user@example.com",
  "role": "EmbassyOfficer",
  "tenant_id": "{tenantId}",
  "office_id": "{officeId}",
  "permissions": ["candidate.read", "embassy.update", ...],
  "is_super_admin": false
}
```

### Authorization Flow (Updated)
```
Request → AuthorizationBehavior:
  1. Extract permissions from JWT claims
  2. If IsSuperAdmin → bypass all permission checks
  3. If command implements IRequirePermission → check specific permission
  4. If command implements IRequireOfficeAccess → check officeId matches

Request → WorkflowAuthorizationBehavior:
  1. Extract user's officeId and roles from JWT
  2. If command targets a candidate → verify candidate belongs to user's office (or user is Agency Owner)
  3. If command executes a workflow transition → verify user's role is in allowed_roles for that transition
```

---

## BL-06: Docker Compose Setup

### Container Architecture
```yaml
services:
  api:        # .NET 10 API (port 5000)
  frontend:   # Next.js 15 (port 3000)
  postgres:   # PostgreSQL 16 (port 5432)
  # Volume: /data (file storage)
```

### Environment Configuration
- All secrets via environment variables (never in code)
- Connection string, JWT key, bot tokens as env vars
- `.env` file for local development (not committed)
- Docker volumes for: PostgreSQL data, file storage, logs

---

## BL-07: Health Checks

### Endpoints
- `GET /health` — Shallow check (API process running)
- `GET /health/ready` — Deep check (PostgreSQL connectable, schema resolution working)

### Checks
- PostgreSQL connectivity (existing NpgSql health check)
- Tenant schema resolution (can resolve at least one tenant)
- File storage directory writable
- SignalR hub responsive
