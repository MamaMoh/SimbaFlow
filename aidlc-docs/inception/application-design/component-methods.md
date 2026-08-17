# Component Methods

## DC-01: Candidate Aggregate

| Method | Input | Output | Purpose |
|--------|-------|--------|---------|
| Register | RegisterCandidateCommand (name, passport, nationality, DOB, contact, labourId?, country?, office?, contractDate?) | Result&lt;Guid&gt; | Create new candidate record |
| Update | UpdateCandidateCommand (id, fields...) | Result | Update candidate fields |
| Delete | DeleteCandidateCommand (id) | Result | Soft-delete candidate |
| GetById | GetCandidateQuery (id) | Result&lt;CandidateDto&gt; | Retrieve full candidate detail |
| Search | SearchCandidatesQuery (term, filters, page, pageSize) | Result&lt;PaginatedList&lt;CandidateListDto&gt;&gt; | Search/filter candidates |
| UploadDocument | UploadDocumentCommand (candidateId, file, documentType) | Result&lt;Guid&gt; | Store document reference |
| GetDocuments | GetCandidateDocumentsQuery (candidateId) | Result&lt;List&lt;DocumentDto&gt;&gt; | List candidate documents |
| GenerateCV | GenerateCVCommand (candidateId) | Result&lt;byte[]&gt; | Generate PDF CV |
| GetTimeline | GetCandidateTimelineQuery (candidateId) | Result&lt;List&lt;TimelineEntryDto&gt;&gt; | Get full status history |

## DC-02: Workflow Engine (Event-Sourced)

| Method | Input | Output | Purpose |
|--------|-------|--------|---------|
| ExecuteTransition | ExecuteTransitionCommand (candidateId, transitionId, userId, fields?) | Result | Execute a workflow transition (appends event) |
| GetCurrentState | GetWorkflowStateQuery (candidateId) | Result&lt;WorkflowStateDto&gt; | Derive current state from events |
| GetAvailableActions | GetAvailableActionsQuery (candidateId, userId) | Result&lt;List&lt;ActionDto&gt;&gt; | Compute available actions for user+candidate |
| GetViewCandidates | GetViewCandidatesQuery (stageId, filters, userId) | Result&lt;PaginatedList&lt;ViewCandidateDto&gt;&gt; | Get candidates visible in a specific view |
| ReplayEvents | ReplayEventsQuery (candidateId, asOfDate?) | Result&lt;WorkflowStateDto&gt; | Temporal query — state at any point |
| GetEventStream | GetEventStreamQuery (candidateId) | Result&lt;List&lt;WorkflowEventDto&gt;&gt; | Full event history |

## DC-03: Workflow Configuration

| Method | Input | Output | Purpose |
|--------|-------|--------|---------|
| GetWorkflowDefinition | GetWorkflowDefinitionQuery (tenantId) | Result&lt;WorkflowDefinitionDto&gt; | Get full workflow config for agency |
| CreateStage | CreateStageCommand (name, position, description) | Result&lt;Guid&gt; | Add workflow stage |
| UpdateStage | UpdateStageCommand (stageId, name?, position?, description?) | Result | Modify stage |
| DeleteStage | DeleteStageCommand (stageId) | Result | Remove stage |
| CreateStatus | CreateStatusCommand (stageId, name, isTerminal) | Result&lt;Guid&gt; | Add status to stage |
| CreateTransitionRule | CreateTransitionRuleCommand (sourceStageId, targetStageId, conditions, requiredFields, allowedRoles, buttonLabel) | Result&lt;Guid&gt; | Define transition |
| UpdateTransitionRule | UpdateTransitionRuleCommand (ruleId, conditions?, requiredFields?, allowedRoles?, buttonLabel?) | Result | Modify transition |
| ConfigureParallelTracks | ConfigureParallelTracksCommand (stageId, tracks[]) | Result | Set up parallel tracks |
| ConfigureMirrorView | ConfigureMirrorViewCommand (stageId, conditions, targetStageId) | Result | Set up mirror view rule |
| SeedDefaultWorkflow | SeedDefaultWorkflowCommand (tenantId) | Result | Create 8-stage default template |

## DC-04: Accounting (Standalone)

| Method | Input | Output | Purpose |
|--------|-------|--------|---------|
| CreateAccount | CreateAccountCommand (code, name, type, currency, parentId?) | Result&lt;Guid&gt; | Add account to chart |
| PostJournalEntry | PostJournalEntryCommand (date, description, lines[{accountId, debit, credit, currency}]) | Result&lt;Guid&gt; | Record double-entry transaction |
| GetTrialBalance | GetTrialBalanceQuery (asOfDate) | Result&lt;TrialBalanceDto&gt; | Balances for all accounts |
| GetProfitAndLoss | GetPnLQuery (startDate, endDate) | Result&lt;PnLDto&gt; | P&L statement |
| GetBalanceSheet | GetBalanceSheetQuery (asOfDate) | Result&lt;BalanceSheetDto&gt; | Balance sheet |
| GetLedger | GetLedgerQuery (accountId, startDate, endDate) | Result&lt;List&lt;LedgerEntryDto&gt;&gt; | Account transaction history |
| RecordExchangeRate | RecordExchangeRateCommand (fromCurrency, toCurrency, rate, date) | Result | Store exchange rate |

## DC-05: Commission Bridge

| Method | Input | Output | Purpose |
|--------|-------|--------|---------|
| InitializeCommission | InitializeCommissionCommand (candidateId, feeStructure) | Result&lt;Guid&gt; | Create commission record |
| DefineFeeBreakdown | DefineFeeBreakdownCommand (commissionId, fees[{category, amount, currency}]) | Result | Set fee components |
| RecordPayment | RecordPaymentCommand (commissionId, amount, currency, method, reference, payer) | Result&lt;Guid&gt; | Record payment (creates journal entry) |
| GetCommissionStatus | GetCommissionQuery (candidateId) | Result&lt;CommissionDto&gt; | Get payment status |
| LogDispute | LogDisputeCommand (commissionId, amount, reason, counterparty) | Result&lt;Guid&gt; | Create dispute |
| ResolveDispute | ResolveDisputeCommand (disputeId, outcome, adjustmentAmount?) | Result | Close dispute |
| GetOfficeCommissions | GetOfficeCommissionsQuery (officeId, filters) | Result&lt;PaginatedList&lt;CommissionListDto&gt;&gt; | Office-level commission list |

## DC-06: Agency/Tenant

| Method | Input | Output | Purpose |
|--------|-------|--------|---------|
| ProvisionTenant | ProvisionTenantCommand (name, slug, contact, HQ, agencyLevel, license…, adminUser) | Result&lt;Guid&gt; | Create agency (schema + seed + HQ office) |
| UpdateTenantLicense | UpdateTenantLicenseCommand (tenantId, level, license fields, countries) | Result | MoLS license metadata |
| GetTenants | GetTenantsQuery | Result&lt;List&lt;TenantDto&gt;&gt; | List all tenants (admin) |
| CreateOffice | CreateOfficeCommand (name, address, city, country, phone, managerId?) | Result&lt;Guid&gt; | Add office/branch |
| UpdateOffice | UpdateOfficeCommand (id, fields...) | Result | Modify office |
| CreatePartnerAgency | CreatePartnerAgencyCommand (name, country, capacityTier, licenseId?, contact…) | Result&lt;Guid&gt; | SuperAdmin: add catalog partner |
| UpdatePartnerAgency | UpdatePartnerAgencyCommand (…) | Result | SuperAdmin: update catalog |
| GetPartnerCatalog | GetPartnerCatalogQuery (country?, active?) | Result&lt;List&lt;PartnerDto&gt;&gt; | Admin catalog list |
| LinkPartner | LinkPartnerCommand (partnerAgencyId, agreementStart, agreementEnd) | Result&lt;Guid&gt; | Tenant: create ትስስር + agreement |
| RenewPartnerLink | RenewPartnerLinkCommand (linkId, newEnd) | Result | Extend agreement |
| GetLinkedPartners | GetLinkedPartnersQuery (country?, activeOnly?) | Result&lt;List&lt;PartnerLinkDto&gt;&gt; | Tenant intake options |

## DC-08: Exception Containment

| Method | Input | Output | Purpose |
|--------|-------|--------|---------|
| CreateException | CreateExceptionCommand (candidateId, type, reason, details) | Result&lt;Guid&gt; | Flag candidate as Returned/Runaway |
| AddInvestigationNote | AddInvestigationNoteCommand (exceptionId, note, attachments?) | Result | Add investigation entry |
| AssignLiability | AssignLiabilityCommand (exceptionId, party, amount, currency) | Result | Assign financial liability |
| ResolveException | ResolveExceptionCommand (exceptionId, outcome, financialSettlement?) | Result | Close the investigation |
| GetExceptions | GetExceptionsQuery (filters, page, pageSize) | Result&lt;PaginatedList&lt;ExceptionDto&gt;&gt; | List exception cases |

## DC-09: Notification

| Method | Input | Output | Purpose |
|--------|-------|--------|---------|
| CreateNotificationRule | CreateNotificationRuleCommand (eventType, channels[], roles[], template) | Result&lt;Guid&gt; | Define notification rule |
| UpdateNotificationRule | UpdateNotificationRuleCommand (ruleId, fields...) | Result | Modify rule |
| SendNotification | SendNotificationCommand (userId, channel, message, language) | Result | Send single notification |
| GetDeliveryStatus | GetDeliveryStatusQuery (filters) | Result&lt;List&lt;DeliveryDto&gt;&gt; | Check delivery history |
| SetLanguagePreference | SetLanguagePreferenceCommand (userId, language) | Result | Set user language (en/am) |

## IC-03: SignalR Hub

| Method | Input | Output | Purpose |
|--------|-------|--------|---------|
| OnConnected | connectionId, userId, tenantId | void | Register user connection |
| OnDisconnected | connectionId | void | Remove user connection |
| BroadcastCandidateUpdate | tenantId, officeId, candidateId, changeType, data | void | Push update to connected users |
| BroadcastNotification | userId, notification | void | Push personal notification |

## IC-04/05: Bot Services

| Method | Input | Output | Purpose |
|--------|-------|--------|---------|
| HandleStatusCommand | passportNumber or name | StatusResponse | Look up candidate status |
| HandleMedicalCommand | passportNumber, result | ConfirmationResponse | Update medical status |
| HandleArrivedCommand | passportNumber | ConfirmationResponse | Confirm arrival |
| HandleCVCommand | passportNumber | FileResponse (PDF) | Generate and send CV |
| HandleLanguageCommand | userId, language | ConfirmationResponse | Set language preference |
| HandleRegisterCommand | chatId | VerificationResponse | Link bot user to system account |

## Report/Export Services

| Method | Input | Output | Purpose |
|--------|-------|--------|---------|
| GeneratePipelineReport | filters | ReportDataDto | Pipeline stage counts |
| GeneratePerformanceReport | officeId?, dateRange | PerformanceDto | Agency/office performance |
| GenerateFinancialSummary | dateRange, officeId? | FinancialSummaryDto | Financial summary |
| ExportToExcel | reportData, columns | byte[] | Generate .xlsx |
| ExportToPDF | reportData, template | byte[] | Generate .pdf |
| ConfigureScheduledReport | reportType, frequency, recipients, filters | Result&lt;Guid&gt; | Set up auto-generation |
