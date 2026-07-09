# PDF Generation Service — High-Level Design

## Context
A new C# service is needed to generate PDFs asynchronously. Callers submit a job and receive an immediate 202 Accepted; the service processes the job in the background and notifies the caller via HTTP callbacks (success or error). IronPDF is the initial rendering engine but must be replaceable without changing the public contract.

The service is split into two independently deployable components communicating through a queue, with a shared **job store** as the source of truth:

- **API** — accepts incoming jobs, validates them, checks the store for an existing job (idempotency), creates the job record in `Pending` state, returns `202 Accepted`, and writes the job content to the content store (blob).
- **Processor** — subscribes to content-store change events, picks up newly written blobs, transitions job status in the job store as it renders, deletes the content blob after processing, and POSTs the result (PDF or error) to the caller's callback URL.

The job store is **Azure Table Storage** by default but sits behind an `IJobStore` interface so it can be swapped (Redis, SQL, Cosmos DB) without touching either component. The content store is **Azure Blob Storage** by default and sits behind an `IContentStore` interface; it can be swapped for Cosmos DB (storing content as a document property) or a SQL table (storing content as `NVARCHAR(MAX)` / `VARBINARY(MAX)`) without touching the use cases.

---

## Architecture Overview

> Mermaid process-flow diagrams (submission, processing, status state machine, component dependencies) live in [pdf-generation-service-flow.md](pdf-generation-service-flow.md).

```
                                    ┌─────────────────────────────┐
Caller                              │   IJobStore                 │
  │                                 │   (Azure Table Storage /    │
  ▼                                 │    Redis / SQL — swappable) │
[API:POST /api/v1/document/generate]│                             │
  │  ── lookup by idempotentKey ───►│  exists?                    │
  │  ◄─ existing job (200) ─────────│                             │
  │  ── create job (Pending) ──────►│  job record + status        │
  │ 202 Accepted + jobId            └──────────────▲──────────────┘
  ▼                                                │ status writes
IContentStore  (Azure Blob Storage — swappable     │ Pending → Processing →
  │             for Cosmos DB / SQL table)         │ Rendered → Succeeded
  │  ── write content blob (keyed by jobId) ──►    │         ↘ Failed / NotifyFailed
  │                                                │
  │  [blob-created event / trigger]                │
  ▼                                                │
[Processor: BackgroundJobProcessor (IHostedService or Azure Function blob trigger)]──┘
  │
  ├── IContentStore  ──► read content blob → delete blob after processing
  │
  ├── IDocumentRenderer  ──► IronPdfRenderer (or future provider)
  │
  └── ICallbackNotifier  ──► HttpCallbackNotifier  (POSTs result to caller's callback URL)
         ├── success callback  (PDF bytes + metadata)
         └── error callback    (error detail)
```

---

## API Contract

### Versioning Strategy
All endpoints carry an explicit version segment. URL-path versioning (`/api/v{n}/...`) is the primary approach — it is visible, easy to route, and cacheable. A version header (`api-version`) can be supported as a secondary mechanism. Multiple versions can coexist simultaneously; old versions are deprecated with a `Sunset` response header before removal.

### Endpoints

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/v1/document/generate` | Submit a document generation job |
| `GET` | `/api/v1/document/jobs/{jobId}` | Poll job status (for callers who prefer polling over callbacks) |

---

### Request Payload Shape

The payload has two levels: top-level audit/routing fields the service infrastructure needs immediately, and a `data` object containing everything the renderer needs.

```jsonc
{
  "idempotentKey": "string",           // Caller-assigned dedup key — safe to retry
  "inputType": "Html | PlainText | HtmlUrl | RawPdf",
  "successCallbackUrl": "https://...",
  "errorCallbackUrl": "https://...",

  "data": {
    "content": "string",              // HTML string | plain text | URL | base64 PDF
    "assets": {                       // Named binary assets — substituted as data: URIs before rendering
      "logo": "<base64>",
      "signature": "<base64>"
    },
    "renderOptions": { /* see below */ },
    "header": { /* optional — see below */ },
    "footer": { /* optional — see below */ }
  }
}
```

**Top-level fields:**

| Field | Description |
|---|---|
| `idempotentKey` | Caller-assigned unique key to prevent duplicate processing |
| `inputType` | `Html`, `PlainText`, `HtmlUrl`, or `RawPdf` |
| `successCallbackUrl` | URL to POST to when the PDF is ready |
| `errorCallbackUrl` | URL to POST to if generation fails |

**`data` object fields:**

| Field | Description |
|---|---|
| `content` | HTML string, plain text, a URL to render, or base64-encoded PDF depending on `inputType` |
| `assets` | Named binary assets as a key-value map (name → base64). Referenced by name inside `content`. |
| `renderOptions` | Page size, orientation, margins, DPI, and custom metadata key-value pairs to embed in the PDF |
| `header` | Optional document header — see below |
| `footer` | Optional document footer — see below |

**Response:** `202 Accepted`

```jsonc
{
  "jobId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Pending",
  "statusLink": "/api/v1/document/jobs/3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

---

### `renderOptions` Reference

All fields are optional. Omitted fields use the renderer's defaults.

| Field | Type | Accepted values | Default | Notes |
|---|---|---|---|---|
| `paperSize` | string | `A0` `A1` `A2` `A3` `A4` `A5` `A6` `Letter` `Legal` `Tabloid` `Ledger` | `A4` | Standard paper size names |
| `orientation` | string | `Portrait` `Landscape` | `Portrait` | |
| `margins` | object | — | 20 mm all sides | See sub-fields below |
| `margins.top` | number | ≥ 0 | `20` | Millimetres |
| `margins.right` | number | ≥ 0 | `20` | Millimetres |
| `margins.bottom` | number | ≥ 0 | `20` | Millimetres |
| `margins.left` | number | ≥ 0 | `20` | Millimetres |
| `dpi` | integer | `72` `96` `150` `300` | `96` | Higher values increase output file size |
| `metadata` | object | key-value string pairs | — | Embedded as PDF document properties (Title, Author, Subject, Keywords, or custom XMP keys) |

```jsonc
"renderOptions": {
  "paperSize": "A4",
  "orientation": "Landscape",
  "margins": { "top": 15, "right": 20, "bottom": 15, "left": 20 },
  "dpi": 150,
  "metadata": {
    "Title": "Invoice #4521",
    "Author": "Billing Service",
    "Subject": "Monthly invoice"
  }
}
```

---

### Header / Footer Shape

Both `header` and `footer` share the same structure. All fields are optional.

| Field | Type | Description |
|---|---|---|
| `height` | number | Band height in millimetres |
| `left` | string | Content for the left zone (plain text or HTML fragment) |
| `center` | string | Content for the centre zone |
| `right` | string | Content for the right zone |

Token substitution at render time:

| Token | Replaced with |
|---|---|
| `{page}` | Current page number |
| `{total}` | Total page count |

```jsonc
"footer": {
  "height": 12,
  "left": "Confidential",
  "center": "Page {page} of {total}",
  "right": "Generated 2026-07-09"
}
```

---

### Callback Payloads

**Success** (`POST successCallbackUrl`):

```jsonc
{
  "jobId": "3fa85f64-...",
  "idempotentKey": "invoice-4521-v1",
  "completedAt": "2026-07-09T10:00:00Z",
  "pdf": "<base64-encoded PDF bytes>"
}
```

**Error** (`POST errorCallbackUrl`):

```jsonc
{
  "jobId": "3fa85f64-...",
  "idempotentKey": "invoice-4521-v1",
  "failedAt": "2026-07-09T10:00:01Z",
  "error": {
    "code": "RenderFailed",
    "message": "..."
  }
}
```

---

## Image Strategy

Images are a first-class concern. Three approaches can coexist:

**1. Inline base64 in HTML content**  
The caller embeds images directly as `data:` URIs inside the HTML. No extra handling needed — the renderer treats them like any other HTML content.

**2. URL references in HTML content**  
The caller references publicly accessible image URLs inside the HTML. The renderer fetches them at render time. Simplest path for logos or images already hosted somewhere.

**3. Named assets in the request payload (recommended)**  
The caller provides a named map of base64-encoded images in the `data.assets` field. Before rendering, the service substitutes each asset name for a `data:` URI so the renderer resolves them without outbound HTTP calls. This is the preferred path for images that are not publicly hosted or when deterministic rendering (no network dependency) is needed.

For plain-text input, images are not directly applicable — callers needing images should use the `Html` input type.

---

## Job Store & Status Model

The job store is the single source of truth for every job. It serves two purposes at once:

1. **Idempotency** — the API looks up the `idempotentKey` here to decide whether a job already exists.
2. **Status tracking** — the processor records lifecycle transitions here so callers can poll `GET /api/v1/document/jobs/{jobId}` and operators can audit/recover stuck jobs.

There is **one** abstraction, `IJobStore` — the earlier `IIdempotencyStore` is folded into it. Default implementation is **Azure Table Storage**; swappable for Redis, SQL, or Cosmos DB without changing the API or Processor.

### Job Record

| Field | Notes |
|---|---|
| `jobId` | Service-generated GUID; returned to caller |
| `idempotentKey` | Caller-assigned; the dedup key |
| `status` | See lifecycle below |
| `inputType` | Echoed from request for diagnostics |
| `attemptCount` | Incremented on each processing attempt; drives dead-lettering |
| `successCallbackUrl` / `errorCallbackUrl` | Where the processor delivers the result |
| `error` | Structured error detail when `Failed` / `NotifyFailed` / `DeadLettered` |
| `createdAt` / `updatedAt` / `completedAt` | Timestamps for SLA, auditing, and cleanup/TTL |

**Azure Table Storage mapping:** `PartitionKey` = `idempotentKey` (so the idempotency lookup is a single fast point query), `RowKey` = `jobId`. Table Storage limits each property to 64 KB and each entity to 1 MB, so the PDF bytes are **never stored in the table** — they live only in flight (see delivery note below).

### Status Lifecycle

| Status | Meaning | Terminal? |
|---|---|---|
| `Pending` | Job stored and published to the queue; awaiting a processor | no |
| `Processing` | Dequeued; rendering underway | no |
| `Rendered` | PDF produced; callback not yet delivered | no (intermediate) |
| `Succeeded` | Success callback delivered to the caller | ✅ |
| `Failed` | Rendering failed; error callback delivered | ✅ |
| `NotifyFailed` | PDF rendered fine but callback delivery failed; eligible for callback retry | no |
| `DeadLettered` | Exceeded max attempts; requires alert / manual intervention | ✅ |

```
Pending ──► Processing ──► Rendered ──► Succeeded
                │              │
                │              └──► NotifyFailed ──► (retry) ──► Succeeded
                │                        └──► DeadLettered
                └──► Failed
```

The `Rendered` / `NotifyFailed` split exists because rendering and callback delivery are **independent failure points**. Separating them makes a callback retry cheap: a retry that sees `Rendered`/`NotifyFailed` re-sends the result instead of re-rendering.

> **Delivery note:** the PDF is delivered in the body of the POST to the caller's `successCallbackUrl` — it is not persisted to blob storage. To let a `NotifyFailed` retry re-send without re-rendering, the processor temporarily holds the rendered bytes (in-process or a short-lived scratch blob) keyed by `jobId` until delivery is confirmed, then discards them. If a retry happens after the bytes have been purged, the job is simply re-rendered from the stored job record.

### Phasing

If a leaner first cut is preferred, ship with `Pending / Processing / Succeeded / Failed` and add `Rendered`, `NotifyFailed`, and `DeadLettered` when callback retry/dead-lettering is wired up. The enum is additive, so this does not break the contract.

---

## Component Breakdown

The solution follows **hexagonal (ports & adapters) / clean architecture**. Dependencies point inward only: the two host processes (API, Worker) and the infrastructure adapters all depend on the application core; the core depends on nothing outward. This is what lets every external concern — content store, job store, PDF engine, callback transport — be swapped without touching a use case.

### Domain (`PdfService.Domain`)
The innermost layer. Entities and value objects with no framework or infrastructure references: the `Job` entity, the `JobStatus` enum and its allowed transitions, `RenderOptions`, and the header/footer model. Zero dependencies.

### Application (`PdfService.Application`)
The core. Holds the **use cases** and the **ports** they depend on. Depends only on `Domain`. All use cases are host-agnostic — the API and Worker are simply driving adapters that invoke them.

#### Use Cases

**Driven by the API host:**

| Use Case | Responsibility | Ports used |
|---|---|---|
| `SubmitDocumentJob` | Validate request; look up `idempotentKey` in the job store (idempotency check); create the job record in `Pending` state; write serialised job content (HTML, assets, render options) to the content store keyed by `jobId`; return the `jobId` to the caller. | `IJobStore` (read + write), `IContentStore` (write) |
| `GetJobStatus` | Look up a job by `jobId` and return its current status and metadata. Used by callers who prefer polling over callbacks. | `IJobStore` (read) |

**Driven by the Worker host:**

| Use Case | Responsibility | Ports used |
|---|---|---|
| `ProcessDocumentJob` | Read the content blob from the content store; transition status to `Processing`; render the PDF; delete the content blob; deliver the result to the caller's callback URL; write the final status transition (`Succeeded`, `Failed`, `NotifyFailed`, or `DeadLettered`). | `IContentStore` (read + delete), `IJobStore` (write), `IDocumentRenderer`, `ICallbackNotifier` |

#### Output Ports (driven — interfaces the core declares, infrastructure implements)

| Port | Responsibility | Default adapter |
|---|---|---|
| `IJobStore` | Persist and query job records; idempotency lookup by `idempotentKey`; status transitions. | `AzureTableJobStore` |
| `IContentStore` | Write, read, and delete the serialised job content blob between submission and processing. | `AzureBlobContentStore` |
| `IDocumentRenderer` | Render HTML / plain-text / URL / raw-PDF content to a PDF byte array. | `IronPdfRenderer` |
| `ICallbackNotifier` | POST the success payload (PDF bytes + metadata) or error payload to the caller's callback URL. | `HttpCallbackNotifier` |

#### Input Ports (driving — optional)

Optional `ISubmitDocumentJob` / `IProcessDocumentJob` / `IGetJobStatus` interfaces. Introduce only when a host or test needs to mock the use case boundary. Default: inject the use case classes directly.

### Contracts (`PdfService.Contracts`)
The **public, caller-facing wire contract** — the only contract a third-party client needs. Shippable as a client NuGet. Plain DTOs and enums, no behavior, no infrastructure refs:

- `GenerateDocumentRequest` / `GenerateDocumentResponse` — the POST payload and 202 body (used by the **API**).
- `JobStatusResponse` — the `GET /jobs/{jobId}` poll body (used by the **API**).
- `SuccessCallbackPayload` / `ErrorCallbackPayload` — what the Worker POSTs back to the caller's callback URLs (used by the **Worker**).

Both hosts reference this, but for opposite ends of the conversation: the API maps request/response, the Worker builds callback payloads. Mapping between these DTOs and the `Domain` model happens at the edges (controllers / a thin mapper).

### Infrastructure (`PdfService.Infrastructure`) — the adapters
Concrete implementations of the output ports. Depends on `Application` + `Domain`:

- **IronPdfRenderer** (`IDocumentRenderer`) — the only place IronPDF is referenced. Handles all four input types. Translates header/footer options into IronPDF's native header/footer API. Injects named assets as data URIs before handing content to the engine.
- **AzureBlobContentStore** (`IContentStore`) — writes serialised job content as a blob on submission; reads and deletes it on processing. Blobs are keyed by `jobId` under a dedicated container. Fires a blob-created event (via Azure Event Grid or built-in Storage Events) that triggers the Worker. Swappable for a Cosmos DB document store or a SQL table (`NVARCHAR(MAX)` / `VARBINARY(MAX)`) by adding another adapter. An `InMemoryContentStore` backs unit/integration tests.
- **AzureTableJobStore** (`IJobStore`) — Azure Table Storage. Holds the full job record and status (see Job Store & Status Model). `PartitionKey` = `idempotentKey`, `RowKey` = `jobId`. Swappable for Redis, SQL, or Cosmos DB by adding another adapter. (An `InMemoryJobStore` backs unit/integration tests.)
- **HttpCallbackNotifier** (`ICallbackNotifier`) — sends HTTP POST to callback URLs. Success payload includes the generated PDF bytes and job metadata. Error payload includes the job ID and a structured error description.

### API host (`PdfService.Api`) — driving adapter + composition root
Thin ASP.NET Core controllers: map `Contracts` DTOs, invoke `SubmitDocumentJob`, return 202. No rendering logic. This project is one of the two **composition roots** — it wires the DI container, binding output ports to the Infrastructure adapters.

Can also be implemented as an **Azure Function with HTTP trigger** instead of a traditional ASP.NET Core service — the use case and port bindings remain unchanged; only the hosting envelope differs.

### Worker host (`PdfService.Worker`) — driving adapter + composition root
Subscribes to blob-created events from `IContentStore` and invokes `ProcessDocumentJob`. For each event:

1. Read the content blob from `IContentStore` (identified by `jobId`).
2. Mark `Processing` in `IJobStore` (increment `attemptCount`).
3. Call the renderer. On failure → `Failed`, POST error callback, **delete the content blob**.
4. On success → `Rendered` (holding the bytes in flight), POST the PDF to the success callback, **delete the content blob**.
5. On callback success → `Succeeded`. On callback failure → `NotifyFailed`; retry up to the configured limit, then `DeadLettered`.

Blob deletion happens as soon as the job leaves `Processing` (step 3 or 4) — the blob is never needed again after the content has been read and the render attempted. Every status transition is written through `IJobStore` so status is durable and pollable. The second composition root — can be co-hosted with the API in one process or deployed separately for independent scaling; either way the wiring is identical because both bind the same ports.

Can also be implemented as an **Azure Function with Blob Storage trigger (via Event Grid)** instead of a long-running hosted service — the function is invoked once per blob-created event, executes the use case, and the port bindings remain unchanged; deployment and scaling differ but the logic is identical.

---

## Idempotency Rules

The API resolves idempotency with a single `IJobStore` lookup by `idempotentKey` (no separate store).

| Scenario | Behavior |
|---|---|
| Key not seen before | Create job in `Pending`, write content blob, return 202 + new jobId |
| Key seen, status `Pending` / `Processing` / `Rendered` / `NotifyFailed` | Return 202 + same jobId, do not re-write blob (job still in flight) |
| Key seen, status `Succeeded` / `Failed` / `DeadLettered` | Return 200 + same jobId + final status, do not reprocess |

Network retries and duplicate submissions from the caller are safe.

---

## Project Structure

Hexagonal layering. Each project may depend only on those listed under it — arrows point inward toward `Domain`.

```
PdfService/
├── PdfService.Domain/         — Entities & value objects: Job, JobStatus (+ transitions),
│                                 RenderOptions, Header/Footer. ZERO dependencies.
├── PdfService.Application/     — Use cases (SubmitDocumentJob, ProcessDocumentJob),
│                                 ports (IJobStore, IContentStore, IDocumentRenderer,
│                                 ICallbackNotifier). → Domain
├── PdfService.Contracts/       — Public wire DTOs (request/response, callback payloads).
│                                 Shippable as a client NuGet. → (none)
├── PdfService.Infrastructure/  — Adapters: IronPdfRenderer, AzureBlobContentStore,
│                                 AzureTableJobStore, HttpCallbackNotifier. → Application, Domain
├── PdfService.Api/             — Driving adapter + composition root: controllers,
│                                 middleware, versioning. → Application, Infrastructure, Contracts
├── PdfService.Worker/          — Driving adapter + composition root: BackgroundJobProcessor
│                                 hosted service. → Application, Infrastructure, Contracts
└── PdfService.Tests/           — Unit & integration tests (InMemoryJobStore, WireMock.Net)
```

**Dependency rule:** `Domain` knows nothing; `Application` knows only `Domain`; `Infrastructure` and the two hosts know `Application`. Only the hosts (`Api`, `Worker`) wire DI — they are the composition roots. `Contracts` stands alone with no internal references so clients can consume it in isolation.

**Notes on the split:**
- *Ports vs public DTOs are separate concerns.* Ports (interfaces) live in `Application`; caller-facing DTOs live in `Contracts`. This keeps the public API shape from leaking into the core and lets the wire contract version independently.
- *Optional merge.* If domain logic stays thin, `Domain` can be folded into `Application` as a single `Core` project (5 projects instead of 6). Don't split finer than this.
- The API and Worker can run as one host for simplicity or as two for independent scaling — the binding is identical since both are just composition roots over the same ports.
- *Hosting flexibility:* Both can be implemented as traditional ASP.NET Core / `IHostedService` hosts (as described) or as serverless Azure Functions (HTTP trigger for API, Blob/Event Grid trigger for Worker). The hexagonal structure isolates infrastructure concerns so a switch between models requires only a new driving adapter and composition root; the application core, use cases, and ports remain unchanged.

---

## Technology Choices and Swap Paths

| Concern | Default (v1) | Future swap |
|---|---|---|
| Content store (job payload hand-off) | Azure Blob Storage via `AzureBlobContentStore`; blob-created event triggers Worker | Cosmos DB document store / SQL table (`NVARCHAR(MAX)`) — reimplement `IContentStore`; swap trigger to change-feed / polling |
| Job + idempotency store | Azure Table Storage via `AzureTableJobStore` | Redis / SQL / Cosmos DB — reimplement `IJobStore` |
| PDF engine | IronPDF via `IronPdfRenderer` | Any library — new `IDocumentRenderer` implementation + DI rebind |
| Callback delivery | Single-attempt `HttpClient` POST | Add Polly retry/circuit-breaker to `ICallbackNotifier`; status drives `NotifyFailed` → `DeadLettered` |
| Worker trigger | Blob Storage event (Event Grid) | Any event source — swap the driving adapter only |

---

## Verification Approach

1. **Unit tests** — mock all interfaces; test idempotency rules, input-type routing, header/footer option mapping, and asset injection logic independently
2. **Integration tests** — run a full in-process pipeline with `InMemoryContentStore` and `InMemoryJobStore`; use WireMock.Net as the callback sink and assert the correct payload arrives; assert the content entry is deleted from `IContentStore` after processing completes
3. **Idempotency test** — submit the same `idempotentKey` concurrently from multiple threads; assert only one job is created in the store, one PDF is generated, and one callback fires
4. **Status lifecycle test** — drive a job through the store and assert transitions: `Pending → Processing → Rendered → Succeeded` on the happy path; `→ Failed` on a render error; `Rendered → NotifyFailed → Succeeded` when the first callback attempt fails then a retry succeeds; `→ DeadLettered` once `attemptCount` exceeds the limit
5. **Manual smoke test** — POST an HTML job with an embedded image asset, a header, and a footer; confirm 202 response and inspect the callback PDF visually