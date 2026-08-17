# Functional Design Plan — Unit 4: Travel, Departure & Arrival

## Unit Context
- **Unit**: Travel, Departure & Arrival (Unit 4)
- **Stories**: US-5.01–US-5.08, US-6.01–US-6.07
- **Dependencies**: Unit 2 engine, Unit 3 (To Ticket)
- **Existing seed**: Ticket / Departure / Arrival stages + transitions already in `WorkflowSeeder`

## Plan

- [x] Step 1: Confirm architecture posture vs Unit 3 (intent modules + engine)
- [x] Step 2: Resolve exception containment model (new aggregates vs status-only)
- [x] Step 3: Resolve notification / Canceled / Commission handoff scope
- [x] Step 4: Generate domain-entities, business-logic-model, business-rules, frontend-components
- [x] Step 5: Functional design approval questions

**Artifacts**: `construction/travel-arrival/functional-design/`  
**Approval**: `construction/travel-arrival/functional-design-approval-questions.md`

---

## Clarifying Questions

### Question 1 — Module shape
How should Unit 4 APIs be structured?

A) **TravelModule + ArrivalModule + ExceptionModule** as in Unit of Work (recommended — mirrors Embassy/Lmis split)

B) **Single TravelModule** covering Ticket + Departure + Arrival; ExceptionModule separate

C) **Reuse WorkflowModule only** — no intent modules; generic stage boards + status updates

D) Other (please describe after [Answer]: tag below)

[Answer]: A

### Question 2 — Exception containment data model
Unit of Work lists `ExceptionCase`, `InvestigationNote`, `LiabilityAssignment`. How deep for Unit 4?

A) **Full entities** in tenant schema (recommended for US-6.04–6.05 investigation workspace)

B) **Status-only on Arrival** (Returned/Runaway) + notes in WorkflowEvent JSON — defer investigation entities to later

C) **ExceptionCase only** (notes/liability as JSON columns on the case) — no separate note/liability tables yet

D) Other (please describe after [Answer]: tag below)

[Answer]:A

### Question 3 — “Canceled” after Not Departed
Seeder has Back to Ticket when Not Departed. Stories also mention Canceled. What should Canceled do?

A) **Status on Departure** (`canceled=true` / track) — candidate stays on Departure board as dead-end (no To Arrival)

B) **Transition to a terminal stage** or deactivate candidate (`CandidateStatus.Inactive`) with reason

C) **Same as Back to Ticket** for v1 — skip distinct Canceled action

D) Other (please describe after [Answer]: tag below)

[Answer]:D   Departure
  → mark “Not Departed”
  → required reason (e.g. Missed flight | Immigration | Medical | Candidate no-show | Airline cancel | Other)
  → then choose outcome:
        1) Back to Ticket   → rebook (most common)
        2) Cancel departure → this trip is closed; stay on Departure as history (hidden from countdown)

### Question 4 — Notify candidate (US-5.05)
Bot push is Unit 7. For Unit 4 notify action:

A) **Mark Notified only** (status + timestamp + SignalR); bot send is no-op / queued stub until Unit 7

B) **Require** Telegram/WhatsApp integration in Unit 4

C) **Optional**: if bot linked, attempt send; always allow manual Notified mark

D) Other (please describe after [Answer]: tag below)

[Answer]:A

### Question 5 — Add to Commission (US-6.06)
Arrival → Commission:

A) **Transition only** (`Add to Commission` with RemoveFromSource=false) — Commission board shows visible candidates; Commission **record** created in Unit 5

B) **Create Commission shell row** in Unit 4 (minimal fields) + keep on Arrival ledger

C) **Defer button** until Unit 5 — Arrival confirms Arrived only

D) Other (please describe after [Answer]: tag below)

[Answer]: B

### Question 6 — Permanent Arrival ledger
How should “never leave Arrival” work with Add to Commission?

A) **RemoveFromSource=false** on Add to Commission (already seeded) — Arrival stays primary or stays in VisibleInStages (recommended)

B) Arrival becomes mirror; Commission is primary

C) Soft-copy: duplicate display row (reject — conflicts with single-candidate model)

D) Other (please describe after [Answer]: tag below)

[Answer]: A
*(File previously had C; soft-copy rejected — confirmed as A in FD artifacts + approval Q2)*
