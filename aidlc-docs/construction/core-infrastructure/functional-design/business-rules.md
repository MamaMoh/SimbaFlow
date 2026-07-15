# Business Rules — Unit 1: Core Infrastructure

## BR-01: Tenant Provisioning Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-01.1 | Agency name must be unique across all tenants | Unique constraint on TenantInfo.Name |
| BR-01.2 | Tenant slug must be URL-safe (lowercase, alphanumeric + hyphens, max 50 chars) | Regex validation: `^[a-z][a-z0-9-]{1,48}[a-z0-9]$` |
| BR-01.3 | Schema name must be valid PostgreSQL identifier (max 63 chars) | Derived from slug: `tenant_{slug}` with underscores |
| BR-01.4 | Only System Admin can provision tenants | Permission: `tenant.provision` required |
| BR-01.5 | Admin email must be valid and not already in use | Email format validation + uniqueness check |
| BR-01.6 | A deactivated tenant cannot be reactivated without System Admin approval | Status transition: Deactivated → Active requires `tenant.manage` permission |
| BR-01.7 | Suspended tenants retain data but users cannot log in | Auth middleware checks tenant status; returns 403 for suspended tenants |

## BR-02: Tenant Schema Isolation Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-02.1 | All data queries MUST be scoped to the authenticated user's tenant schema | TenantSchemaResolver sets search_path before every DB operation |
| BR-02.2 | System Admin can access any tenant's schema | If user.IsSuperAdmin, allow explicit tenant targeting |
| BR-02.3 | Users without a TenantId can only access public schema | Non-tenant users cannot execute tenant-scoped operations |
| BR-02.4 | Public schema tables are readable by all authenticated users | Exchange rates, system config accessible regardless of tenant |
| BR-02.5 | Tenant schema name cannot be changed after creation | Immutable field, enforced at entity level |
| BR-02.6 | Database connections must set search_path at the START of each request | Middleware/interceptor level, not handler level |

## BR-03: Authentication & Session Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-03.1 | JWT access token expires after 15 minutes | Token validation middleware checks `exp` claim |
| BR-03.2 | Refresh token expires after 7 days | Refresh token rotation on each use |
| BR-03.3 | Maximum 5 active refresh tokens per user | Oldest token revoked when limit exceeded |
| BR-03.4 | Suspended tenant users receive 403 on any request | Middleware checks tenant status from cache |
| BR-03.5 | Password must meet complexity requirements (8+ chars, upper, lower, digit, special) | ASP.NET Core Identity password validators |
| BR-03.6 | Account locks after 5 failed login attempts (15 min lockout) | Identity lockout settings |
| BR-03.7 | MFA required for roles: Admin, Agency Owner, Finance | Configurable per-role MFA enforcement |
| BR-03.8 | First login forces password change | `MustChangePassword` flag on user entity |

## BR-04: File Storage Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-04.1 | Maximum file size: 10MB | Validated before disk write; Kestrel request size limit |
| BR-04.2 | Allowed file types: PDF, JPG, JPEG, PNG, DOCX | Extension + MIME type validation (check magic bytes, not just extension) |
| BR-04.3 | Files are stored in tenant-specific directories | Path derived from tenant slug, never user-input paths |
| BR-04.4 | File paths must not contain path traversal characters | Reject `..`, absolute paths, special characters |
| BR-04.5 | Thumbnails generated for image files only | Detect image MIME type → generate 200x200 thumbnail |
| BR-04.6 | File deletion is soft (flag in DB, file remains on disk) | IsDeleted flag on CandidateDocument; physical cleanup via scheduled job |
| BR-04.7 | File access requires same-tenant membership + read permission | Checked before streaming file bytes |

## BR-05: SignalR Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-05.1 | Only authenticated users can connect to SignalR hub | JWT authentication on hub connection |
| BR-05.2 | Users only receive events for their own tenant | Group membership scoped by TenantId |
| BR-05.3 | Office-level events only sent to users in that office | Additional group scoping by OfficeId |
| BR-05.4 | Personal notifications sent only to target user | User-specific group: `user:{userId}` |
| BR-05.5 | Reconnection must re-authenticate and re-join groups | OnConnectedAsync re-executes on reconnect |
| BR-05.6 | SignalR connection does not block API responses | Broadcasts are fire-and-forget (no await) |

## BR-06: Authorization Rules (Updated for Labour Export)

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-06.1 | SuperAdmin bypasses all permission checks | First check in AuthorizationBehavior |
| BR-06.2 | Agency Owner has all permissions within their tenant | Agency Owner role includes all tenant-level permissions |
| BR-06.3 | Office Manager has all permissions within their office only | Office-scoped check in WorkflowAuthorizationBehavior |
| BR-06.4 | Each API operation requires a specific permission code | Commands implement IRequirePermission |
| BR-06.5 | Workflow transitions have per-transition role restrictions | TransitionRule.AllowedRoles checked by WorkflowAuthorizationBehavior |
| BR-06.6 | Read operations on sensitive data (finance, audit) require explicit permission | audit.read, accounting.read enforced |
| BR-06.7 | Role changes take immediate effect (no token invalidation grace period) | Permissions re-checked from DB on each request for critical operations |

## BR-07: Audit Rules

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-07.1 | All write operations are automatically audit-logged | AuditBehavior in MediatR pipeline |
| BR-07.2 | Audit log includes: userId, action, entity, old values, new values, timestamp | Captured in AuditBehavior |
| BR-07.3 | Sensitive read operations are read-audit-logged | IReadAuditService called explicitly |
| BR-07.4 | Audit logs are append-only (cannot be deleted by application users) | No DELETE endpoint for audit; Auditor role is read-only |
| BR-07.5 | Audit logs are stored in the tenant schema (tenant-isolated) | Same schema resolution applies |
| BR-07.6 | Audit log retention: minimum 365 days | No auto-purge; archive strategy for older data |
