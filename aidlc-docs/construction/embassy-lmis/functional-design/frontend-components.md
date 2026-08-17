# Frontend Components — Unit 3: Embassy & LMIS Processing

## Navigation

Add (or promote from generic `/workflow/[stageId]`) named routes:

| Route | Page | Permission |
|-------|------|------------|
| `/workflow/embassy` | Embassy View | `embassy.view` |
| `/workflow/case-executive` | Case Executive View | `embassy.case_view` |
| `/workflow/lmis` | LMIS View | `lmis.view` |

Generic `/workflow/[stageId]` remains for other stages. Named routes resolve stage by slug/name and apply **stage-specific columns and action sheets**.

---

## Pages

### Embassy View (`/workflow/embassy`)

- **Component**: `EmbassyBoardPage`
- **Table columns**: Name, Passport, Office, Medical, Tasheer, Visa, Days in stage, Actions
- **Row badges**: Unfit, Expired, Mirror→LMIS (when visible in LMIS)
- **Inline / sheet actions**:
  - Book Medical (date + facility dialog)
  - Record Medical Result (Fit / Unfit)
  - Book Tasheer (date dialog)
  - Record Tasheer Result (Book Done / Expired)
  - Set Ready (when clearances complete)
  - Record Visa Outcome (Issued / Rejected + reason)
  - Resubmit (when Rejected)
  - To LMIS (from available actions when Issued)
- **Messaging**: `PageAlert` for AccessDenied / LoadError; sonner toasts on success/failure
- **Realtime**: invalidate SWR on SignalR stage/status events (wire client subscribe if not yet done)

### Case Executive View (`/workflow/case-executive`)

- **Component**: `CaseExecutiveBoardPage`
- **Data**: candidates with Case Executive in `VisibleInStages` (visa Ready|Submitted)
- **Columns**: Name, Passport, Visa status, Submission date/ref, Days waiting, Actions
- **Actions**: Submit documentation (date + optional reference)
- **Read-only** for medical/tasheer (show as context chips only)

### LMIS View (`/workflow/lmis`)

- **Component**: `LmisBoardPage`
- **Columns**: Name, Passport, Insurance, Milestone, Days in stage, Source (Primary \| Mirror), Actions
- **Filters**: insurance, milestone, office, mirror-only toggle
- **Actions**:
  - Mark Insurance Paid (payment date)
  - Advance Milestone (next valid only)
  - Upload LMIS Document
  - To Ticket (when Issued)
- **Mirror rows**: editable for LMIS fields while still in Embassy primary stage (same candidate)

---

## Shared / reused components

| Component | Unit 3 use |
|-----------|------------|
| `WorkflowViewTable` | Base table; Embassy/LMIS supply column defs |
| `ActionButtonBar` | Transition actions (To LMIS, To Ticket, To Embassy) |
| `CandidateStatusBadge` | Track chips (medical/tasheer/visa/insurance/milestone) |
| `DocumentUploader` / `DocumentList` | LMIS docs with `documentType=LMIS` |
| `PageAlert` / sonner | Errors and feedback |
| New: `StatusUpdateSheet` | Form dialog for Book Medical / Submit / Reject reason |
| New: `TrackStatusSelect` | Constrained next-status picker per track |

---

## API client hooks

Extend or add:

```
lib/api/embassy.ts
  useEmbassyBoard()
  bookMedical()
  recordMedicalResult()
  bookTasheer()
  recordTasheerResult()
  setVisaReady()
  submitVisaDocs()
  recordVisaOutcome()
  resubmitVisa()

lib/api/lmis.ts
  useLmisBoard()
  recordInsurancePaid()
  advanceMilestone()
```

Transitions continue via existing `lib/api/workflow.ts` (`executeTransition`, `getAvailableActions`).

---

## UX rules

1. Disabled actions show server `disabledReason` in tooltip.
2. Rejected visa: reason field mandatory before submit.
3. Milestone control only offers the **next** allowed value.
4. Mirror LMIS rows visually distinct (subtle badge, not a second card pattern beyond existing table).
5. Empty boards use `PageAlert` empty state, not blank tables.

---

## Out of scope UI (later units)

- Ticket / Departure / Arrival boards — Unit 4 (To Ticket may navigate to Ticket board stub if page exists)
- Finance commission from Arrival — Unit 5
- Bot notifications content — Unit 8 (SignalR in-app only here)
