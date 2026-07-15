# User Personas

## Persona 1: Amir — Agency Owner

| Attribute | Value |
|-----------|-------|
| **Role** | Agency Owner (Super-Admin per tenant) |
| **Age** | 45-60 |
| **Tech Savvy** | Medium |
| **Primary Device** | Desktop + Mobile |
| **Language** | Amharic (primary), English (secondary) |

**Background**: Owns and operates a labour export agency with 2-5 branch offices. Has 15+ years in the overseas employment industry. Manages relationships with overseas partner agencies and government bodies.

**Goals**:
- Full visibility into all candidates across all offices and stages
- Revenue and commission tracking with financial statements
- Configure workflow to match their agency's specific processes
- Monitor staff performance and office-level KPIs
- Ensure compliance with government regulations

**Frustrations**:
- Loses track of candidates stuck in pipeline stages
- Manual Excel tracking leads to data loss and errors
- Cannot easily compare office performance
- No real-time visibility into field agent activities

**Key Workflows**: Dashboard review, financial reports, workflow configuration, staff management, office comparison

---

## Persona 2: Hana — Office Manager

| Attribute | Value |
|-----------|-------|
| **Role** | Office Manager (Branch-level admin) |
| **Age** | 30-45 |
| **Tech Savvy** | Medium-High |
| **Primary Device** | Desktop |
| **Language** | Amharic (primary), English (working) |

**Background**: Manages a single branch office with 5-15 staff. Responsible for day-to-day operations, candidate throughput, and local staff coordination.

**Goals**:
- Ensure candidates move through pipeline efficiently
- Manage office staff assignments and permissions
- Generate office-level reports for agency owner
- Handle escalations and stuck candidates
- Coordinate between embassy officers and field agents

**Frustrations**:
- No single view of all candidates in her office
- Cannot easily identify bottlenecks
- Manual notification of overdue candidates
- Difficulty coordinating with overseas offices

**Key Workflows**: Office dashboard, candidate oversight, staff assignment, escalation handling, report generation

---

## Persona 3: Dawit — Embassy Officer

| Attribute | Value |
|-----------|-------|
| **Role** | Embassy Officer |
| **Age** | 25-40 |
| **Tech Savvy** | Medium |
| **Primary Device** | Desktop |
| **Language** | Amharic, English |

**Background**: Handles the embassy processing stage — medical bookings, Tasheer appointments, visa tracking. Works closely with embassy contacts and medical facilities.

**Goals**:
- Process candidates through embassy stage quickly
- Track medical and Tasheer statuses simultaneously
- Know immediately when a candidate is "embassy-ready"
- Hand off to Case Executive smoothly
- Track visa outcomes (Issued/Rejected)

**Frustrations**:
- Juggling parallel tracks (medical + Tasheer) manually
- Missing the moment when both tracks complete
- No automatic notification when visa is issued
- Paper-based tracking of appointment dates

**Key Workflows**: Embassy view management, medical booking, Tasheer tracking, visa status updates, transfer to LMIS

---

## Persona 4: Sara — Case Executive

| Attribute | Value |
|-----------|-------|
| **Role** | Case Executive |
| **Age** | 25-35 |
| **Tech Savvy** | Medium |
| **Primary Device** | Desktop |
| **Language** | English (primary), Amharic |

**Background**: Processes physical documentation for visa applications. Receives candidates from Embassy Officers when status is "Ready" and prepares submission packets.

**Goals**:
- Clear queue of ready candidates efficiently
- Track which documents are submitted vs pending
- Update submission status accurately
- Coordinate with Embassy Officer on outcomes

**Frustrations**:
- Receives candidates without knowing full context
- No automated notification when new candidates are assigned
- Manual tracking of submission dates
- Unclear handoff responsibilities

**Key Workflows**: Case executive view, document preparation, status update to Submitted, coordination with embassy

---

## Persona 5: Yonas — Finance Officer

| Attribute | Value |
|-----------|-------|
| **Role** | Finance |
| **Age** | 30-50 |
| **Tech Savvy** | Medium |
| **Primary Device** | Desktop |
| **Language** | Amharic, English |

**Background**: Manages agency finances — commission calculations, payment tracking, fee collection, bank reconciliation. Reports to Agency Owner on financial health.

**Goals**:
- Accurate commission tracking per candidate
- Record all payments with proper double-entry
- Generate financial reports (P&L, balance sheet)
- Track outstanding balances per office
- Handle multi-currency transactions
- Resolve payment disputes

**Frustrations**:
- Manual commission calculations are error-prone
- No clear audit trail of financial transactions
- Difficulty reconciling bank statements
- Cannot generate statements on demand

**Key Workflows**: Commission management, payment recording, bank reconciliation, financial reporting, dispute resolution

---

## Persona 6: Kebede — Field Agent

| Attribute | Value |
|-----------|-------|
| **Role** | Field Agent |
| **Age** | 22-35 |
| **Tech Savvy** | Low-Medium |
| **Primary Device** | Mobile (Telegram/WhatsApp) |
| **Language** | Amharic (primary) |

**Background**: Works in the field — recruits candidates, collects documents, confirms arrivals, notifies candidates of flight dates. Rarely at a desk. Relies heavily on mobile communication.

**Goals**:
- Quickly look up candidate status on mobile
- Receive push notifications when action is needed
- Update candidate information (medical result, arrival) from field
- Generate CVs for candidates on the go
- Communicate in Amharic

**Frustrations**:
- No mobile access to system data
- Must call office to check status
- Cannot update records from field
- Language barriers in existing tools

**Key Workflows**: Status lookup via bot, quick updates, notification receipt, CV generation, arrival confirmation

---

## Persona 7: Tigist — Data Entry Clerk

| Attribute | Value |
|-----------|-------|
| **Role** | Data Entry Clerk |
| **Age** | 20-30 |
| **Tech Savvy** | Medium |
| **Primary Device** | Desktop |
| **Language** | Amharic, basic English |

**Background**: Handles initial candidate registration — enters biographical data, uploads documents, scans passports. High-volume, repetitive work requiring accuracy.

**Goals**:
- Register candidates quickly and accurately
- Upload documents efficiently
- Avoid duplicate registrations
- Search existing candidates before creating new ones
- Correct data entry errors promptly

**Frustrations**:
- Slow forms with too many required fields
- No duplicate detection
- Document upload is cumbersome
- Cannot bulk-import candidates

**Key Workflows**: Candidate registration, document upload, duplicate checking, data correction, search

---

## Persona 8: Abebe — Auditor

| Attribute | Value |
|-----------|-------|
| **Role** | Auditor (Read-only) |
| **Age** | 35-55 |
| **Tech Savvy** | Medium |
| **Primary Device** | Desktop |
| **Language** | English, Amharic |

**Background**: External or internal auditor who reviews operations, financial records, and compliance. Needs comprehensive read access but cannot modify any data.

**Goals**:
- View all candidate records and their history
- Audit financial transactions and commission calculations
- Review workflow transition logs (who moved what, when)
- Generate compliance reports
- Export data for external analysis

**Frustrations**:
- Cannot access historical data easily
- No consolidated audit view
- Must request data from multiple people
- No export capabilities

**Key Workflows**: Audit trail review, financial audit, candidate history inspection, report generation, data export

---

## Persona 9: Meron — Notification Manager

| Attribute | Value |
|-----------|-------|
| **Role** | Notification Manager |
| **Age** | 25-40 |
| **Tech Savvy** | High |
| **Primary Device** | Desktop |
| **Language** | English, Amharic |

**Background**: Configures notification rules, manages bot settings, ensures candidates and staff receive timely alerts. Bridges the technical and operational sides.

**Goals**:
- Configure which stage transitions trigger notifications
- Manage notification templates (both languages)
- Monitor notification delivery status
- Configure bot commands and permissions
- Ensure departure countdown alerts work correctly

**Frustrations**:
- No centralized notification configuration
- Cannot test notification delivery
- No visibility into failed notifications
- Template changes require developer intervention

**Key Workflows**: Notification rule configuration, template management, bot configuration, delivery monitoring

---

## Persona 10: Solomon — System Admin

| Attribute | Value |
|-----------|-------|
| **Role** | Admin (System-wide) |
| **Age** | 28-45 |
| **Tech Savvy** | High |
| **Primary Device** | Desktop |
| **Language** | English |

**Background**: IT administrator managing the SimbaFlow platform. Handles user provisioning, role assignment, system configuration, and troubleshooting.

**Goals**:
- Provision new agencies (tenant setup)
- Manage users, roles, and permissions
- Configure system-wide settings
- Monitor system health and performance
- Handle support escalations

**Frustrations**:
- Manual tenant provisioning process
- No bulk user import
- Difficult to troubleshoot permission issues
- No system health dashboard

**Key Workflows**: Tenant provisioning, user management, role/permission configuration, system monitoring, troubleshooting

---

## Persona 11: External API System

| Attribute | Value |
|-----------|-------|
| **Role** | API Integration User |
| **Age** | N/A (system account) |
| **Tech Savvy** | N/A |
| **Primary Device** | API calls |
| **Language** | N/A |

**Background**: Service account used for external system integrations — government LMIS API (future), partner agency systems, notification services (Telegram/WhatsApp APIs).

**Goals**:
- Authenticate via API credentials
- Access candidate data programmatically
- Push status updates from external systems
- Receive webhooks for stage transitions
- Rate-limited to prevent abuse

**Frustrations**:
- N/A (system account)

**Key Workflows**: API authentication, data sync, status push, webhook reception

---

## Persona-to-Module Matrix

| Persona | Candidate | Workflow | Embassy | LMIS | Travel | Arrival | Finance | ERP | Bot | Reports |
|---------|-----------|----------|---------|------|--------|---------|---------|-----|-----|---------|
| Amir (Owner) | View | Config | View | View | View | View | Full | Full | Notify | Full |
| Hana (Office Mgr) | Full | View | View | View | View | View | View | Office | Notify | Office |
| Dawit (Embassy) | View | — | Full | View | — | — | — | — | Notify | — |
| Sara (Case Exec) | View | — | Partial | — | — | — | — | — | Notify | — |
| Yonas (Finance) | View | — | — | — | — | — | Full | View | Notify | Finance |
| Kebede (Field) | Update | — | Update | — | — | Update | — | — | Full | — |
| Tigist (Data Entry) | Create | — | — | — | — | — | — | — | — | — |
| Abebe (Auditor) | Read | Read | Read | Read | Read | Read | Read | Read | — | Full |
| Meron (Notif Mgr) | — | — | — | — | — | — | — | Config | Config | — |
| Solomon (Admin) | — | Config | — | — | — | — | — | Full | Config | System |
| API System | Read/Write | — | Write | Write | — | Write | — | — | — | — |
