# Partner Management — capacity, agreement expiry, placement tracking, billing

Partners are the **foreign receiving agencies** an Ethiopian agency signs agreements (ትስስር) with.
Regulatory source: MoLS Directive 1126/2018 — see
`aidlc-docs/inception/requirements/partner-agency-and-tenant-licensing.md`.

## What already existed
`AgencyLevelRules` (level 1–5 caps + Art. 40 tiers), `TenantInfo` licence fields, the
`PartnerAgency` catalog / `PartnerLink` split, and cap enforcement on link create.

## Compliance holes found and fixed
1. **Expired agreements were offered at intake.** `/api/partners/links/mine` and
   `?linkedOnly=true` returned every link, and the linked list filtered only on *status* — a link
   whose `AgreementEnd` had passed still looked Active. The directive requires expired partners to be
   hidden from intake.
2. **Intake never validated the partner at all.** `RegisterCandidateCommand` stored whatever
   `PartnerAgencyId` it was given — a candidate could be attached to a catalog partner the agency has
   **no agreement with**, or to a lapsed one.

## Implementation

### Backend
- **`Domain/Services/PartnerAgreementRules.cs`** — single source of truth for expiry:
  `Evaluate(start, end, status, today)` → `Active | ExpiringSoon (≤60d) | Expired | NotStarted |
  Suspended`, plus `DaysRemaining`, `IsUsableForIntake`, `Describe`.
- **`Features/Partners/PartnerLinkValidator.cs`** — shared guard used by
  `RegisterCandidateCommand` and `UpdateCandidateCommand` (update only re-checks when the partner
  actually changes, so an existing candidate stays editable after an agreement lapses). Returns
  plain-language errors ("Your agency has no agreement with the selected partner…").
- **`GET /api/partners/links/mine`** and **`?linkedOnly=true`** — now return computed
  `agreementState`, `daysRemaining`, `agreementLabel`, `isUsable`, and accept `usableOnly=true`.
  The Partners **page** deliberately shows expired links (so they can be renewed); **intake** passes
  `usableOnly=true`.
- **`GET /api/partners/capacity`** — agency level, per-country used/max/remaining, licensed
  countries used/max.
- **`GET /api/partners/{id}/candidates`** — who this agency placed through that partner
  ("where did the person go"), tenant-scoped.
- **`GET /api/partners/{id}/billing`** — commission rollup **derived by joining commissions →
  candidates on `PartnerAgencyId`** (decision: no denormalized column, so it can't drift):
  fees / collected / outstanding + per-status breakdown.
- **Compliance centre** — `GetComplianceAlertsQuery` now also raises **Partner agreement** and
  **Agency licence** expiry into the existing expired/≤30/≤90 buckets, so `/compliance` and
  `/my-work` pick them up automatically.

### Frontend
- `/partners` — agreement **StatusBadge** (Expired=danger, ≤60d=warning, Active=success) with the
  date range beneath, partner name links to detail, and a **capacity strip**
  (`components/partners/capacity-strip.tsx`) showing e.g. "Qatar 1/4" plus the level description.
- **New `/partners/[id]`** — header + fees/collected/outstanding tiles, and **Candidates** /
  **Billing** tabs using the standard card + `indexColumn` + `NameCell`.
- Intake partner dropdown now requests `usableOnly=true`.

## Verification
- Backend **162/162** (16 new `PartnerAgreementRulesTests` covering boundaries: ends today, ±1 day,
  60-day edge, Suspended/Expired status precedence, and the directive's cap tables).
- Live, against the Demo Agency tenant seeded with an expired (−10d), expiring (+20d) and active
  (+700d) agreement plus a licence expiring in 45 days:
  - `links/mine?usableOnly=true` hides the expired partner; the Partners page still shows it.
  - Registering against the **expired** partner → **400** with a renew message.
  - Registering against a **catalog partner with no link** → **400**.
  - Registering against a **usable** partner → **201**.
  - `/compliance/alerts` lists the expired agreement, the 20-day one, and the licence.
  - `/partners/{id}/candidates` and `/billing` return the right rows.
  - **Cross-tenant check:** Ethio Star's owner gets 0 candidates for the same partner (no leak).
- Playwright **49/49** including the new `e2e/partners.spec.ts`.

## Notes
- The partner list column reads "Partner / Office" on the workflow boards while
  `Candidate.OfficeName` actually holds the **partner** name (legacy AppSheet naming). Renaming that
  column to "Partner" is still outstanding.
- Demo data left in place for exploration: three Demo Agency partner links + licence
  `MoLS-DEMO-001`, and candidate *Marta Bekele* placed through Qatar Domestic Services.
