# Business Rules — Unit 3: Embassy & LMIS Processing

## BR-E01: Stage entry

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-E01.1 | "To Embassy" only from New Contracts (or configured source) | TransitionRule.SourceStageId |
| BR-E01.2 | Transfer removes candidate from source board | RemoveFromSource=true |
| BR-E01.3 | On Embassy entry, medical and tasheer default to Pending if unset | Handler post-transition init |

## BR-E02: Parallel tracks (Medical + Tasheer)

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-E02.1 | Medical and Tasheer progress independently | Separate CurrentStatusValues keys |
| BR-E02.2 | Book Medical requires Pending (or re-book after Expired on tasheer path only for tasheer) | Command pre-check |
| BR-E02.3 | Medical result only from Booked → Fit\|Unfit | Command pre-check |
| BR-E02.4 | Tasheer result only from Booked → Book Done\|Expired | Command pre-check |
| BR-E02.5 | Appointment date required on Book Medical / Book Tasheer | FluentValidation |
| BR-E02.6 | Facility name required on Book Medical | FluentValidation |

## BR-E03: Mirror — LMIS preview

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-E03.1 | Mirror when medical=Fit AND tasheer=Book Done | MirrorViewRule on Embassy |
| BR-E03.2 | Single Candidate row — no clone | VisibleInStages only |
| BR-E03.3 | Candidate remains operable in Embassy while mirrored | CurrentStageId stays Embassy |
| BR-E03.4 | Mirror re-evaluated on every relevant status update | Engine post-status hook |
| BR-E03.5 | Full "To LMIS" is distinct from mirror (changes CurrentStageId) | TransitionRule |

## BR-E04: Case Executive handoff

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-E04.1 | Ready requires clearances complete (Fit + Book Done) | SetVisaReadyCommand |
| BR-E04.2 | Case Executive board shows candidates with visa Ready\|Submitted | Mirror rule / query filter |
| BR-E04.3 | Only Case Executive role can set Submitted | Permission `embassy.case_submit` |
| BR-E04.4 | Embassy Officers record Issued/Rejected | Permission `embassy.visa_outcome` |
| BR-E04.5 | Submitted requires prior Ready | Command pre-check |
| BR-E04.6 | Issued/Rejected requires prior Submitted | Command pre-check |

## BR-E05: Visa rejection & resubmit

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-E05.1 | Rejection reason required when outcome=Rejected | FluentValidation |
| BR-E05.2 | Resubmit only from Rejected → Ready | Command pre-check |
| BR-E05.3 | Prior rejection preserved in event stream | Append-only events |
| BR-E05.4 | Resubmission attempt counter increments on each Resubmit | Denormalized count + event data |
| BR-E05.5 | Resubmit re-notifies Case Executives | SignalR after status update |

## BR-E06: Full transfer To LMIS

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-E06.1 | "To LMIS" enabled only when visa=Issued | Transition conditions |
| BR-E06.2 | Transfer removes Embassy and Case Executive visibility | RemoveFromSource + clear mirrors |
| BR-E06.3 | CurrentStage becomes LMIS | Engine transition |
| BR-E06.4 | Insurance defaults to Insurance Unpaid if unset | Post-transition init |

## BR-L01: LMIS insurance

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-L01.1 | Insurance Paid requires LMIS visibility (primary or mirror) | Command pre-check |
| BR-L01.2 | Paid automatically advances operational status to Available | Side-effect (two events) |
| BR-L01.3 | Payment date recorded | Event Data + denormalized key |

## BR-L02: LMIS milestones

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-L02.1 | Milestone advances only when insurance=Available | Command pre-check |
| BR-L02.2 | Sequence only: Uploaded → Check Verified → Issued | Allowed-next map |
| BR-L02.3 | Skipping milestones rejected | Validation error |
| BR-L02.4 | "To Ticket" only when milestone=Issued | Transition conditions |

## BR-L03: LMIS documents

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-L03.1 | LMIS uploads use DocumentType.LMIS | Upload API |
| BR-L03.2 | Same file storage + audit as other candidate docs | Unit 1/2 reuse |

## BR-E07: Permissions (Unit 3 codes)

| Permission | Purpose |
|------------|---------|
| `embassy.view` | Embassy board |
| `embassy.update` | Medical/Tasheer/Ready/outcome |
| `embassy.case_view` | Case Executive board |
| `embassy.case_submit` | Mark Submitted |
| `embassy.visa_outcome` | Issued/Rejected/Resubmit |
| `lmis.view` | LMIS board |
| `lmis.update` | Insurance, milestones |
| `lmis.document` | Upload LMIS docs |
| `workflow.execute` | Stage transitions (To Embassy/LMIS/Ticket) |

Agencies map these onto their custom roles (Unit 1 RBAC). Default seed roles (optional in Unit 3): Embassy Officer, Case Executive, Office Manager.

## BR-E08: Audit & realtime

| Rule ID | Rule | Enforcement |
|---------|------|-------------|
| BR-E08.1 | Every status/transition appends WorkflowEvent | Engine |
| BR-E08.2 | SignalR broadcasts stage/status changes | Existing handlers |
| BR-E08.3 | Platform audit log for privileged actions | Existing audit pipeline |
