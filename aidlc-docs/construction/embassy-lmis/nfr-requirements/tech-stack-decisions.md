# Tech Stack Decisions — Unit 3: Embassy & LMIS Processing

## Confirmed stack (inherited)

No new frameworks. Unit 3 is a **domain specialization layer** on Unit 2’s workflow engine.

| Component | Technology | Unit 3 notes |
|-----------|------------|--------------|
| API | Carter + MediatR | `EmbassyModule`, `LmisModule` |
| Validation | FluentValidation | Per-intent validators |
| Persistence | TenantDbContext | Same Candidate / WorkflowEvent tables |
| Real-time | SignalR | Existing stage/status handlers |
| Frontend | Next.js + SWR + sonner | Named stage boards |
| PBT | FsCheck | Embassy/LMIS property suite |

## Unit 3–specific decisions

### Decision 1: Intent modules over raw UpdateStatus in UI
- **Choice**: Dedicated `EmbassyModule` + `LmisModule` with named endpoints
- **Rationale**: Encodes track preconditions, appointment payloads, rejection reason, milestone sequence, and insurance side-effects in one place; matches application design (AM-04/AM-05)
- **Trade-off**: More handlers than a thin frontend; generic WorkflowModule remains for admin/transitions

### Decision 2: Case Executive as mirror-only stage
- **Choice**: Seed a Case Executive `WorkflowStage` used only via `VisibleInStages`
- **Rationale**: Reuses board query infrastructure; FR-03.5 “appears in Case Executive while remaining in Embassy”; clears on To LMIS with RemoveFromSource
- **Trade-off**: Existing tenants need seeder backfill / mirror rule upsert (not only “skip if definition exists”)

### Decision 3: Appointment & rejection metadata in JSONB
- **Choice**: Store in `WorkflowEvent.Data` + denormalize selected keys into `CurrentStatusValues`
- **Rationale**: No schema migration for new columns; board columns stay fast; events remain audit source of truth
- **Trade-off**: Weakly typed string keys — mitigated by shared constants + validators

### Decision 4: Insurance Paid → Available as two events
- **Choice**: Append Paid then Available in one transaction
- **Rationale**: Clear audit trail for payment vs operational availability
- **Trade-off**: Slightly more events; UI reads final Available

### Decision 5: Milestone gating in intent handler (not only transition rules)
- **Choice**: `AdvanceLmisMilestoneCommand` enforces allowed-next map
- **Rationale**: Status updates are not stage transitions; transition rules alone cannot prevent Uploaded→Issued skip
- **Trade-off**: Rule duplicated conceptually with UI constrained select — server is authoritative

### Decision 6: No government LMIS API client in Unit 3
- **Choice**: Document upload + manual milestone only (FR-04.5 readiness)
- **Rationale**: External API contracts unknown; avoid speculative adapters
- **Future**: Circuit breaker + Polly when integration unit lands

### Decision 7: Frontend named routes resolve by stage name
- **Choice**: `/workflow/embassy`, `/workflow/case-executive`, `/workflow/lmis` resolve definition stages by name/slug
- **Rationale**: Stable UX URLs; agencies renaming stages in admin can break name binding — document that default names are reserved for these routes, or bind by `StageType` + sort order
- **Mitigation**: Prefer lookup by well-known stage name constants seeded by platform; admin rename warned in UI (follow-up)

### Decision 8: Permissions are additive codes
- **Choice**: Add `embassy.*` and `lmis.*` permission codes; map onto tenant roles
- **Rationale**: Least privilege between Embassy Officer vs Case Executive
- **Trade-off**: Seed default role↔permission mappings for new tenants; existing tenants may need a one-time permission seed
