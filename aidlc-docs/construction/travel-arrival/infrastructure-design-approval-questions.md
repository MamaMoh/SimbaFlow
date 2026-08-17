# Unit 4 Infrastructure Design — Approval

**Unit**: Travel, Departure & Arrival  
**Artifact**: `construction/travel-arrival/infrastructure-design/infrastructure-design.md`

## Highlights

- Same Compose stack; **no new services**
- Tenant tables: ExceptionCase, InvestigationNote, LiabilityAssignment, Commission (+ unique indexes)
- Workflow upgrader guarantees Add to Commission `RemoveFromSource=false`
- Permissions `travel.*`; NoOp notifier; three Carter modules + named frontend routes
- Tests under existing `dotnet test` pipeline

## Question 1
Approve Unit 4 Infrastructure Design?

A) **Approve** — proceed to Unit 4 Code Generation plan

B) **Approve with changes** (describe after Answer)

C) **Request changes** before approving (describe after Answer)

D) Other (please describe after [Answer]: tag below)

[Answer]: A
