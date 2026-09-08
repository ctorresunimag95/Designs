# Invoice Integrator — C# Project Architecture

> **Status:** Ready for implementation. Clean Architecture with country-agnostic domain model. Phases 1–4 aligned to project structure.

---

## Architecture Overview

**Pattern:** Clean Architecture (Domain → Application ← Infrastructure, Host orchestrates)

**Principle:** Ports & Adapters. Domain layer is pure C#, zero external dependencies. Application defines port interfaces; Infrastructure plugs in implementations. Allows Phase 1 to run with mock clients while Phase 2–3 await EDICOM sandbox.

**Two independently deployable services** (per ADR-0025):
- **SOA.InvoiceExtractor** — discovers candidates, gathers ARMS data, validates payload, upserts tracking row, publishes to queue
- **BHSI.InvoiceSubmitter** — consumes queue, claims row, acquires token, submits to EDICOM, handles webhook + reconciler

Both services share Domain, Application, and a single Infrastructure assembly. Boundaries within Infrastructure are enforced by folder convention. Each host wires only the registrations it needs in `Program.cs`.

```
Shared assemblies
  InvoiceIntegrator.Domain
  InvoiceIntegrator.Application
  InvoiceIntegrator.Infrastructure   (one class library, split by folder — see below)

Deployable hosts
  SOA.InvoiceExtractor
    ├─ Program.cs
    ├─ BackgroundServices/ (CronExtractorWorker)
    └─ Endpoints/ (TriggerEndpoints)

  BHSI.InvoiceSubmitter
    ├─ Program.cs
    ├─ BackgroundServices/ (ProcessorWorker, ReconcilerWorker)
    └─ Endpoints/ (WebhookEndpoints)
```

---

## Domain Layer

Pure C#, no frameworks. Immutable value objects + aggregate root with guarded state transitions.

### Models

**`InvoiceStatus`** — State machine enum
```csharp
enum InvoiceStatus { Pending, Claimed, WaitingConfirmation, Acknowledged, Failed }
```

**`StructuredInvoice`** — Country-agnostic invoice (51 fields, grouped into logical records)
- Header (`#1–9`): `InvoiceNumber`, `IssueDate`, `InvoiceTypeCode`, `CurrencyCode`, `InvoiceTransactionTypeCode`, `DueDate`, `BusinessProcessType`, `SpecificationIdentifier`, `PaymentMeansCode`
- `Party` (seller `#10–20` / buyer `#21–29`): `Name`, `ElectronicDelivery` (`Address`, `Identifier`), `LegalInfo` (`Identifier`, `RegistrationIdentifierType`), `TaxInfo` (`Identifier`, `Scheme`), `Address` (`Line1`, `City`, `Subdivision`, `CountryCode`)
- `TaxBreakdown` (`#35–38`): `TaxableAmount`, `TaxAmount`, `TaxClass` (`Code`, `Rate`) — one entry per tax category
- `InvoiceLine` (`#39–51`): `LineIdentifier`, `InvoicedQuantity`, `UnitOfMeasureCode`, `LineNetAmount`, `UnitPrice` (`Net`, `Gross`, `BaseQuantity`), `TaxClass`, `VatLineAmountAED`, `LineAmountAED`, `ItemName`, `ItemDescription`
- `DocumentTotals` (`#30–34`): `SumOfLineNetAmounts`, `InvoiceTotalWithoutTax`, `InvoiceTotalTaxAmount`, `InvoiceTotalWithTax`, `AmountDueForPayment`
- **Note:** Structure mirrors the AE PINT sample payload in [PHASE1-ARMS-Datapoints-EDICOM-Gap-Analysis.md](PHASE1-ARMS-Datapoints-EDICOM-Gap-Analysis.md). Field comments link to AE PINT field numbers (e.g., `#1` = invoice number) for traceability.

**`InvoiceIntegration`** — Aggregate root (maps to `invint.InvoiceIntegration` table)
- Identity: `Id`, `InvoiceNumber` (business key, unique)
- State: `Status` (enum), `FailureReason`, `ErrorCode`, `ErrorMessage`
- Timestamps: `CreatedAt`, `UpdatedAt`, `ClaimedAt`, `SubmittedAt`, `ProcessedAt`, `LastReconciledAt`
- EDICOM refs: `TransactionId`, `EdicomReference`
- Reconciliation: `ReconcileAttempts`
- Domain methods: `Claim()`, `MarkSubmitted()`, `MarkAcknowledged()`, `MarkFailed()`, `IncrementReconcileAttempts()`, `ReleaseClaim()`
- Factory: `Create(invoiceNumber, createdAt)`

**`TransactionLog`** — Submission event log (one row per EDICOM submission attempt / webhook event)
- `Id`, `InvoiceNumber`, `TransactionId`
- `EventType` (enum: `Submitted`, `WebhookReceived`, `ReconcilerPolled`)
- `Status`, `EdicomReference`, `ErrorCode`
- `OccurredAt`
- Written by Submitter; read by Reconciler and observability queries

---

## Application Layer

Orchestrates use cases, validates business rules, defines external dependencies as port interfaces.

### Ports (Interfaces)

```csharp
IEdicomClient              // POST /publish, GET /messages?transactionId={id}, mock + real
IInvoiceRepository         // Get, Save, GetByTransactionId, atomic Claim (invint.InvoiceIntegration)
ITransactionLogRepository  // Append(TransactionLog), GetByTransactionId
IQueuePublisher            // Publish(StructuredInvoice payload) to InvoiceProcessingQueue
ISourceDbReader            // ARMS data source: invoices, seller, buyer, lines, totals
ITokenService              // Auth token acquire + cache + refresh
ITaxEngine                 // Resolve tax category & rate by policy type
ICountryResolver           // Map country name → ISO 3166-1 alpha-2 code
IInvoiceDefaultsProvider   // Org/tenant defaults + codelists (seller BHSI values,
                           //   invoice type, currency, business process, spec ID,
                           //   payment means, transaction type) — the "default value /
                           //   logic to be provided by CC/OPS" fields from Gap Analysis
```

### Specifications

**`AePintSpecification`** — Knows AE PINT rules (51 fields, validation, tax codes, rounding)
- `Validate(StructuredInvoice)` → returns validation errors
- `ToJson(StructuredInvoice)` → serializes to EDICOM JSON shape

### Validators

**`SpecificationValidator`** — Cross-field rules, format constraints (Phase 1 checklist)
- All 51 mandatory fields populated (no null/missing in terminal JSON)
- Line amounts + tax = totals; tax calc matches (no rounding drift > 0.01 AED)
- Buyer country code resolved via ISO mapping table
- Tax category code and rate align (no `S` with 0%, no `Z`/`E` with 5%)
- Invoice number unique + stable format (`INV-{YYYY}-{sequence}`)
- TRN format (15 digits) / currency = AED

### Use Cases

**`BuildPayloadUseCase`** — Phase 1
- Input: `invoiceNumber`
- Output: `StructuredInvoice` (fully validated)
- Orchestrates: data gathering + validation + payload construction

**`ProcessInvoiceUseCase`** — Phase 2
- Input: `invoiceNumber` from queue
- Atomic claim → gather payload → acquire token → submit EDICOM → write TransactionId
- Handles: retry logic (Polly), circuit breaker, state transitions

**`ReconcileUseCase`** — Phase 3
- Query `WaitingConfirmation` rows past grace window
- Poll EDICOM status per TransactionId
- Write `Acknowledged` / `Failed` or increment `ReconcileAttempts`
- Alert on max attempts exceeded

### Services

**`DataGatheringService`** — Coordinates multi-source reads (used by Extractor)
- ARMS queries (buyer name/TRN/address, line premium amounts, totals, due date)
- Seller + document defaults via `IInvoiceDefaultsProvider` (BHSI name/TRN/address/trade license, invoice type, currency, spec ID, business process, payment means)
- Tax engine (resolve category code + rate; default `S` / 5%)
- Country resolver (buyer country name → ISO code)
- Derived values: seller/buyer electronic address (first 10 digits of TRN), electronic identifier (`0235` + first 10 digits), item name/description from policy

---

## Infrastructure Layer

One class library (`InvoiceIntegrator.Infrastructure`). Boundaries enforced by folder. Each host's `Program.cs` registers only the folders it needs.

### Persistence/ — both hosts

**`InvoiceIntegratorDbContext`** — EF Core
- `DbSet<InvoiceIntegration>` — `invint.InvoiceIntegration` table
- `DbSet<TransactionLog>` — `invint.TransactionLog` table
- Indexes: claim query (Status + ClaimedAt), reconciler query (Status + SubmittedAt), webhook lookup (TransactionId)
- CHECK constraint on Status values; UNIQUE constraint on InvoiceNumber

**`InvoiceRepository`** — Implements `IInvoiceRepository`

**`TransactionLogRepository`** — Implements `ITransactionLogRepository`

### Messaging/ — both hosts

**`ServiceBusPublisher`** — Implements `IQueuePublisher`; publishes complete EDICOM payload to `InvoiceProcessingQueue`

**`ServiceBusConsumer`** — Consumes messages from queue, delivers to ProcessorWorker; DLQ on MaxDeliveryCount

### SourceDb/ — Extractor only

**`SourceDbReader`** — Implements `ISourceDbReader`; queries ARMS tables for invoice data

### ExternalApis/ — Extractor only

**`TaxEngineAdapter`** — Implements `ITaxEngine`; resolves tax category by policy type

**`CountryResolverAdapter`** — Implements `ICountryResolver`; name → ISO code mapping

### Defaults/ — Extractor only

**`InvoiceDefaultsProvider`** — Implements `IInvoiceDefaultsProvider`; org/tenant defaults from config (seller BHSI values, invoice type `380`, currency `AED`, spec ID `urn:peppol:pint:billing-1@ae-1`, business process `urn:peppol:bis:billing`, payment means `30`, transaction type `00000000`)

### Edicom/ — Submitter only

**`EdicomClient`** — Implements `IEdicomClient`
- Real impl: POST `/publish`, GET `/messages?transactionId={id}`
- Mock impl: returns fake transactionId (Phase 1–2 dev)

**`EdicomPayloadMapper`** — `StructuredInvoice` → EDICOM JSON shape (field name mapping per AE PINT spec)

### Auth/ — Submitter only

**`TokenService`** — Implements `ITokenService`
- EDICOM auth flow (`POST /token`, `grant_type=password`, `scope=openid`); auth model (API key / OAuth2 / mTLS) pending EDICOM confirmation — kept behind the port
- Token cache + proactive refresh (~30s before `expires_in: 3600`)
- Credentials sourced from Azure Key Vault

---

## Host / Presentation Layer

Two Azure Functions apps (.NET 10 isolated worker), independently deployable. Each app hosts multiple triggers.

### SOA.InvoiceExtractor — Azure Functions App

**`InvoiceExtractorTimerTrigger`** — `TimerTrigger` (configurable cron expression)
- Queries Source DB for candidate invoices (last N hours)
- For each: gather full ARMS data → validate payload → upsert `invint.InvoiceIntegration` (Status = Pending) → publish complete payload to queue

**`InvoiceManualTrigger`** — `HttpTrigger` POST `/trigger/invoice`
- Input: `{ invoiceNumber }`
- Same pipeline as timer: gather → validate → upsert → publish
- Returns `202 Accepted`

### BHSI.InvoiceSubmitter — Azure Functions App

**`InvoiceSubmitterQueueTrigger`** — `ServiceBusTrigger` on `InvoiceProcessingQueue`
- Atomic claim row (Status = Claimed) → validate payload → acquire token → POST to EDICOM → write TransactionId → append TransactionLog row → advance Status = WaitingConfirmation

**`WebhookTrigger`** — `HttpTrigger` POST `/webhook/edicom`
- Verify HMAC or IP allowlist
- Idempotent status update by TransactionId → append TransactionLog row (EventType = WebhookReceived)
- Returns `200 OK`

**`ReconcilerTimerTrigger`** — `TimerTrigger` (configurable cron expression)
- Queries `WaitingConfirmation` rows past grace window → polls EDICOM status per TransactionId → writes terminal state + TransactionLog row → alerts on max attempts exceeded

### Program.cs (per app)

Each app wires only the infrastructure folders it needs:
- Extractor: `SourceDb/`, `Defaults/`, `ExternalApis/`, `Messaging/` (publisher), `Persistence/`
- Submitter: `Edicom/`, `Auth/`, `Messaging/` (consumer), `Persistence/`
- Both: Application use cases + validators + specifications; Polly on `IEdicomClient` (Submitter only)

---

## Phase-to-Project Mapping

| Phase | What | Domain | Application | Infrastructure | Host |
|-------|------|--------|-------------|-----------------|------|
| 1 | Payload builder (Extractor core) | `StructuredInvoice`, `InvoiceIntegration` | `BuildPayloadUseCase`, `AePintSpecification`, `SpecificationValidator`, `DataGatheringService` | `SourceDbReader`, `TaxEngineAdapter`, `CountryResolverAdapter`, `InvoiceDefaultsProvider`, `DbContext` | — |
| 2 | Submitter + EDICOM mock | `TransactionLog` | `ProcessInvoiceUseCase` | `EdicomClientMock`, `TokenService`, `ServiceBusConsumer`, `ServiceBusPublisher`, `InvoiceRepository`, `TransactionLogRepository` | `ProcessorWorker`, migration |
| 3 | Webhook + reconciler | — | `ReconcileUseCase` | `EdicomClient` (real) | `WebhookTrigger`, `ReconcilerTimerTrigger` |
| 4 | Extractor triggers | — | — | — | `InvoiceExtractorTimerTrigger`, `InvoiceManualTrigger` |

**Dependency:** Phases 2 & 4 can proceed in parallel after Phase 1. Phase 3 blocked on EDICOM sandbox contract.

---

## Key Design Decisions

1. **`StructuredInvoice` is country-agnostic** — Named for extensibility. AE PINT rules live in `AePintSpecification` (Application). If another country PINT arrives, add `SgPintSpecification`, etc. No rename needed.

2. **`IEdicomClient` behind a port** — Enables Phase 1–2 to run with mock indefinitely. Swap real client when sandbox arrives; processor untouched.

3. **Domain methods guard state transitions** — `Claim()`, `MarkSubmitted()` enforce valid paths. Prevents accidental state corruption.

4. **Stale claim recovery** — `ReleaseClaim()` lets reconciler reset a `Claimed` row to `Pending` if processor crashed. Prevents orphaned rows.

5. **Atomic claim** — Processor atomically claims row, advances to `Claimed` state, then gathers + submits. Prevents concurrent processors colliding.

6. **One Functions app per service** — Each Azure Functions app hosts all its triggers (timer, HTTP, queue). Deployment boundary aligns with the ADR two-service model. No reason to split per trigger at this scale.

7. **Polly on `IEdicomClient`** — Retry (3×, exponential backoff) + circuit breaker (5 failures) wrapped transparently. DLQ for messages that exhaust.

---

## Exit Criteria

- **Phase 1:** Given an `invoiceNumber`, produce a fully validated, serializable `StructuredInvoice` with all 51 fields from ARMS/defaults. No EDICOM call.
- **Phase 2:** Processor consumes queue messages, claims rows, submits to EDICOM mock, writes `TransactionId`. Retry + CB + DLQ tested.
- **Phase 3:** Invoices reach terminal state (`Acknowledged` / `Failed`) via webhook + reconciler fallback paths against EDICOM sandbox.
- **Phase 4:** Both trigger types produce correct `Pending` rows and messages that processor drives to terminal state.

---

## File Structure (C# Projects)

```
InvoiceIntegrator.sln
├─ InvoiceIntegrator.Domain/
│  └─ Models/ (InvoiceStatus.cs, InvoiceIntegration.cs, TransactionLog.cs,
│               StructuredInvoice.cs + nested records)
├─ InvoiceIntegrator.Application/
│  ├─ Ports/ (IEdicomClient.cs, IInvoiceRepository.cs, ITransactionLogRepository.cs,
│  │          IQueuePublisher.cs, ISourceDbReader.cs, ITokenService.cs,
│  │          ITaxEngine.cs, ICountryResolver.cs, IInvoiceDefaultsProvider.cs)
│  ├─ Specifications/ (AePintSpecification.cs)
│  ├─ Validators/ (SpecificationValidator.cs)
│  ├─ UseCases/ (BuildPayloadUseCase.cs, ProcessInvoiceUseCase.cs, ReconcileUseCase.cs)
│  └─ Services/ (DataGatheringService.cs)
├─ InvoiceIntegrator.Infrastructure/
│  ├─ Persistence/ (InvoiceIntegratorDbContext.cs, InvoiceRepository.cs,
│  │                TransactionLogRepository.cs, Migrations/)
│  ├─ Messaging/   (ServiceBusPublisher.cs, ServiceBusConsumer.cs)
│  ├─ SourceDb/    (SourceDbReader.cs)                         — Extractor only
│  ├─ ExternalApis/(TaxEngineAdapter.cs, CountryResolverAdapter.cs) — Extractor only
│  ├─ Defaults/    (InvoiceDefaultsProvider.cs)                — Extractor only
│  ├─ Edicom/      (EdicomClient.cs, EdicomClientMock.cs,
│  │                EdicomPayloadMapper.cs)                    — Submitter only
│  └─ Auth/        (TokenService.cs)                           — Submitter only
├─ SOA.InvoiceExtractor/                        (Azure Functions app)
│  ├─ Program.cs
│  └─ Functions/ (InvoiceExtractorTimerTrigger.cs, InvoiceManualTrigger.cs)
└─ BHSI.InvoiceSubmitter/                       (Azure Functions app)
   ├─ Program.cs
   └─ Functions/ (InvoiceSubmitterQueueTrigger.cs, WebhookTrigger.cs,
                   ReconcilerTimerTrigger.cs)
```

---

## Next Steps

1. Create domain models (`InvoiceStatus`, `InvoiceIntegration`, `TransactionLog`, `StructuredInvoice` + nested records)
2. Define Application ports + `AePintSpecification` + `SpecificationValidator`
3. Implement Phase 1: `BuildPayloadUseCase`, `DataGatheringService`, `SourceDbReader`, `InvoiceDefaultsProvider`, `TaxEngineAdapter`, `CountryResolverAdapter`
4. Scaffold EF Core `InvoiceIntegratorDbContext` + initial migration (both tables)
5. Wire Extractor host DI (`Program.cs`) with Phase 1 components
6. Phase 2: `ProcessInvoiceUseCase`, `EdicomClientMock`, `TokenService`, `ServiceBusPublisher/Consumer`, Submitter host

See [IMPLEMENTATION-PHASES.md](IMPLEMENTATION-PHASES.md) for detailed phase-by-phase deliverables.
