# Domain Entities — Unit 5: Finance & Commission

## Design posture (approved)

| Topic | Choice |
|-------|--------|
| Scope | **Commission-first** — fees, payments, disputes, queue; journals on payment; defer CoA UI / bank recon / statements / tax UI |
| Shell | **Extend** Unit 4 `Commission` entity |
| Journals | **Always** post `JournalEntry` + lines on payment (seeded default CoA) |
| FX | **ETB primary** + `ExchangeRate` table; payment currency + rate-to-ETB |
| Modules | `CommissionModule` + `AccountingModule` |
| Init | Unit 4 shell remains source of truth — no auto-create on Arrived |

---

## Extended: Commission (from Unit 4)

```
Commission : BaseEntity (tenant)
├── CandidateId : Guid (unique when not deleted)
├── Status : CommissionStatus  // Open | Partial | Settled | Disputed
├── CountryOfTravel, OfficeName, ContractDate  // snapshots (Unit 4)
├── OpenedAt, OpenedByUserId
├── TotalFeesAmount : decimal?          // denormalized Σ fees (ETB)
├── TotalPaidAmount : decimal?          // denormalized Σ payments (ETB)
├── BalanceAmount : decimal?            // fees − paid (ETB)
├── Fees : CommissionFee[]
├── Payments : Payment[]
└── Disputes : Dispute[]
```

**Status transitions**
- `Open` → fees added, no payments yet (or balance = fees)
- `Partial` → 0 < paid < fees
- `Settled` → paid ≥ fees (and no open dispute)
- `Disputed` → open Dispute exists (may coexist with Partial)

---

## New: CommissionFee

```
CommissionFee : BaseEntity
├── CommissionId : Guid
├── FeeType : FeeType  // AgencyFee | PartnerShare | Medical | Ticket | Other
├── Description : string?
├── Amount : decimal
├── Currency : string  // usually ETB
├── AmountEtb : decimal  // converted if needed
└── SortOrder : int
```

---

## New: Payment

```
Payment : BaseEntity
├── CommissionId : Guid
├── Amount : decimal
├── Currency : string
├── ExchangeRateToEtb : decimal  // 1 if ETB
├── AmountEtb : decimal
├── PaidAt : DateTime
├── Method : PaymentMethod  // Cash | BankTransfer | MobileMoney | Other
├── Reference : string?
├── Notes : string?
├── JournalEntryId : Guid?  // set after posting
└── RecordedByUserId : Guid
```

---

## New: Dispute

```
Dispute : BaseEntity
├── CommissionId : Guid
├── Status : DisputeStatus  // Open | Resolved | Withdrawn
├── Reason : string
├── OpenedAt : DateTime
├── OpenedByUserId : Guid
├── ResolvedAt : DateTime?
├── ResolutionNotes : string?
└── ResolvedByUserId : Guid?
```

---

## Accounting (minimal for Unit 5)

### Account (seeded Chart of Accounts — no admin UI yet)

```
Account : BaseEntity
├── Code : string  // e.g. 1100, 4100
├── Name : string
├── Type : AccountType  // Asset | Liability | Equity | Revenue | Expense
├── Currency : string  // default ETB
├── IsSystem : bool  // seeded, protected
└── IsActive : bool
```

**Seed defaults (indicative)**
| Code | Name | Type |
|------|------|------|
| 1100 | Cash / Bank | Asset |
| 1200 | Accounts Receivable — Commissions | Asset |
| 4100 | Commission Revenue | Revenue |
| 2100 | Clearing / Suspense | Liability |

### JournalEntry + JournalLine

```
JournalEntry : BaseEntity
├── EntryNumber : string  // tenant-sequential
├── PostedAt : DateTime
├── Description : string
├── SourceType : string  // "CommissionPayment"
├── SourceId : Guid?     // Payment.Id
├── PostedByUserId : Guid
└── Lines : JournalLine[]

JournalLine : BaseEntity
├── JournalEntryId : Guid
├── AccountId : Guid
├── Debit : decimal   // ETB
├── Credit : decimal  // ETB
└── Memo : string?
```

**Invariant:** Σ Debit = Σ Credit (ETB) per entry.

### ExchangeRate

```
ExchangeRate : BaseEntity
├── FromCurrency : string
├── ToCurrency : string  // ETB for v1
├── Rate : decimal
├── EffectiveDate : DateOnly
└── Source : string?  // Manual | Import
```

---

## Deferred (later finance slice)

- CoA admin UI, FiscalPeriod, TaxRate, BankReconciliation
- Full P&L / Balance Sheet / Trial Balance pages
- Per-currency CoA balances

---

## Enums

```
CommissionStatus: Open | Partial | Settled | Disputed  (already partially defined)
FeeType: AgencyFee | PartnerShare | Medical | Ticket | Other
PaymentMethod: Cash | BankTransfer | MobileMoney | Other
DisputeStatus: Open | Resolved | Withdrawn
AccountType: Asset | Liability | Equity | Revenue | Expense
```
