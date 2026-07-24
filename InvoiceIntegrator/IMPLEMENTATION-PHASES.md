# Invoice Integrator — Implementation Phases

> **Context:** EDICOM sandbox credentials and final API contract are pending. Phases are ordered so that the highest-value, most independent work proceeds first and external dependencies only block the later phases.

---

## Phase 1 — Integration Table + Payload Builder

**Goal:** prove that all 51 AE PINT mandatory fields can be reliably collected from internal systems. No EDICOM dependency.

### Deliverables

- `invint.InvoiceIntegration` table migration — full schema below

#### Table: `invint.InvoiceIntegration`

| Column              | Type              | Nullable | Default            | Purpose                                                                                   |
| ------------------- | ----------------- | -------- | ------------------ | ----------------------------------------------------------------------------------------- |
| `Id`                | `BIGINT IDENTITY` | No       | —                  | Surrogate PK                                                                              |
| `InvoiceNumber`     | `NVARCHAR(100)`   | No       | —                  | Business key; unique constraint enforces idempotency                                      |
| `Status`            | `NVARCHAR(30)`    | No       | `'Pending'`        | State machine value; check constraint enforces valid values                               |
| `CreatedAt`         | `DATETIME2`       | No       | `SYSUTCDATETIME()` | When intake first upserted the row                                                        |
| `UpdatedAt`         | `DATETIME2`       | No       | `SYSUTCDATETIME()` | Updated on every state transition                                                         |
| `ClaimedAt`         | `DATETIME2`       | Yes      | `NULL`             | Set when processor claims the row; drives stale-claim recovery                            |
| `SubmittedAt`       | `DATETIME2`       | Yes      | `NULL`             | Set after successful POST to EDICOM; drives reconciler grace window                       |
| `TransactionId`     | `NVARCHAR(200)`   | Yes      | `NULL`             | EDICOM-assigned transaction ID; written with `WaitingConfirmation`                        |
| `EdicomReference`   | `NVARCHAR(200)`   | Yes      | `NULL`             | EDICOM's document reference, returned on acknowledgment                                   |
| `ProcessedAt`       | `DATETIME2`       | Yes      | `NULL`             | When the row reached a terminal state                                                     |
| `ReconcileAttempts` | `INT`             | No       | `0`                | Incremented by reconciler on each poll with no terminal status                            |
| `LastReconciledAt`  | `DATETIME2`       | Yes      | `NULL`             | Timestamp of last reconciler poll                                                         |
| `StaleResetCount`   | `INT`             | No       | `0`                | Incremented each time stale claim recovery resets a stuck `Claimed` row; use to detect invoices that have been retried multiple times without reaching EDICOM |
| `FailureReason`     | `NVARCHAR(50)`    | Yes      | `NULL`             | Internal code: `ValidationFailed`, `SubmitRejected`, `MaxAttemptsExceeded`, `CircuitOpen` |
| `ErrorCode`         | `NVARCHAR(100)`   | Yes      | `NULL`             | EDICOM or AE PINT error code (e.g. `PEPPOL-EN16931-R001`)                                 |
| `ErrorMessage`      | `NVARCHAR(2000)`  | Yes      | `NULL`             | Human-readable error detail from EDICOM or validator                                      |

#### Artifacts

- AE PINT domain model — a typed object covering all 51 mandatory fields defined in [EDICOM-Info.md](EDICOM-Info.md)
- Data gathering service
  - Source DB → core invoice fields (seller, buyer, lines, totals)
  - Other DBs / Config → supplemental and config data
  - External APIs → enrichment data (tax codes, vendor master, etc.)
- Payload validator — all mandatory fields present, format rules enforced (TRN length, ISO currency code, AE PINT tax category constraints, bitmask flags)
- Serializer to the final JSON shape behind a named adapter boundary (`EdicomPayloadMapper`) so field name changes from EDICOM only require updating the mapper
- Unit tests per data source + integration tests against real source data

### Exit criteria

Given an `invoiceNumber`, the service produces a fully validated, serializable AE PINT payload with all 51 fields populated — no EDICOM call required.

---

## Phase 2 — Processor Consumer + EDICOM Client

**Goal:** end-to-end processing pipeline. Runs against a mock until sandbox credentials arrive; swapping in the real client requires no processor changes.

### Deliverables

- `IEdicomClient` interface with a stub/mock implementation (returns a fake `transactionId`)
- OAuth2 token service — client credentials flow, token cache, proactive refresh ~30 s before expiry
- Processor consumer
  - Consume queue message (invoiceNumber)
  - Atomic claim row → `Status = Claimed`
  - Invoke Phase 1 data gatherer + validator
  - Acquire token via token service
  - Submit via `IEdicomClient`
  - Write `transactionId` + `Status = WaitingConfirmation`
  - Acknowledge message only after successful write
- Polly retry + circuit breaker on the EDICOM client (3 attempts, exponential backoff; circuit opens after 5 consecutive failures)
- Message broker consumer setup + DLQ configuration (`MaxDeliveryCount = 5`)
- Real `EdicomClient` implementation wired up once sandbox is available — processor untouched

### Exit criteria

Processor runs end-to-end with the mock client. All state transitions write correctly. Retry, circuit breaker, and DLQ paths exercised in tests. Real client passes the same test suite against sandbox.

---

## Phase 3 — Completion: Webhook + Reconciler

**Goal:** invoices reach a terminal state (`Acknowledged` / `Failed`) via both paths.

**Dependency:** EDICOM must confirm webhook payload shape, status poll response format, and auth method.

### Deliverables

- Webhook handler
  - Inbound auth verification (HMAC signature / IP allowlist)
  - Idempotent status update by `transactionId`
  - Returns `200 OK` to EDICOM
- Polling reconciler
  - Query `Status = WaitingConfirmation AND SubmittedAt < NOW() - GraceMinutes`
  - `GET /status/{transactionId}` per stale row
  - Terminal → write `Acknowledged` / `Failed`
  - Still pending → increment `ReconcileAttempts`
  - Max attempts reached → write `Failed (MaxAttemptsExceeded)` + alert
- Alert integration on reconciler exhaustion and DLQ depth > 0

### Exit criteria

An invoice reaches `Acknowledged` or `Failed` via both the webhook path and the reconciler fallback path in a staging environment.

---

## Phase 4 — Triggers

**Goal:** intake layer that drives the full pipeline from real events.

### Deliverables

- Timer cron
  - Source DB discovery query (candidates from last N hours)
  - Batch upsert + publish — one message per invoice
- HTTP `/trigger/invoice` endpoint
  - Single upsert + publish
  - Returns `202 Accepted`
- Idempotency validation — duplicate trigger events for the same invoice collapse to one `Pending` row and one queue message

### Exit criteria

Both trigger types produce correct `Pending` rows and queue messages that the Phase 2 processor picks up and drives to a terminal state.

---

## Phase dependency summary

```

Phase 1 ──────────────────────────────────────────────────────► (no external deps)
Phase 2 ── depends on Phase 1 ────────────────────────────────► (mock until sandbox)
Phase 3 ── depends on Phase 2 · blocked on EDICOM contract ───►
Phase 4 ── depends on Phase 1 · can overlap with Phase 2/3 ───►

```

Phases 2 and 4 can proceed in parallel once Phase 1 is done. Phase 3 is the only phase gated on an external deliverable (EDICOM sandbox + confirmed contract).

```

```
