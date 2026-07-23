# Invoice Integrator — C# Project Architecture

> **Status:** Ready for implementation. Clean Architecture with country-agnostic domain model. Phases 1–4 aligned to project structure.

---

## Architecture Overview

**Pattern:** Clean Architecture (Domain → Application ← Infrastructure, Host orchestrates)

**Principle:** Ports & Adapters. Domain layer is pure C#, zero external dependencies. Application defines port interfaces; Infrastructure plugs in implementations. Allows Phase 1 to run with mock clients while Phase 2–3 await EDICOM sandbox.

```
Host (ASP.NET Core)
  ├─ Program.cs
  ├─ BackgroundServices/ (ProcessorWorker, ReconcilerWorker, CronTriggerWorker)
  └─ Endpoints/ (TriggerEndpoints, WebhookEndpoints)
        ↓ depends on ↓
Application
  ├─ Ports/ (IEdicomClient, IInvoiceRepository, ISourceDbReader, ITokenService, ITaxEngine, ICountryResolver)
  ├─ Specifications/ (AePintSpecification)
  ├─ Validators/ (SpecificationValidator)
  ├─ UseCases/ (BuildPayloadUseCase, ProcessInvoiceUseCase, ReconcileUseCase)
  └─ Services/ (DataGatheringService, PayloadMapper)
        ↓ depends on ↓
Domain
  └─ Models/
      ├─ InvoiceIntegration (aggregate root)
      ├─ InvoiceStatus (enum)
      └─ StructuredInvoice (value object; 51 fields country-neutral)
        ↑ depends on nothing ↑
        
Infrastructure
  ├─ Persistence/ (DbContext, Migrations, EF repositories)
  ├─ Edicom/ (EdicomClient real + mock, EdicomPayloadMapper)
  ├─ SourceDb/ (SourceDbReader, ARMS queries)
  ├─ ExternalApis/ (TaxEngine adapter, CountryResolver adapter)
  ├─ Auth/ (TokenService, OAuth2 client credentials)
  └─ ServiceBus/ (ServiceBusPublisher, ServiceBusConsumer)
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
- Fields: invoice details (number, date, type, currency, due date, business process, spec ID)
- Nested: `Party` (seller/buyer), `Address`, `ElectronicDelivery`, `TaxRegistration`, `LegalRegistration`
- Nested: `DocumentTotals`, `TaxBreakdown`, `TaxClass`, `InvoiceLine`, `UnitPrice`
- **Note:** Field comments link to AE PINT field numbers (e.g., `#1` = invoice number) for traceability

**`InvoiceIntegration`** — Aggregate root (database entity)
- Identity: `Id`, `InvoiceNumber` (business key, unique)
- State: `Status` (enum), `FailureReason`, `ErrorCode`, `ErrorMessage`
- Timestamps: `CreatedAt`, `UpdatedAt`, `ClaimedAt`, `SubmittedAt`, `ProcessedAt`, `LastReconciledAt`
- EDICOM refs: `TransactionId`, `EdicomReference`
- Reconciliation: `ReconcileAttempts`
- Domain methods: `Claim()`, `MarkSubmitted()`, `MarkAcknowledged()`, `MarkFailed()`, `IncrementReconcileAttempts()`, `ReleaseClaim()`
- Factory: `Create(invoiceNumber, createdAt)`

---

## Application Layer

Orchestrates use cases, validates business rules, defines external dependencies as port interfaces.

### Ports (Interfaces)

```csharp
IEdicomClient              // POST /invoices, GET /status/{transactionId}, mock + real
IInvoiceRepository         // Get, Save, GetPending, GetByTransactionId
ISourceDbReader            // ARMS data source: invoices, seller, buyer, lines, totals
ITokenService              // OAuth2 token acquire + cache + refresh
ITaxEngine                 // Resolve tax category & rate by policy type
ICountryResolver           // Map country name → ISO code
```

### Specifications

**`AePintSpecification`** — Knows AE PINT rules (51 fields, validation, tax codes, rounding)
- `Validate(StructuredInvoice)` → returns validation errors
- `ToJson(StructuredInvoice)` → serializes to EDICOM JSON shape

### Validators

**`SpecificationValidator`** — Cross-field rules, format constraints
- All 51 fields non-null
- Tax + lines = totals (rounding tolerance)
- Buyer country ISO-mapped
- Tax codes aligned with rates (S → 5%, Z → 0%, E → 0%)
- TRN format (15 digits)
- Currency = AED

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

**`DataGatheringService`** — Coordinates multi-source reads
- ARMS queries (seller, buyer, lines, totals)
- Tax engine (resolve category + rate)
- Country resolver (ISO codes)
- Config lookups (payment means, spec ID, process type)

**`PayloadMapper`** — `StructuredInvoice` → domain name fields; serialized by `AePintSpecification`

---

## Infrastructure Layer

Implements ports. Adapters for external systems + persistence.

### Persistence

**`DbContext`** — EF Core
- `DbSet<InvoiceIntegration>`
- Migration: `invint.InvoiceIntegration` table (15 columns from IMPLEMENTATION-PHASES.md)
- Indexes: claim query, reconciler query, webhook lookup (TransactionId)
- CHECK constraint on Status values
- UNIQUE constraint on InvoiceNumber

**`InvoiceRepository`** — Implements `IInvoiceRepository`

### EDICOM

**`EdicomClient`** — Implements `IEdicomClient`
- Real impl: POST to EDICOM sandbox/prod, GET status
- Mock impl: returns fake transactionId (Phase 1–2 dev)

**`EdicomPayloadMapper`** — `StructuredInvoice` → EDICOM JSON shape (field name mapping)

### External Systems

**`SourceDbReader`** — Implements `ISourceDbReader`; queries ARMS tables for invoice data

**`TaxEngineAdapter`** — Implements `ITaxEngine`; resolves tax category by policy type

**`CountryResolverAdapter`** — Implements `ICountryResolver`; name → ISO code mapping

### Authentication

**`TokenService`** — Implements `ITokenService`
- OAuth2 client credentials flow
- Token cache + proactive refresh (~30s before expiry)
- Handles token acquisition for EDICOM calls

### Service Bus

**`ServiceBusPublisher`** — Publish (invoiceNumber) to queue

**`ServiceBusConsumer`** — Consume messages, invoke ProcessInvoiceUseCase; DLQ on MaxDeliveryCount

---

## Host / Presentation Layer

Single ASP.NET Core runnable. Multiple background services + HTTP endpoints.

### Background Services

**`ProcessorWorker`** — Continuous message consumer
- Calls `ProcessInvoiceUseCase`
- Handles: failures, DLQ, retry logic

**`ReconcilerWorker`** — Scheduled/continuous reconciliation
- Calls `ReconcileUseCase`
- Polls EDICOM status for stale rows

**`CronTriggerWorker`** — Timer cron (configurable interval)
- Queries Source DB for new candidates
- Batch upsert to InvoiceIntegration + publish

### HTTP Endpoints

**`POST /trigger/invoice`** — Manual trigger
- Input: `invoiceNumber`
- Upsert + publish
- Returns `202 Accepted`

**`POST /webhook/edicom`** — EDICOM webhook callback
- Verify HMAC or IP allowlist
- Idempotent status update by TransactionId
- Write Acknowledged / Failed
- Returns `200 OK`

### Program.cs

DI wiring:
- Domain: register in container (if needed)
- Application: register use cases, validators, specifications
- Infrastructure: register adapters, DbContext, message broker, token service
- Host: register background services, endpoints
- Polly: attach retry + circuit breaker to `IEdicomClient`

---

## Phase-to-Project Mapping

| Phase | What | Domain | Application | Infrastructure | Host |
|-------|------|--------|-------------|-----------------|------|
| 1 | Payload builder | `StructuredInvoice` | `BuildPayloadUseCase`, `AePintSpecification`, `SpecificationValidator`, `DataGatheringService` | `SourceDbReader`, `TaxEngineAdapter`, `CountryResolverAdapter` | — |
| 2 | Processor + EDICOM mock | — | `ProcessInvoiceUseCase` | `EdicomClientMock`, `TokenService`, `ServiceBusConsumer`, `ServiceBusPublisher`, `InvoiceRepository` + DbContext | `ProcessorWorker`, migration |
| 3 | Webhook + reconciler | — | `ReconcileUseCase` | `EdicomClient` (real) | `ReconcilerWorker`, webhook endpoint, migration (alerts) |
| 4 | Triggers | `InvoiceIntegration.Create()` | — | — | `CronTriggerWorker`, `/trigger/invoice` endpoint |

**Dependency:** Phases 2 & 4 can proceed parallel after Phase 1. Phase 3 blocked on EDICOM contract.

---

## Key Design Decisions

1. **`StructuredInvoice` is country-agnostic** — Named for extensibility. AE PINT rules live in `AePintSpecification` (Application). If another country PINT arrives, add `SgPintSpecification`, etc. No rename needed.

2. **`IEdicomClient` behind a port** — Enables Phase 1–2 to run with mock indefinitely. Swap real client when sandbox arrives; processor untouched.

3. **Domain methods guard state transitions** — `Claim()`, `MarkSubmitted()` enforce valid paths. Prevents accidental state corruption.

4. **Stale claim recovery** — `ReleaseClaim()` lets reconciler reset a `Claimed` row to `Pending` if processor crashed. Prevents orphaned rows.

5. **Atomic claim** — Processor atomically claims row, advances to `Claimed` state, then gathers + submits. Prevents concurrent processors colliding.

6. **Single Host process** — All workers (processor, reconciler, cron trigger) in one ASP.NET Core app for Phase 1–2 simplicity. Can split into separate deployables later if scale demands.

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
│  └─ Models/ (InvoiceStatus.cs, InvoiceIntegration.cs, StructuredInvoice.cs + nested records)
├─ InvoiceIntegrator.Application/
│  ├─ Ports/ (interfaces)
│  ├─ Specifications/ (AePintSpecification.cs)
│  ├─ Validators/ (SpecificationValidator.cs)
│  ├─ UseCases/ (BuildPayloadUseCase.cs, ProcessInvoiceUseCase.cs, ReconcileUseCase.cs)
│  └─ Services/ (DataGatheringService.cs, PayloadMapper.cs)
├─ InvoiceIntegrator.Infrastructure/
│  ├─ Persistence/ (DbContext.cs, InvoiceRepository.cs, Migrations/)
│  ├─ Edicom/ (EdicomClient.cs, EdicomClientMock.cs, EdicomPayloadMapper.cs)
│  ├─ SourceDb/ (SourceDbReader.cs)
│  ├─ ExternalApis/ (TaxEngineAdapter.cs, CountryResolverAdapter.cs)
│  ├─ Auth/ (TokenService.cs)
│  └─ ServiceBus/ (ServiceBusPublisher.cs, ServiceBusConsumer.cs)
└─ InvoiceIntegrator.Host/
   ├─ Program.cs (DI wiring)
   ├─ BackgroundServices/ (ProcessorWorker.cs, ReconcilerWorker.cs, CronTriggerWorker.cs)
   └─ Endpoints/ (TriggerEndpoints.cs, WebhookEndpoints.cs)
```

---

## Next Steps for Agent

1. Create domain models (InvoiceStatus, InvoiceIntegration, StructuredInvoice + nested records)
2. Define Application ports + specifications
3. Scaffold EF Core DbContext + initial migration
4. Implement Phase 1 use case (BuildPayloadUseCase, DataGatheringService)
5. Wire up DI in Program.cs
6. Add unit + integration tests per phase

See [IMPLEMENTATION-PHASES.md](IMPLEMENTATION-PHASES.md) for detailed phase-by-phase deliverables.
