# Full Database Schema — Candidate & Workflow (SimbaFlow)

## Tenancy

- **Public schema**: Identity, tenants, permissions, platform audit
- **Tenant schema** (`tenant_*`): candidates, workflow, offices, partners, stays

Isolation: schema-per-tenant via `TenantConnectionInterceptor` (not shared-schema RLS).

## Adopt vs reject from ERP_Architecture_and_Database_Design.md

| ERP design | SimbaFlow |
|------------|-----------|
| `candidate_medical`, `candidate_tasheer`, … | **Rejected** — statuses in `candidates.current_status_values` JSONB + `workflow_events` + step stays |
| `current_stage TEXT` | **Rejected** — `current_stage_id` UUID FK |
| Shared schema + `office_id` RLS | **Rejected** — schema-per-tenant; `office_id` = branch |
| `candidate_sponsor_visa` | **Adopted** as `candidate_placements` |
| `candidate_relatives` / `candidate_skills` | **Adopted** |
| `candidate_stage_history` | **Replaced** by `candidate_stage_stays` + events |
| Task assignment / status transition permissions | **Adopted** |
| Pipeline business rules | **Adopted** as seeded workflow config |

## Tenant tables

### Agency
- `offices` — branch offices
- `partners` — overseas sponsors

### Candidates
- `candidates` — identity, address, timing denorm, workflow denorm
- `candidate_placements` — 1:1 visa/sponsor/contract
- `candidate_relatives` — 1:N
- `candidate_skills` — 1:1
- `candidate_documents`
- `candidate_stage_stays` — enter/exit/duration per stage visit
- `candidate_step_stays` — start/finish/duration per track status
- `candidate_returned`
- `candidate_complaints`
- `candidate_commissions` — amount metadata (status in JSONB)

### Workflow config
- `workflow_definitions`, `workflow_stages` (+ SLA hours), `workflow_stage_statuses`
- `parallel_track_definitions`, `mirror_view_rules`, `workflow_transition_rules`, `stage_mandatory_fields`
- `task_assignments`, `status_transition_permissions`

### Workflow runtime
- `workflow_events` (append-only)
- `workflow_snapshots`

## Timing model

1. Event `Timestamp` = source of truth  
2. `candidate_stage_stays` / `candidate_step_stays` = queryable ledgers  
3. Candidate denorm: `current_stage_entered_at`, `last_action_at`, `last_action_label`, `flight_date`, `is_overdue`

## Default seeded pipeline

`New Contracts → Embassy → LMIS → Ticket → Depart → Arrival → Commission`

Embassy parallel tracks: `medical`, `tasheer`.  
Mirror: LMIS preview when medical=Fit AND tasheer=Done.  
To LMIS requires embassy=Issued.  
embassy→Submitted allowed for CaseExecutive only.
