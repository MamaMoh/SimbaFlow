# NFR Requirements — Unit 3: Embassy & LMIS Processing

Inherits all Unit 1–2 NFR baselines. This document adds Unit 3–specific targets for embassy parallel tracks, Case Executive mirror, LMIS milestones, and intent APIs.

## NFR-PERF: Performance Requirements

| ID | Requirement | Target | Context |
|----|-------------|--------|---------|
| PERF-30 | Embassy board query (paginated) | < 300ms p95 | Stage + VisibleInStages filter, office scope |
| PERF-31 | Case Executive board query | < 300ms p95 | Mirror-only VisibleInStages filter |
| PERF-32 | LMIS board query (incl. mirrors) | < 300ms p95 | Primary LMIS + Embassy-mirrored rows |
| PERF-33 | Intent status update (Book Medical, etc.) | < 500ms p95 | Validate + UpdateStatus + mirror re-eval + SignalR |
| PERF-34 | Mirror activation latency | < 1s | Same request as triggering status update (US-3.06) |
| PERF-35 | Available actions on Embassy row | < 100ms p95 | Reuses Unit 2 engine target |
| PERF-36 | Milestone advance + To Ticket enable | < 500ms p95 | Sequence check + status append |

## NFR-SCALE: Scalability Requirements

| ID | Requirement | Target | Strategy |
|----|-------------|--------|----------|
| SCALE-30 | Concurrent Embassy board users | 30+ per tenant | Indexed stage queries; SWR cache |
| SCALE-31 | Candidates in Embassy at once | 5,000+ | Denormalized status JSONB + stage indexes |
| SCALE-32 | Mirrored LMIS preview rows | 2,000+ concurrent mirrors | VisibleInStages GIN/array query |
| SCALE-33 | Resubmission attempts per candidate | 20+ | Counter in status values; history in events |

## NFR-SEC: Security Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| SEC-30 | Embassy vs Case Executive separation | Distinct permissions (`embassy.view` vs `embassy.case_view`) |
| SEC-31 | Only Case Executives can Submit docs | `embassy.case_submit` on SubmitVisaDocumentation |
| SEC-32 | Only authorized roles set Issued/Rejected | `embassy.visa_outcome` |
| SEC-33 | Rejection reason never logged in clear PII dumps | Structured log fields exclude free-text reason body or redact |
| SEC-34 | LMIS document download audited | Existing document read-audit |
| SEC-35 | Intent APIs re-validate track preconditions server-side | Never trust client “next status” |
| SEC-36 | Office-scoped boards by default | Filter by user’s office unless cross-office permission |

## NFR-RES: Resiliency Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| RES-30 | Status update + mirror activation atomic | Single DB transaction |
| RES-31 | Insurance Paid → Available as ordered events | Two appends in one transaction; rollback both on failure |
| RES-32 | Failed Book Medical leaves prior track unchanged | Transaction rollback |
| RES-33 | SignalR notify failure does not roll back status | Fire-and-forget after commit |
| RES-34 | Seed Case Executive stage idempotent | Seeder skips if definition exists; migration/backfill for existing tenants |

## NFR-TEST: PBT Requirements (Unit-Specific)

| ID | Requirement | PBT Rule | Implementation |
|----|-------------|----------|----------------|
| TEST-30 | Medical/Tasheer independence | PBT-03 | Updating one track never mutates the other |
| TEST-31 | Mirror LMIS iff Fit ∧ Book Done | PBT-03 | After any status seq, VisibleInStages↔condition |
| TEST-32 | Case Executive mirror iff visa ∈ {Ready,Submitted} | PBT-03 | Symmetry with mirror rule |
| TEST-33 | Milestone sequence enforced | PBT-03 | Illegal skips rejected; state unchanged |
| TEST-34 | Rejected requires reason | PBT-04 | Commands without reason always fail validation |
| TEST-35 | Resubmit Rejected→Ready preserves history | PBT-02 | Events still contain prior rejection payload |
| TEST-36 | To LMIS clears Embassy + Case Executive visibility | PBT-03 | After transfer, only LMIS (primary) |
| TEST-37 | Insurance Paid implies Available | PBT-03 | Final insurance track = Available after Paid command |
| TEST-38 | Stateful embassy/LMIS command sequences | PBT-06 | Random Book/Result/Ready/Submit/Outcome/Pay/Milestone |

## NFR-USAB: Usability Requirements (Unit-Specific)

| ID | Requirement | Implementation |
|----|-------------|----------------|
| USAB-30 | Parallel tracks shown side-by-side on Embassy board | Medical + Tasheer columns |
| USAB-31 | Disabled actions explain why | `disabledReason` tooltips |
| USAB-32 | Reject dialog requires reason before submit | Client + server validation |
| USAB-33 | Milestone control offers only next step | Constrained select |
| USAB-34 | Mirror LMIS rows visually distinct | “Mirror” badge |
| USAB-35 | Days-in-stage visible on all three boards | Computed from last StageTransitioned/entry |
| USAB-36 | Success/error toasts on every intent action | sonner + PageAlert pattern from Unit 2 |

## Tech Stack Additions (Unit 3-Specific)

| Package | Purpose |
|---------|---------|
| (None) | Reuses MediatR, FluentValidation, SignalR, SWR, QuestPDF stack from Units 1–2 |

## Testable Properties Summary

Must have FsCheck (and complementary example tests) for:

1. Track independence (medical ≠ tasheer mutation)
2. LMIS mirror symmetry (Fit ∧ Book Done)
3. Case Executive mirror symmetry (Ready|Submitted)
4. Milestone sequential invariant
5. Rejection reason required
6. Resubmit preserves rejection history
7. Full To LMIS visibility cleanup
8. Insurance Paid → Available invariant
9. Stateful random command model for embassy/LMIS intents
