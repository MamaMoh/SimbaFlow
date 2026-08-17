# Domain Entities — Unit 4: Travel, Departure & Arrival

## Design posture

**Decisions** (from `travel-arrival-functional-design-plan.md`):

| Topic | Choice |
|-------|--------|
| Modules | TravelModule + ArrivalModule + ExceptionModule |
| Exceptions | Full `ExceptionCase` + `InvestigationNote` + `LiabilityAssignment` |
| Not Departed | Reason required → Back to Ticket **or** Cancel departure (history on Departure, hidden from countdown) |
| Notify | Mark Notified only (bot Unit 7) |
| Commission | **Shell commission row** on “Add to Commission” (Unit 4); full finance Unit 5 |
| Arrival ledger | **RemoveFromSource=false** — candidate remains on Arrival (Q6 soft-copy rejected) |

Travel/departure/arrival **stage state** continues via Candidate + WorkflowEvent JSONB (Unit 2/3 pattern).  
**New aggregates** only for exception containment + minimal commission shell.

---

## Existing entities reused

| Entity | Unit 4 usage |
|--------|----------------|
| `Candidate` | Stage, status values, visibility; denormalized destination / flight_date / ticket fields in status JSON |
| `WorkflowEvent` | Book ticket, notify, departed, not-departed+reason, arrival confirm |
| `WorkflowStage` | Ticket, Departure, Arrival, Commission (seeded) |
| `WorkflowTransitionRule` | To Departure, To Arrival, Back to Ticket, Add to Commission |
| `CandidateDocument` | Ticket / travel docs if uploaded |

---

## Status tracks (seeded / extended)

### Ticket
| Track / field | Values / notes |
|---------------|----------------|
| `ticket_status` | Pending → Booking Complete |
| `destination` | Required field (string / country) |
| `flight_date` | Required field (date) — also used for Remaining Days |

### Departure
| Track | Values |
|-------|--------|
| `notification_status` | Awaiting → Notified |
| `departure_status` | (empty) → Departed \| Not Departed |
| `departure_outcome` | (when Not Departed path) Rebooked \| Canceled |
| `non_departure_reason` | MissedFlight \| Immigration \| Medical \| CandidateNoShow \| AirlineCancel \| Other |
| `canceled` | true when outcome = Canceled (countdown filter excludes these) |
| `notified_at` | ISO timestamp (metadata) |
| `departed_at` | ISO timestamp (metadata) |

### Arrival
| Status | Notes |
|--------|--------|
| Pending | Entered Arrival |
| Arrived | Confirmed safe arrival |
| Returned | Flags exception (opens ExceptionCase) |
| Runaway | Flags exception (opens ExceptionCase) |

---

## New entity: ExceptionCase

```
ExceptionCase : BaseEntity (tenant schema)
├── CandidateId : Guid (required)
├── Type : ExceptionType (Returned | Runaway)
├── Status : ExceptionStatus (Open | UnderInvestigation | Resolved | Closed)
├── OpenedAt : DateTime
├── OpenedByUserId : Guid
├── ClosedAt : DateTime?
├── ResolutionSummary : string?
├── FinancialImpactAmount : decimal?
├── FinancialImpactCurrency : string? (default ETB)
└── Notes : InvestigationNote[]
└── Liabilities : LiabilityAssignment[]
```

**Rule:** Flagging Returned/Runaway on Arrival **creates** an ExceptionCase (one open case per candidate at a time preferred; reopen allowed after Closed).

---

## New entity: InvestigationNote

```
InvestigationNote : BaseEntity
├── ExceptionCaseId : Guid
├── AuthorUserId : Guid
├── Body : string (required)
├── CreatedAt : DateTime
└── AttachmentDocumentIds : Guid[]? (optional refs to CandidateDocument)
```

---

## New entity: LiabilityAssignment

```
LiabilityAssignment : BaseEntity
├── ExceptionCaseId : Guid
├── Party : LiabilityParty (Agency | Partner | Candidate | Other)
├── Amount : decimal
├── Currency : string
├── Notes : string?
└── AssignedAt : DateTime
```

Unit 5 may post journal adjustments from these; Unit 4 only stores the assignment.

---

## New entity: Commission (shell — Unit 4 minimal)

```
Commission : BaseEntity (tenant schema)
├── CandidateId : Guid (required, unique active shell per candidate for v1)
├── Status : CommissionStatus (Open)  // Unit 5 extends: Partial, Settled, Disputed
├── CountryOfTravel : string? (snapshot)
├── OfficeName : string? (partner snapshot)
├── ContractDate : DateOnly?
├── OpenedAt : DateTime
├── OpenedByUserId : Guid
└── // Unit 5 adds: fees, payments, balances
```

Created when **Add to Commission** succeeds. Candidate **remains** on Arrival (`RemoveFromSource=false`).

---

## Enums

```
ExceptionType: Returned | Runaway
ExceptionStatus: Open | UnderInvestigation | Resolved | Closed
LiabilityParty: Agency | Partner | Candidate | Other
CommissionStatus: Open | … (Unit 5)
NonDepartureReason: MissedFlight | Immigration | Medical | CandidateNoShow | AirlineCancel | Other
```

---

## Visibility model

```
Ticket → Departure (RemoveFromSource=true)
Departure → Arrival when Departed (RemoveFromSource=true)
Departure → Ticket when Not Departed + Back to Ticket (RemoveFromSource=true)
Departure + Canceled: stay CurrentStage=Departure; countdown query excludes canceled=true
Arrival + Add to Commission: RemoveFromSource=false → Arrival ledger permanent; also visible in Commission
```
