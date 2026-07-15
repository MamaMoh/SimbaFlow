# NFR Requirements — Unit 2: Candidate & Workflow Engine

## NFR-PERF: Performance Requirements

| ID | Requirement | Target | Context |
|----|-------------|--------|---------|
| PERF-10 | Candidate search (paginated) | < 300ms p95 | Full-text search + filters across 50K candidates |
| PERF-11 | Get workflow state (event replay) | < 50ms p95 | With snapshot every 20 events, max 20 events to replay |
| PERF-12 | Get available actions | < 100ms p95 | Evaluate conditions for all transition rules from current stage |
| PERF-13 | Execute transition (write) | < 500ms p95 | Validate + append event + update denormalized + broadcast |
| PERF-14 | Get view candidates (stage query) | < 300ms p95 | Query candidates in stage + mirror views, paginated |
| PERF-15 | Upload document (10MB) | < 5s | File write + DB record + thumbnail generation |
| PERF-16 | Generate CV (PDF) | < 3s | Template render + file save |
| PERF-17 | Workflow config load | < 200ms | Full definition with stages, statuses, rules |
| PERF-18 | Event stream query (timeline) | < 200ms | All events for one candidate (typically < 100) |

## NFR-SCALE: Scalability Requirements

| ID | Requirement | Target | Strategy |
|----|-------------|--------|----------|
| SCALE-10 | Candidates per tenant | 50,000+ | Indexed queries, pagination, denormalized stage |
| SCALE-11 | Workflow events per candidate | 500+ (long-lived) | Snapshot every 20 events limits replay |
| SCALE-12 | Total events per tenant | 5,000,000+ | Partitioned by candidateId, indexed by sequence |
| SCALE-13 | Concurrent view access | 50+ users same stage view | Read-only queries, no locks |
| SCALE-14 | Workflow stages per definition | 50+ | No hard limit in schema |
| SCALE-15 | Transition rules per definition | 200+ | JSONB conditions, lazy-loaded |

## NFR-SEC: Security Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| SEC-20 | Candidate data is PII — access must be authorized | candidate.read permission + office scope |
| SEC-21 | Document access is sensitive — read-audit required | IReadAuditService on every document download |
| SEC-22 | Workflow transitions must verify role + conditions server-side | Never trust client-provided action state |
| SEC-23 | Candidate passport numbers must not appear in logs | PII redaction in Serilog destructuring policy |
| SEC-24 | Workflow configuration changes are high-privilege | workflow.configure permission (Agency Owner/Admin only) |
| SEC-25 | Event stream is tamper-proof (append-only, no updates/deletes) | No UPDATE/DELETE on workflow_events table |

## NFR-RES: Resiliency Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| RES-20 | Event append must be atomic with denormalized update | Single transaction (event + candidate update) |
| RES-21 | Snapshot corruption should not lose data | Events are source of truth; snapshot can be rebuilt |
| RES-22 | Failed transition should not leave partial state | Transaction rollback on any failure |
| RES-23 | File upload failure should not create orphan DB records | Transaction: DB record only committed after successful file write |
| RES-24 | SignalR broadcast failure should not fail the transition | Fire-and-forget broadcast (non-blocking) |

## NFR-TEST: PBT Requirements (Unit-Specific)

| ID | Requirement | PBT Rule | Implementation |
|----|-------------|----------|----------------|
| TEST-10 | Event replay produces identical state | PBT-03 (Invariant) | FsCheck: replay(events) == replay(events) for any generated event sequence |
| TEST-11 | Snapshot + remaining events == full replay | PBT-02 (Round-trip) | FsCheck: state_from_snapshot_plus_tail == state_from_all_events |
| TEST-12 | Transition atomic rejection | PBT-04 (Idempotence) | FsCheck: failed transition leaves state unchanged |
| TEST-13 | Condition evaluation is deterministic | PBT-04 (Idempotence) | FsCheck: evaluate(cond, state) always returns same result |
| TEST-14 | Mirror view activation/deactivation symmetry | PBT-03 (Invariant) | FsCheck: visible_in_stages consistent with condition evaluation |
| TEST-15 | Candidate never disappears from all views | PBT-03 (Invariant) | FsCheck: for any event sequence, candidate is in at least one stage |
| TEST-16 | Stateful workflow engine test | PBT-06 (Stateful) | FsCheck: random command sequences (register, transition, update status) maintain invariants |
| TEST-17 | Available actions determinism | PBT-04 (Idempotence) | FsCheck: same inputs always produce same action set |

## NFR-USAB: Usability Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| USAB-10 | Action buttons update immediately when conditions change | Optimistic client evaluation + SignalR push |
| USAB-11 | Candidate search provides instant feedback | Debounced search (300ms), skeleton loading |
| USAB-12 | Workflow view shows candidate count per stage | Badge counts in sidebar navigation |
| USAB-13 | Timeline shows human-readable descriptions | Event type mapped to natural language |
| USAB-14 | Document upload shows progress bar | Stream upload with progress events |
| USAB-15 | Workflow config changes have undo capability | Version history with rollback to previous version |

## Tech Stack Additions (Unit 2-Specific)

| Package | Version | Purpose |
|---------|---------|---------|
| QuestPDF | 2024.x | CV PDF generation |
| (No new backend packages) | — | Uses existing EF Core, MediatR, FluentValidation |
| (No new frontend packages) | — | Uses existing SWR, Zustand, React Hook Form, Zod |

## Testable Properties Summary (from Functional Design)

Carried forward from `business-rules.md` — these 8 properties MUST have FsCheck implementations:

1. Event replay idempotence
2. Snapshot consistency (round-trip)
3. Transition atomic rejection (idempotence)
4. Mirror view symmetry (invariant)
5. Condition evaluation commutativity
6. Candidate search invariant (no lost candidates)
7. Available actions determinism (idempotence)
8. Stateful workflow model-based test (random command sequences)
