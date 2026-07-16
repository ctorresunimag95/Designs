# Invoice Integration — Architecture Options

## Scenario A — Existing Source Database

### Option A1 — Direct Scheduled Extractor

```mermaid
flowchart LR
    Sched([Timer / Cron])
    subgraph Worker[Invoice Integrator Worker]
        Extractor[Extract Candidates\nwatermark + anti-join]
        Pipeline[Claim → Validate → Submit → Record]
    end
    Source[(Source DB\nREAD-ONLY)]
    Track[(invint.InvoiceDispatch\nUNIQUE InvoiceNumber)]
    Edicom{{EDICOM ASP\nPeppol Access Point}}

    Sched -- trigger --> Worker
    Extractor -- read watermark --> Source
    Extractor -- anti-join / due retries --> Track
    Pipeline -- claim + status writes --> Track
    Pipeline -- POST invoice --> Edicom
    Edicom -- transactionId + status --> Pipeline
```

---

### Option A2 — Polling Extractor + Message Bus + EDICOM Processor

```mermaid
flowchart LR
    Sched([Timer / Cron])

    subgraph Poller[Poller Service]
        Extract[Extract Candidates\nwatermark + anti-join]
        Publish[Publish InvoiceDetected]
    end

    subgraph Processor[EDICOM Processor\n scale horizontally]
        Consume[Consume InvoiceDetected]
        Transform[Map → AE PINT]
        Submit[Submit to EDICOM]
        Record[Record Outcome]
    end

    Source[(Source DB\nREAD-ONLY)]
    Track[(invint.InvoiceDispatch\nUNIQUE InvoiceNumber)]
    Bus([Message Bus\nAzure Service Bus /\nRabbitMQ])
    Edicom{{EDICOM ASP\nPeppol Access Point}}

    Sched -- trigger --> Poller
    Extract -- read watermark --> Source
    Extract -- anti-join --> Track
    Publish -- InvoiceDetected --> Bus
    Bus -- message --> Consume
    Consume -- claim --> Track
    Submit -- POST invoice --> Edicom
    Edicom -- transactionId --> Submit
    Record -- status write --> Track
```

> **Backpressure risk.** If the timer is delayed or misses runs, the poller may publish a large burst of messages at once. If the processor drains slower than the poller publishes (e.g. EDICOM throttling, circuit breaker open), the queue depth grows and can hit Service Bus size limits. Mitigate by capping the poller's batch size per run (`TOP N`) so the watermark advances incrementally, and scale out processor instances to drain faster.

---

### Option A3 — Polling Extractor + Bus + EDICOM Processor + Async Status Reconciler

```mermaid
flowchart LR
    Sched([Timer / Cron])
    StatusSched([Status Poll Cron])

    subgraph Poller[Poller Service]
        Extract[Extract Candidates]
        Publish[Publish InvoiceDetected]
    end

    subgraph Processor[EDICOM Processor]
        Consume[Consume InvoiceDetected]
        Submit[Submit to EDICOM]
        RecordTxn[Record transactionId\nStatus = WaitingConfirmation]
    end

    subgraph Reconciler[Status Reconciler]
        PollStatus[Poll EDICOM status endpoint\nfor WaitingConfirmation rows]
        RecordFinal[Record ACKNOWLEDGED /\nREJECTED]
    end

    Source[(Source DB\nREAD-ONLY)]
    Track[(invint.InvoiceDispatch\nUNIQUE InvoiceNumber)]
    Bus([Message Bus])
    Edicom{{EDICOM ASP}}

    Sched -- trigger --> Poller
    Extract -- read --> Source
    Publish -- InvoiceDetected --> Bus
    Bus -- message --> Consume
    Consume -- claim --> Track
    Submit -- POST --> Edicom
    Edicom -- transactionId --> RecordTxn
    RecordTxn -- write WaitingConfirmation --> Track
    StatusSched -- trigger --> Reconciler
    PollStatus -- GET status --> Edicom
    PollStatus -- read WaitingConfirmation rows --> Track
    RecordFinal -- update terminal status --> Track
```

> **Backpressure risk.** Same as A2 — a large catch-up burst from the poller can flood the bus faster than the processor drains it, compounded here by the reconciler adding its own EDICOM polling load. Apply the same batch size cap on the poller and scale out processor instances independently of the reconciler.

---

## Scenario B — No Existing Data Source (Invoice Hub)

### Option B1 — Invoice Hub with Push Ingest + EDICOM Publisher

```mermaid
flowchart LR
    ERP[ERP System]
    Billing[Billing System]
    Claims[Claims Engine]
    Policy[Policy System]

    subgraph Hub[Invoice Hub]
        API[Ingest API\nREST / gRPC]
        Normalise[Normalise → CanonicalInvoice\nACL per source]
        Validate[AE PINT Validation]
        Dispatch[Dispatch Pipeline\nClaim → Submit → Record]
    end

    Track[(invint.InvoiceDispatch\nUNIQUE SourceSystem + InvoiceNumber)]
    Edicom{{EDICOM ASP\nPeppol Access Point}}

    ERP -- POST invoice --> API
    Billing -- POST invoice --> API
    Claims -- POST invoice --> API
    Policy -- POST invoice --> API
    API --> Normalise
    Normalise --> Validate
    Validate --> Dispatch
    Dispatch -- claim + status --> Track
    Dispatch -- POST AE PINT --> Edicom
    Edicom -- ack + reference --> Dispatch
```

---

### Option B2 — Invoice Hub with Mixed Push + Pull Adapters, Bus, EDICOM Publisher

```mermaid
flowchart LR
    ERP[ERP\npush-capable]
    Billing[Billing\npush-capable]
    LegacyDB[(Legacy DB\npoll-only)]
    LegacyERP[(Legacy ERP\npoll-only)]

    subgraph Hub[Invoice Hub]
        PushAPI[Push Ingest API]
        PullAdapter1[Pull Adapter\nwatermark poller]
        PullAdapter2[Pull Adapter\nwatermark poller]
        Normalise[Normalise → CanonicalInvoice\nACL per source]
        Bus([Internal Bus\nInvoiceReceived])
        Processor[EDICOM Processor\nscale horizontally]
    end

    Track[(invint.InvoiceDispatch\nUNIQUE SourceSystem + InvoiceNumber)]
    Edicom{{EDICOM ASP\nPeppol Access Point}}

    ERP -- POST --> PushAPI
    Billing -- POST --> PushAPI
    PullAdapter1 -- poll watermark --> LegacyDB
    PullAdapter2 -- poll watermark --> LegacyERP

    PushAPI --> Normalise
    PullAdapter1 --> Normalise
    PullAdapter2 --> Normalise
    Normalise -- InvoiceReceived --> Bus
    Bus --> Processor
    Processor -- claim + status --> Track
    Processor -- POST AE PINT --> Edicom
    Edicom -- ack + reference --> Processor
```

Option B2 is the most complex scenario — designed for an insurance company with no single source of truth, where invoices come from multiple systems, some modern enough to push and some legacy that can only be polled.

**Ingest phase** — two modes running in parallel

Push path: modern systems (ERP, billing) call the Hub's ingest API directly when an invoice is ready. The Hub receives it immediately.
Pull path: legacy systems (old policy engine, legacy ERP) can't call out, so the Hub runs a dedicated poller per legacy source, using the same watermark pattern from ADR-001 to incrementally read new rows.
Both paths feed into the same normalisation step.

**Normalisation**

Each source has its own ACL adapter that maps its native schema to a single CanonicalInvoice contract. This is the critical seam — the rest of the pipeline never knows which system the invoice came from, only that it conforms to the canonical shape.

**Bus**

Once normalised, an InvoiceReceived message is published to the internal bus. From here the flow is identical regardless of whether the invoice arrived via push or pull.

**Processor**

Subscribes to InvoiceReceived, claims the row in invint.InvoiceDispatch (keyed on SourceSystem + InvoiceNumber to avoid collisions across sources), maps to AE PINT, submits to EDICOM, records the transactionId with Status = WaitingForConfirmation.

**Reconciler**

Same as A3 — polls EDICOM's status endpoint for all WaitingForConfirmation rows, writes the final Acknowledged or Failed outcome.

**Key difference from B1**

B1 only has the push path — all source systems must be capable of calling the Hub API. B2 adds the pull adapters for systems that can't, making it suitable for an insurance company with a mix of modern and legacy platforms generating invoices (policy renewals, claims settlements, premium adjustments, etc.).

The tradeoff is operational complexity: you're running push adapters, pull pollers, a bus, a processor, and a reconciler — five moving parts instead of one.

---

## Option Comparison Matrix

| Option                                | Source assumption    | Event-driven? | Complexity  | Scales horizontally? | Best fit                                    |
| ------------------------------------- | -------------------- | :-----------: | :---------: | :------------------: | ------------------------------------------- |
| **A1** Direct Extractor               | Single source DB     |  No (batch)   |     Low     |          No          | Simple, fastest to ship                     |
| **A2** Extractor + Bus + Processor    | Single source DB     |      Yes      |   Medium    |   Yes (processor)    | Medium volume, clean separation of concerns |
| **A3** Extractor + Bus + Async Status | Single source DB     |      Yes      | Medium-High |         Yes          | Async EDICOM confirmation is critical       |
| **B1** Invoice Hub (push)             | None / multiple push |    Partial    |   Medium    |         Yes          | Greenfield, all sources can push            |
| **B2** Invoice Hub (push + pull)      | None / mixed         |      Yes      |    High     |         Yes          | Heterogeneous landscape with legacy systems |
