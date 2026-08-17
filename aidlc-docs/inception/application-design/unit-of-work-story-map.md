# Unit of Work — Story Map

## Pre-Unit: Clinical Code Deletion
No user stories — infrastructure cleanup task.

---

## Unit 1: Core Infrastructure

| Story ID | Story Title | Epic |
|----------|-------------|------|
| US-11.01 | User Login | Cross-Cutting |
| US-11.02 | Multi-Factor Authentication | Cross-Cutting |
| US-11.03 | Real-Time Notifications (SignalR) | Cross-Cutting |
| US-11.04 | Tenant Data Isolation | Cross-Cutting |
| US-11.05 | Password Change and Expiry | Cross-Cutting |
| US-11.06 | Session Management | Cross-Cutting |
| US-8.07 | Provision New Tenant/Agency | Agency ERP |

**Total: 7 stories**

---

## Unit 2: Candidate & Workflow Engine

| Story ID | Story Title | Epic |
|----------|-------------|------|
| US-1.01 | Register New Candidate | Candidate Management |
| US-1.02 | Upload Candidate Documents | Candidate Management |
| US-1.03 | Search Candidates | Candidate Management |
| US-1.04 | Filter Candidates by Criteria | Candidate Management |
| US-1.05 | View Candidate Status Timeline | Candidate Management |
| US-1.06 | Generate Candidate CV | Candidate Management |
| US-1.07 | Edit Candidate Information | Candidate Management |
| US-1.08 | Soft Delete Candidate | Candidate Management |
| US-1.09 | View Candidate Documents | Candidate Management |
| US-1.10 | Assign Labour ID | Candidate Management |
| US-2.01 | View Workflow Configuration | Workflow Engine |
| US-2.02 | Add Workflow Stage | Workflow Engine |
| US-2.03 | Define Transition Rules | Workflow Engine |
| US-2.04 | Configure Dynamic Action Buttons | Workflow Engine |
| US-2.05 | Configure Stage Statuses | Workflow Engine |
| US-2.06 | Configure Parallel Tracks | Workflow Engine |
| US-2.07 | Configure Mirror View Rules | Workflow Engine |
| US-2.08 | Reorder Workflow Stages | Workflow Engine |
| US-2.09 | Configure Mandatory Fields Per Stage | Workflow Engine |
| US-2.10 | Seed Default Workflow Template | Workflow Engine |

**Total: 20 stories**

---

## Unit 3: Embassy & LMIS Processing

| Story ID | Story Title | Epic |
|----------|-------------|------|
| US-3.01 | Transfer Candidate to Embassy View | Embassy |
| US-3.02 | Book Medical Appointment | Embassy |
| US-3.03 | Record Medical Result | Embassy |
| US-3.04 | Book Tasheer Appointment | Embassy |
| US-3.05 | Record Tasheer Result | Embassy |
| US-3.06 | Mirror View Activation | Embassy |
| US-3.07 | Set Operational Status to Ready | Embassy |
| US-3.08 | Process Visa Documentation | Embassy |
| US-3.09 | Record Visa Outcome | Embassy |
| US-3.10 | Transfer to LMIS (Full Transfer) | Embassy |
| US-3.11 | Handle Visa Rejection | Embassy |
| US-4.01 | View LMIS Queue | LMIS |
| US-4.02 | Record Insurance Payment | LMIS |
| US-4.03 | Upload LMIS Documents | LMIS |
| US-4.04 | Track LMIS Milestone Progression | LMIS |
| US-4.05 | Transfer to Ticket View | LMIS |

**Total: 16 stories**

---

## Unit 4: Travel, Departure & Arrival

| Story ID | Story Title | Epic |
|----------|-------------|------|
| US-5.01 | View Ticket Queue | Travel |
| US-5.02 | Book Flight Ticket | Travel |
| US-5.03 | Transfer to Departure View | Travel |
| US-5.04 | View Departure Countdown | Travel |
| US-5.05 | Notify Candidate of Departure | Travel |
| US-5.06 | Record Successful Departure | Travel |
| US-5.07 | Handle Non-Departure | Travel |
| US-5.08 | Transfer to Arrival View | Travel |
| US-6.01 | Confirm Safe Arrival | Arrival |
| US-6.02 | Flag Exception — Returned | Arrival |
| US-6.03 | Flag Exception — Runaway | Arrival |
| US-6.04 | Exception Containment — Investigation | Arrival |
| US-6.05 | Exception Containment — Resolution | Arrival |
| US-6.06 | Add to Commission | Arrival |
| US-6.07 | View Arrival Ledger (Permanent) | Arrival |

**Total: 15 stories**

---

## Unit 5: Finance & Commission (ERP)

| Story ID | Story Title | Epic |
|----------|-------------|------|
| US-7.01 | View Commission Queue | Finance |
| US-7.02 | Record Fee Breakdown | Finance |
| US-7.03 | Record Payment (Double-Entry) | Finance |
| US-7.04 | Multi-Currency Support | Finance |
| US-7.05 | Bank Reconciliation | Finance |
| US-7.06 | Generate Financial Statements | Finance |
| US-7.07 | Track Disputes | Finance |
| US-7.08 | Per-Office Commission Reporting | Finance |
| US-7.09 | Tax Calculations | Finance |

**Total: 9 stories**

---

## Unit 6: Agency ERP (Staff, Office, Partners, Admin)

| Story ID | Story Title | Epic |
|----------|-------------|------|
| US-8.01 | Manage Staff/Employees | ERP |
| US-8.02 | Manage Offices/Branches | ERP |
| US-8.03 | Manage Partner Agency Links (Tenant) | ERP |
| US-8.03a | Manage Partner Catalog (Platform) | ERP |
| US-8.04 | Configure Roles and Permissions | ERP |
| US-8.05 | View Audit Trail | ERP |
| US-8.06 | Dashboard and KPIs | ERP |

**Total: 6 stories**

---

## Unit 7: Bot & Notifications

| Story ID | Story Title | Epic |
|----------|-------------|------|
| US-9.01 | Bot Infrastructure — Telegram Connection | Bot |
| US-9.02 | Bot Infrastructure — WhatsApp Connection | Bot |
| US-9.03 | Status Lookup via Bot | Bot |
| US-9.04 | Push Notifications for Stage Transitions | Bot |
| US-9.05 | Quick Action — Update Medical Status | Bot |
| US-9.06 | Quick Action — Confirm Arrival | Bot |
| US-9.07 | CV Generation via Bot | Bot |
| US-9.08 | Multi-Language Bot Responses | Bot |
| US-9.09 | Bot User Registration | Bot |

**Total: 9 stories**

---

## Unit 8: Reporting & Analytics

| Story ID | Story Title | Epic |
|----------|-------------|------|
| US-10.01 | Pipeline View Report | Reporting |
| US-10.02 | Agency Performance Dashboard | Reporting |
| US-10.03 | Office Comparison Report | Reporting |
| US-10.04 | Overdue/Stuck Candidates Alert | Reporting |
| US-10.05 | Export to Excel | Reporting |
| US-10.06 | Export to PDF | Reporting |
| US-10.07 | Scheduled Reports | Reporting |

**Total: 7 stories**

---

## Coverage Summary

| Unit | Stories | % of Total |
|------|---------|-----------|
| Unit 1: Core Infrastructure | 7 | 8% |
| Unit 2: Candidate & Workflow | 20 | 22% |
| Unit 3: Embassy & LMIS | 16 | 18% |
| Unit 4: Travel & Arrival | 15 | 16% |
| Unit 5: Finance & Commission | 9 | 10% |
| Unit 6: Agency ERP | 6 | 7% |
| Unit 7: Bot & Notifications | 9 | 10% |
| Unit 8: Reporting & Analytics | 7 | 8% |
| **Total** | **89** | **100%** |

All user stories from `stories.md` are assigned to exactly one unit. No stories are unassigned or duplicated.
