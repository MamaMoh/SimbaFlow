# Business Logic Model — Unit 3: Embassy & LMIS Processing

## Architecture note

All mutations go through `IWorkflowEngineService` (Unit 2). Embassy/Lmis modules provide:

1. Intent-named commands (BookMedical, RecordVisaOutcome, …)
2. Stage-board queries with column projections
3. Domain side-effects after status updates (insurance → Available, mirror re-eval)

```
EmbassyModule / LmisModule
        │
        ▼
   MediatR Handlers
        │
        ▼
 IWorkflowEngineService.UpdateStatus / ExecuteTransition / GetAvailableActions
        │
        ▼
 TenantDbContext (Candidate + WorkflowEvent)
```

---

## BL-E01: Transfer to Embassy (US-3.01)

```
ExecuteTransition("To Embassy")
  1. Preconditions (engine): CurrentStage = New Contracts; status Ready if configured
  2. Append StageTransitioned (RemoveFromSource = true)
  3. Initialize Embassy track defaults if missing:
     medical=Pending, tasheer=Pending
  4. Emit CandidateStageChanged
  5. SignalR broadcast stage board refresh
```

---

## BL-E02: Book Medical (US-3.02)

```
BookMedicalCommand(candidateId, appointmentDate, facilityName)
  1. Assert CurrentStage = Embassy (or visible in Embassy)
  2. Assert medical ∈ {Pending, Expired-retry allowed later}
  3. UpdateStatus(track=medical, to=Booked, data={appointmentDate, facilityName})
  4. Denormalize medical_appointment_date, medical_facility into CurrentStatusValues
  5. Re-evaluate mirrors (no-op until Fit+Book Done)
  6. Emit CandidateStatusChanged
```

---

## BL-E03: Record Medical Result (US-3.03)

```
RecordMedicalResultCommand(candidateId, result: Fit|Unfit)
  1. Assert medical = Booked
  2. UpdateStatus(track=medical, to=result)
  3. If Fit AND tasheer=Book Done → engine activates LMIS mirror (BL-E06)
  4. If Unfit → no mirror; board shows Unfit badge; no auto-block of tasheer track
```

---

## BL-E04: Book Tasheer (US-3.04)

```
BookTasheerCommand(candidateId, appointmentDate)
  1. Assert Embassy visibility
  2. UpdateStatus(track=tasheer, to=Booked, data={appointmentDate})
  3. Denormalize tasheer_appointment_date
```

---

## BL-E05: Record Tasheer Result (US-3.05)

```
RecordTasheerResultCommand(candidateId, result: Book Done|Expired)
  1. Assert tasheer = Booked
  2. UpdateStatus(track=tasheer, to=result)
  3. If Book Done AND medical=Fit → activate LMIS mirror
  4. If Expired → keep Embassy; UI offers re-book (status back to Pending via BookTasheer)
```

---

## BL-E06: Mirror View — Embassy → LMIS (US-3.06)

```
After any StatusUpdated on medical or tasheer:
  1. Engine evaluates MirrorViewRules on Embassy stage
  2. Condition: medical=Fit AND tasheer=Book Done
  3. If met and not already visible → MirrorViewActivated → VisibleInStages += LMIS
     CurrentStageId remains Embassy
  4. If no longer met → MirrorViewDeactivated → remove LMIS from VisibleInStages
     (only while CurrentStage is still Embassy — full transfer uses stage change)
  5. Latency target: same request transaction (< 1s)
```

---

## BL-E07: Set Ready → Case Executive mirror (US-3.07)

```
SetVisaReadyCommand(candidateId)
  1. Assert medical=Fit AND tasheer=Book Done (clearances complete)
  2. UpdateStatus(track=visa, to=Ready)
  3. Engine activates Case Executive mirror rule (visa in {Ready, Submitted})
  4. SignalR notify users with case_executive role / embassy.case_exec permission
```

---

## BL-E08: Case Executive submits docs (US-3.08)

```
SubmitVisaDocumentationCommand(candidateId, submissionDate?, referenceNumber?)
  1. Assert user has Case Executive permission
  2. Assert visa = Ready
  3. UpdateStatus(track=visa, to=Submitted, data={submissionDate, referenceNumber})
  4. Case Executive mirror remains active
  5. Notify Embassy Officers (SignalR)
```

---

## BL-E09: Record Visa Outcome (US-3.09)

```
RecordVisaOutcomeCommand(candidateId, outcome: Issued|Rejected, rejectionReason?)
  1. Assert visa = Submitted
  2. If Rejected → rejectionReason required
  3. UpdateStatus(track=visa, to=outcome)
  4. If Issued → available actions include "To LMIS" (transition rule condition)
  5. If Rejected → store visa_rejection_reason; expose "Resubmit" action
```

---

## BL-E10: Full transfer To LMIS (US-3.10)

```
ExecuteTransition("To LMIS")
  1. Preconditions: CurrentStage=Embassy, visa=Issued
  2. StageTransitioned to LMIS with RemoveFromSource=true
  3. Clear VisibleInStages entries for Embassy and Case Executive
  4. Ensure VisibleInStages includes LMIS as primary (CurrentStageId=LMIS)
  5. Initialize insurance=Insurance Unpaid if unset
  6. Emit CandidateStageChanged
```

---

## BL-E11: Visa rejection resubmit (US-3.11)

```
ResubmitVisaCommand(candidateId)
  1. Assert visa = Rejected
  2. Increment visa_resubmission_count
  3. Preserve prior rejection in event history (append-only)
  4. UpdateStatus(track=visa, to=Ready, action=Resubmit)
  5. Re-activate Case Executive mirror; notify Case Executives
```

---

## BL-L01: LMIS queue query (US-4.01)

```
GetLmisBoardQuery(officeId?, insurance?, milestone?, page)
  1. Candidates where CurrentStageId=LMIS OR VisibleInStages contains LMIS
  2. Project: name, passport, insurance, milestone, daysInStage, office
  3. Include mirror arrivals (still CurrentStage=Embassy) with badge "Mirror"
  4. Office-scoped unless user has cross-office permission
```

---

## BL-L02: Insurance payment (US-4.02)

```
RecordInsurancePaidCommand(candidateId, paymentDate)
  1. Assert LMIS visibility
  2. UpdateStatus(insurance → Insurance Paid, data={paymentDate})
  3. Side-effect in same transaction:
     UpdateStatus(insurance → Available)  // or set denormalized Available directly
  4. Emit status changed once (or two events — prefer two for audit clarity)
```

**Decision**: Prefer **two append events** (Paid then Available) for audit trail; UI shows final Available.

---

## BL-L03: Upload LMIS documents (US-4.03)

```
UploadLmisDocumentCommand → reuse Candidate document upload
  DocumentType = LMIS
  Linked to candidateId; appears on LMIS board document panel
```

No separate LMIS document aggregate.

---

## BL-L04: Milestone progression (US-4.04)

```
AdvanceLmisMilestoneCommand(candidateId, nextMilestone)
  1. Assert insurance = Available (operational gate)
  2. Enforce sequential map:
     (empty|null) → Uploaded
     Uploaded → Check Verified
     Check Verified → Issued
  3. Reject skips (e.g. empty → Issued)
  4. When Issued → "To Ticket" appears in available actions
```

---

## BL-L05: Transfer to Ticket (US-4.05)

```
ExecuteTransition("To Ticket")
  1. Preconditions: CurrentStage=LMIS (or primary LMIS after full transfer), milestone=Issued
  2. RemoveFromSource=true → leave LMIS board
  3. CurrentStageId = Ticket
  4. Note: Ticket board UX is Unit 4; transition must work end-to-end here
```

---

## BL-E12: Available actions (Embassy / LMIS)

Uses Unit 2 `GetAvailableActions` with seeded rules. Unit 3 adds **intent helpers** that map UI buttons to UpdateStatus when the action is a status change (not a stage transition):

| UI action | Mechanism |
|-----------|-----------|
| To Embassy / To LMIS / To Ticket | `ExecuteTransition` |
| Book Medical / Book Tasheer | Intent command → UpdateStatus |
| Fit / Unfit / Book Done / Expired | Intent command → UpdateStatus |
| Set Ready / Submit / Issued / Rejected / Resubmit | Intent command → UpdateStatus |
| Mark Insurance Paid | Intent command → UpdateStatus + side-effect |
| Advance Milestone | Intent command → UpdateStatus with sequence check |

---

## Error model

All handlers return `Result<T>` with:

- `NotFound` — candidate missing
- `Validation` — wrong track state, missing rejection reason, milestone skip
- `Forbidden` — role/permission
- `Conflict` — concurrency (RowVersion)
