# Domain Entities — Unit 1: Core Infrastructure

## Entity: TenantInfo (Adapt Existing)

```
TenantInfo : BaseEntity
├── Name : string (required, max 200)
├── Slug : string (required, unique, max 50, URL-safe)
├── SchemaName : string (required, unique, max 63, PostgreSQL identifier)
├── ContactEmail : string (required)
├── ContactPhone : string?
├── Address : string?
├── City : string?
├── Country : string?
├── SubscriptionStatus : TenantStatus (Active, Suspended, Deactivated)  // SaaS
├── MaxUsers : int (default: 50)
├── ProvisionedAt : DateTime
├── ProvisionedBy : string
├── Settings : TenantSettings (JSON)
│
│ // MoLS / Directive 1126/2018 — planned (see partner-agency-and-tenant-licensing.md)
├── AgencyLevel : int (1..5)                          // ደረጃ
├── LicenseNumber : string?
├── LicenseIssuedAt : DateOnly?
├── LicenseExpiresAt : DateOnly?
├── LicenseStatus : AgencyLicenseStatus               // distinct from SubscriptionStatus
├── LicensedCountries : string[]                      // destination country caps
├── CapitalEtb : decimal?
└── BondUsd : decimal?
```

**Related (Unit 6):** `PartnerAgency` lives in **public** schema; `PartnerLink` binds tenant ↔ partner with agreement dates.

## Entity: TenantSettings (Value Object, stored as JSONB)

```
TenantSettings
├── DefaultLanguage : string (default: "en")
├── SupportedLanguages : string[] (default: ["en", "am"])
├── DefaultCurrency : string (default: "ETB")
├── SupportedCurrencies : string[] (default: ["ETB", "USD", "SAR", "AED"])
├── FileStoragePath : string (derived: /data/tenants/{slug}/)
├── MaxFileUploadSizeMB : int (default: 10)
├── SignalREnabled : bool (default: true)
└── BotEnabled : bool (default: false)
```

## Entity: SystemConfiguration (Public Schema)

```
SystemConfiguration : BaseEntity
├── Key : string (required, unique)
├── Value : string (required)
├── Description : string?
├── Category : string (required)
└── IsEncrypted : bool (default: false)
```

## Entity: ExchangeRate (Public Schema)

```
ExchangeRate : BaseEntity
├── FromCurrency : string (required, 3-char ISO)
├── ToCurrency : string (required, 3-char ISO)
├── Rate : decimal (required, precision 18,8)
├── EffectiveDate : DateOnly (required)
└── Source : string? (manual/api)
```

## Enum: TenantStatus

```
TenantStatus
├── Active = 0
├── Suspended = 1
└── Deactivated = 2
```

## Adapted Identity Entities (Keep + Modify)

### ApplicationUser (Add TenantId enforcement)
```
ApplicationUser (existing — add/modify)
├── TenantId : Guid (required for non-system users)
├── OfficeId : Guid? (replaces ActiveLocationId)
├── PreferredLanguage : string (default: "en")
└── BotLinked : bool (default: false)
```

### ApplicationRole (Add TenantId scoping)
```
ApplicationRole (existing — add)
└── TenantId : Guid? (null = system role, non-null = tenant-specific role)
```

### Permission (New permissions for labour export)
```
Permission (existing entity, new permission codes)
├── candidate.read, candidate.create, candidate.update, candidate.delete
├── workflow.execute, workflow.configure, workflow.view
├── embassy.read, embassy.update
├── lmis.read, lmis.update
├── travel.read, travel.update
├── arrival.read, arrival.update, arrival.exception
├── commission.read, commission.create, commission.update
├── accounting.read, accounting.post, accounting.reconcile
├── office.read, office.create, office.update
├── staff.read, staff.create, staff.update, staff.terminate
├── partner.read, partner.create, partner.update
├── notification.configure, notification.send
├── report.view, report.export, report.schedule
├── tenant.provision, tenant.manage
├── audit.read
└── bot.configure, bot.use
```

## Schema Layout

### Public Schema (Shared across all tenants)
- `tenants` — TenantInfo records
- `system_configuration` — Global settings
- `exchange_rates` — Currency rates
- `asp_net_*` — Identity tables (users, roles, claims, tokens) — tenant-scoped by TenantId column

### Tenant Schema (One per agency: e.g., `tenant_acme`)
- All domain tables (candidates, workflow events, commissions, etc.)
- Created dynamically on tenant provisioning
- Migrations applied per-schema

### Schema Resolution Flow
```
Request → JWT has TenantId claim
  → TenantSchemaResolver reads TenantId
  → Looks up SchemaName from tenants table (public schema)
  → Sets DbContext search_path to "{SchemaName}, public"
  → All queries scoped to tenant schema
  → Public schema tables (exchange_rates, system_config) accessible via "public." prefix
```
