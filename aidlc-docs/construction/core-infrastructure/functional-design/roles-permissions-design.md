# Roles & Permissions Design — Multi-Tenant SaaS

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│  PUBLIC SCHEMA (Platform Level)                          │
│                                                         │
│  - Platform Admin users (SuperAdmin)                    │
│  - Tenant metadata (agencies list)                      │
│  - System permissions (platform-level)                  │
│  - ASP.NET Identity tables (ALL users across tenants)   │
│  - Exchange rates, system config                        │
└─────────────────────────────────────────────────────────┘
         │
         │  Each agency gets:
         ▼
┌─────────────────────────────────────────────────────────┐
│  TENANT SCHEMA: tenant_acme_agency                      │
│                                                         │
│  - Agency-specific roles (custom per agency)            │
│  - Agency-specific role-permission mappings             │
│  - Candidates, workflow, documents, finance...          │
│  - Agency offices, partners                             │
└─────────────────────────────────────────────────────────┘
```

## User Hierarchy

| Level | Role | Scope | Description |
|-------|------|-------|-------------|
| Platform | SuperAdmin | ALL tenants | You (SaaS owner). Manages all agencies. |
| Platform | PlatformSupport | ALL tenants (read) | Support staff. Can view any tenant. |
| Tenant | AgencyOwner | One tenant | Agency owner. Full control within their agency. |
| Tenant | Custom Roles... | One tenant | Agency-defined roles (Manager, Officer, etc.) |

## Key Design Decisions

1. **Users live in PUBLIC schema** (ASP.NET Identity) — one user account can only belong to ONE tenant (via TenantId FK)
2. **SuperAdmin has no TenantId** — they access any tenant by selecting it
3. **Roles are TENANT-SCOPED** — each agency defines their own roles
4. **Permissions are SYSTEM-DEFINED** — the available permission codes are fixed (defined by the platform). Agencies assign them to their custom roles.
5. **Role names can differ per agency** — Agency A might have "Processing Officer" while Agency B has "Embassy Handler" — both mapping to the same underlying permissions.

## Permission Model

### System Permissions (Platform-defined, immutable)
These are the building blocks. Agencies pick which ones to assign to their roles.

### Agency Roles (Tenant-defined, customizable)
Each agency creates their own roles and assigns permissions to them.

Example:
- Agency A: "Senior Officer" role → [candidate.read, candidate.create, workflow.execute, embassy.update]
- Agency B: "Embassy Team" role → [candidate.read, embassy.read, embassy.update]

## Database Tables

### Public Schema
- `asp_net_users` — All users (with TenantId column)
- `asp_net_roles` — System roles ONLY (SuperAdmin, PlatformSupport)
- `tenants` — Agency metadata
- `system_permissions` — Master list of all permission codes

### Tenant Schema (per agency)
- `tenant_roles` — Custom roles defined by this agency
- `tenant_role_permissions` — Which permissions each role has
- `tenant_user_roles` — Which users have which roles (within this tenant)
