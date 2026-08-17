# Business Rules — Unit 5: Finance & Commission

## Commission

| ID | Rule |
|----|------|
| BR-C01 | Shell created only by Unit 4 Add to Commission — Unit 5 does not create on Arrived |
| BR-C02 | At most one non-deleted Commission per CandidateId |
| BR-C03 | Fees required before first payment (or payment allowed with warning — **enforce required**) |
| BR-C04 | Fee Amount ≥ 0; Payment Amount > 0 |
| BR-C05 | Balance = TotalFeesEtb − TotalPaidEtb (clamped display ≥ 0 for Settled check) |
| BR-C06 | Status: Disputed if open Dispute; else Settled if paid ≥ fees; else Partial if paid > 0; else Open |
| BR-C07 | Cannot delete Payment after journal posted (void/reverse later — out of scope) |
| BR-C08 | Settled commissions: fee edit blocked unless unlocked by admin (v1: block) |

## Journals

| ID | Rule |
|----|------|
| BR-J01 | Every payment posts exactly one JournalEntry |
| BR-J02 | Σ Debit = Σ Credit (ETB) or fail transaction |
| BR-J03 | Default accounts must exist (seed) before payment |
| BR-J04 | Journal lines use ETB amounts only in v1 |

## FX

| ID | Rule |
|----|------|
| BR-X01 | ETB payments use rate = 1 |
| BR-X02 | Non-ETB requires ExchangeRate for PaidAt date (or fail) |
| BR-X03 | AmountEtb = Amount × Rate (document rounding: 2 dp) |

## Disputes

| ID | Rule |
|----|------|
| BR-D01 | One Open dispute per commission at a time |
| BR-D02 | Opening dispute sets Commission.Status = Disputed |
| BR-D03 | Resolve requires ResolutionNotes |

## Permissions (indicative)

| Permission | Use |
|------------|-----|
| `commission.read` | Queue, detail, reports |
| `commission.update` | Fees, disputes |
| `commission.create` | (Unit 4 shell; Unit 5 rarely) |
| `accounting.read` | Journals, accounts list, rates |
| `accounting.post` | Record payment (posts journal) |
| `accounting.reconcile` | Deferred |

Existing seed already has `commission.*` and `accounting.*` codes.
