# Frontend Components — Unit 1: Core Infrastructure

## FC-01: SignalR Connection Provider

### Component: `SignalRProvider`
- **Type**: React context provider (wraps entire app)
- **Purpose**: Manage SignalR WebSocket connection lifecycle
- **Props**: `children: ReactNode`
- **State**: `connectionStatus: 'connecting' | 'connected' | 'disconnected' | 'reconnecting'`
- **Context Exposed**:
  - `connection: HubConnection | null`
  - `status: ConnectionStatus`
  - `subscribe(event: string, handler: Function): void`
  - `unsubscribe(event: string, handler: Function): void`

### Behavior
```
On mount (authenticated):
  1. Create HubConnectionBuilder with JWT access token
  2. Configure auto-reconnect with exponential backoff
  3. Start connection
  4. On candidate updates: invalidate SWR cache for affected queries
  
On token refresh:
  1. Stop current connection
  2. Reconnect with new token
  
On unmount:
  1. Stop connection gracefully
```

---

## FC-02: Tenant Context Provider

### Component: `TenantProvider`
- **Type**: React context provider
- **Purpose**: Provide tenant info (name, settings, offices) to the app
- **Props**: `children: ReactNode`
- **State**: Tenant info loaded from `/api/auth/me` response
- **Context Exposed**:
  - `tenant: { id, name, slug, settings }`
  - `currentOffice: { id, name }`
  - `userPermissions: string[]`
  - `hasPermission(permission: string): boolean`

---

## FC-03: Updated Navigation Shell

### Component: `Sidebar` (Adapt existing)
- **Purpose**: Main navigation sidebar with labour export menu items
- **Navigation Items** (permission-gated):

```
Dashboard (overview) — all roles
Candidates — candidate.read
Workflow Views:
  ├── New Contracts — workflow.view
  ├── Embassy — embassy.read
  ├── LMIS — lmis.read
  ├── Tickets — travel.read
  ├── Departures — travel.read
  ├── Arrivals — arrival.read
  └── Commissions — commission.read
Finance:
  ├── Accounting — accounting.read
  ├── Bank Reconciliation — accounting.reconcile
  └── Financial Statements — accounting.read
Bot & Notifications — notification.configure
Reports — report.view
Admin:
  ├── Users — user management permissions
  ├── Roles — role management permissions
  ├── Staff — staff.read
  ├── Offices — office.read
  ├── Partners — partner.read
  ├── Workflow Config — workflow.configure
  ├── Tenants — tenant.manage (system admin only)
  └── Audit Trail — audit.read
```

### Behavior
- Menu items hidden if user lacks permission (via `hasPermission()`)
- Active item highlighted based on current route
- Collapsible sections for workflow views and admin
- Real-time notification badge in header (from SignalR)

---

## FC-04: Notification Toast System

### Component: `NotificationListener`
- **Type**: Hook + UI component
- **Purpose**: Listen to SignalR events and show toast notifications
- **Integration**: Uses `useSignalR()` hook from SignalRProvider
- **Events Handled**:
  - `candidateUpdated` → Show toast: "[Candidate Name] moved to [Stage]"
  - `personalNotification` → Show toast with action button
  - `systemAlert` → Show persistent alert

### Toast Behavior
- Non-intrusive (bottom-right, auto-dismiss after 5s)
- Click to navigate to affected candidate
- Stack up to 3 visible toasts
- Use Sonner library (existing)

---

## FC-05: Tenant Admin Page (System Admin Only)

### Route: `/admin/tenants`
### Component: `TenantManagementPage`
- **Permission Required**: `tenant.manage`
- **Features**:
  - List all tenants with: name, status, user count, provisioned date
  - "Create Agency" button → provisioning form
  - Status toggle (Active/Suspended)
  - Click tenant to view details

### Component: `CreateTenantForm`
- **Fields**: Agency Name, Slug (auto-generated from name), Contact Email, Contact Phone, Admin First Name, Admin Last Name, Admin Email, Temporary Password
- **Validation**: Zod schema, real-time slug availability check
- **On Submit**: POST /api/tenants → creates schema + seeds + provisions admin user

---

## FC-06: Provider Hierarchy

```tsx
<SessionProvider>          {/* next-auth */}
  <ThemeProvider>          {/* next-themes */}
    <TenantProvider>       {/* tenant context */}
      <SignalRProvider>     {/* real-time connection */}
        <Toaster />        {/* sonner notifications */}
        <NotificationListener />
        {children}
      </SignalRProvider>
    </TenantProvider>
  </ThemeProvider>
</SessionProvider>
```
