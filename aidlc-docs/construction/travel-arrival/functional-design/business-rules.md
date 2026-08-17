# Business Rules — Unit 4: Travel, Departure & Arrival

## Ticket

| ID | Rule |
|----|------|
| BR-T01 | `destination` and `flight_date` required to set ticket_status = Booking Complete |
| BR-T02 | To Departure only when ticket_status = Booking Complete |
| BR-T03 | After Back to Ticket from Not Departed, ticket_status resets to Pending for rebook |

## Departure

| ID | Rule |
|----|------|
| BR-D01 | Departure countdown board **excludes** `canceled=true` |
| BR-D02 | Mark Notified only when notification_status = Awaiting and not canceled |
| BR-D03 | Confirm Departed requires notification_status = Notified (gate) |
| BR-D04 | Confirm Departed forbids canceled or already Departed |
| BR-D05 | Not Departed requires `non_departure_reason` (enum or Other + free text) |
| BR-D06 | Not Departed requires explicit outcome: BackToTicket **or** CancelDeparture |
| BR-D07 | CancelDeparture sets canceled=true; blocks To Arrival and Mark Notified |
| BR-D08 | Back to Ticket clears active departure tracks for the next cycle |
| BR-D09 | Bot push is out of scope; Mark Notified is status-only |

## Arrival

| ID | Rule |
|----|------|
| BR-A01 | Arrival is a permanent ledger — Add to Commission uses RemoveFromSource=false |
| BR-A02 | Confirm Arrived only from Pending |
| BR-A03 | Returned/Runaway creates ExceptionCase (Open) if none open |
| BR-A04 | Add to Commission requires Arrived; block if open ExceptionCase exists |
| BR-A05 | At most one Open Commission shell per candidate (idempotent upsert) |
| BR-A06 | Soft-copy / duplicate candidate rows are forbidden |

## Exceptions

| ID | Rule |
|----|------|
| BR-X01 | Investigation notes require non-empty body |
| BR-X02 | Liability amount must be ≥ 0 |
| BR-X03 | CloseException requires ResolutionSummary |
| BR-X04 | Closing exception does not remove Arrival visibility |
| BR-X05 | Only one Open ExceptionCase per candidate at a time |

## Permissions (indicative)

| Permission | Actions |
|------------|---------|
| `travel.ticket` | Book ticket, To Departure |
| `travel.departure` | Notify, Departed, Not Departed outcomes |
| `travel.arrival` | Confirm Arrived, Add to Commission |
| `travel.exception` | Flag exception, notes, liability, close case |
| `travel.exception.view` | Read exception workspace |
