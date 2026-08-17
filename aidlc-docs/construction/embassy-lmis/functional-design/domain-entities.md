# Domain Entities — Unit 3: Embassy & LMIS Processing

## Design posture

Unit 3 **does not introduce new aggregate roots**. Embassy and LMIS state lives in:

- `Candidate.CurrentStatusValues` (denormalized track statuses)
- `Candidate.VisibleInStages` (mirror visibility)
- Append-only `WorkflowEvent` rows with typed JSON `Data` payloads
- Seeded `WorkflowDefinition` (stages, parallel tracks, mirror rules, transitions)

New artifacts in this unit are **payload schemas**, **stage view DTOs**, **side-effect rules**, and **Carter modules** that wrap the Unit 2 engine with domain language.

---

## Existing entities reused (from Unit 2)

| Entity | Unit 3 usage |
|--------|----------------|
| `Candidate` | Primary record; denormalized stage/status/visibility |
| `CandidateDocument` | LMIS document uploads (`DocumentType.LMIS`) |
| `WorkflowEvent` | Medical/Tasheer bookings, visa outcomes, insurance, milestones |
| `WorkflowSnapshot` | Replay performance unchanged |
| `WorkflowStage` | Embassy, Case Executive (new), LMIS |
| `WorkflowStageStatus` | Track-scoped statuses already seeded |
| `ParallelTrackDefinition` | medical + tasheer on Embassy |
| `MirrorViewRule` | Embassy→LMIS (Fit+Book Done); Embassy→Case Executive (Ready) |
| `WorkflowTransitionRule` | To Embassy, To LMIS, To Ticket, Resubmit |

---

## New logical stage: Case Executive

**Not a primary lifecycle stage.** Candidates never have `CurrentStageId = Case Executive`.

```
WorkflowStage "Case Executive"
├── StageType: Simple (mirror-only board)
├── SortOrder: 3.5 (between Embassy and LMIS in nav)
├── IsInitialStage: false
├── IsFinalStage: false
└── Purpose: filtered board for Case Executives while CurrentStage = Embassy
```

**Activation** (mirror rule on Embassy):

```
WHEN visa == "Ready" OR visa == "Submitted"
THEN add Case Executive stage Id to Candidate.VisibleInStages
```

**Deactivation**:

```
WHEN CurrentStage leaves Embassy (e.g. "To LMIS" with RemoveFromSource)
  OR visa resets on Resubmit before Ready
THEN remove Case Executive from VisibleInStages
```

---

## Status tracks (canonical keys)

### Embassy stage

| Track key | Values (progression) | Notes |
|-----------|----------------------|-------|
| `medical` | Pending → Booked → Fit \| Unfit | Parallel with tasheer |
| `tasheer` | Pending → Booked → Book Done \| Expired | Parallel with medical |
| `visa` | (empty) → Ready → Submitted → Issued \| Rejected | Sequential after clearances |

### LMIS stage

| Track key | Values | Notes |
|-----------|--------|-------|
| `insurance` | Insurance Unpaid → Insurance Paid → Available | Paid auto-promotes to Available (BR-E07) |
| `milestone` | (empty) → Uploaded → Check Verified → Issued | Sequential; cannot skip |

---

## Event payload schemas (`WorkflowEvent.Data`)

### `StatusUpdated` — medical / tasheer booking

```json
{
  "track": "medical",
  "from": "Pending",
  "to": "Booked",
  "appointmentDate": "2026-08-01",
  "facilityName": "Addis Medical Center"
}
```

```json
{
  "track": "tasheer",
  "from": "Pending",
  "to": "Booked",
  "appointmentDate": "2026-08-03"
}
```

### `StatusUpdated` — medical / tasheer result

```json
{ "track": "medical", "from": "Booked", "to": "Fit" }
```

```json
{ "track": "tasheer", "from": "Booked", "to": "Book Done" }
```

### `StatusUpdated` — visa

```json
{
  "track": "visa",
  "from": "Submitted",
  "to": "Issued"
}
```

```json
{
  "track": "visa",
  "from": "Submitted",
  "to": "Rejected",
  "rejectionReason": "Incomplete documentation",
  "resubmissionAttempt": 1
}
```

### `StatusUpdated` — LMIS insurance / milestone

```json
{
  "track": "insurance",
  "from": "Insurance Unpaid",
  "to": "Insurance Paid",
  "paymentDate": "2026-08-10"
}
```

```json
{
  "track": "milestone",
  "from": "Uploaded",
  "to": "Check Verified"
}
```

### `MirrorViewActivated` / `MirrorViewDeactivated`

```json
{
  "sourceStageId": "...",
  "targetStageId": "...",
  "reason": "medical=Fit AND tasheer=Book Done"
}
```

### `StageTransitioned` — full transfers

Unchanged from Unit 2 (`fromStage`, `toStage`, `transitionRuleId`). Used for:

- New Contracts → Embassy (`To Embassy`)
- Embassy → LMIS (`To LMIS`, `RemoveFromSource = true` — clears Embassy + Case Executive visibility)
- LMIS → Ticket (`To Ticket`)

### `StatusUpdated` — visa resubmit

```json
{
  "track": "visa",
  "from": "Rejected",
  "to": "Ready",
  "action": "Resubmit",
  "preservedRejectionReason": "...",
  "resubmissionAttempt": 2
}
```

---

## Optional denormalized appointment fields

Store latest appointment metadata on candidate for board columns (avoid replaying events for list views):

```
Candidate.CurrentStatusValues (extended keys, string values):
  medical, tasheer, visa, insurance, milestone   // existing
  medical_appointment_date, medical_facility     // Unit 3
  tasheer_appointment_date                       // Unit 3
  insurance_payment_date                         // Unit 3
  visa_rejection_reason                          // Unit 3
  visa_resubmission_count                        // Unit 3 (numeric as string)
```

No new DB columns required if JSONB `CurrentStatusValues` already holds these keys.

---

## Entities explicitly out of scope (later units)

| Concern | Unit |
|---------|------|
| Ticket / flight fields | Unit 4 |
| ExceptionCase / Returned / Runaway workspace | Unit 4 |
| Commission fee ledger | Unit 5 |
| Government LMIS API client | Future (FR-04.5 readiness only) |
