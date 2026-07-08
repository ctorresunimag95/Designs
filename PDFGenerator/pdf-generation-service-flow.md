# PDF Generation Service — Process Flow

Mermaid diagrams for the PDF Generation Service. See [pdf-generation-service-design.md](pdf-generation-service-design.md) for the full design.

---

## 1. End-to-end flow (high level)

```mermaid
flowchart LR
    Caller([Caller])
    subgraph API[API host]
        Submit[SubmitDocumentJob]
    end
    JobStore[(IJobStore<br/>Azure Table)]
    ContentStore[(IContentStore<br/>Azure Blob Storage<br/>— swappable for<br/>Cosmos DB / SQL)]
    subgraph Worker[Worker host]
        Process[ProcessDocumentJob]
        Renderer[IDocumentRenderer<br/>IronPDF]
    end

    Caller -- "POST /document/generate" --> Submit
    Submit -- "lookup idempotentKey" --> JobStore
    Submit -- "create Pending" --> JobStore
    Submit -- "202 + jobId" --> Caller
    Submit -- "write content blob (jobId)" --> ContentStore
    ContentStore -- "blob-created event / trigger" --> Process
    Process -- "read content blob" --> ContentStore
    Process -- "delete content blob" --> ContentStore
    Process -- "status writes" --> JobStore
    Process --> Renderer
    Process -- "POST PDF / error" --> Caller
```

---

## 2. Submission + idempotency (API)

```mermaid
sequenceDiagram
    autonumber
    actor C as Caller
    participant A as API (SubmitDocumentJob)
    participant S as IJobStore
    participant B as IContentStore (Blob)

    C->>A: POST /api/v1/document/generate (idempotentKey, payload)
    A->>A: Validate request
    A->>S: Lookup by idempotentKey
    alt Key not seen
        A->>S: Create job (Pending)
        A->>B: Write content blob keyed by jobId
        Note over B: blob-created event fires → triggers Worker
        A-->>C: 202 Accepted + jobId + status link
    else Key seen, still in flight (Pending/Processing/Rendered/NotifyFailed)
        A-->>C: 202 Accepted + same jobId (no re-write)
    else Key seen, terminal (Succeeded/Failed/DeadLettered)
        A-->>C: 200 OK + same jobId + final status
    end
```

---

## 3. Processing + callback delivery (Worker)

```mermaid
sequenceDiagram
    autonumber
    participant B as IContentStore (Blob)
    participant W as Worker (ProcessDocumentJob)
    participant S as IJobStore
    participant R as IDocumentRenderer
    participant N as ICallbackNotifier
    actor C as Caller (callback URL)

    B->>W: blob-created event (jobId)
    W->>B: Read content blob (jobId)
    W->>S: status = Processing (attemptCount++)

    W->>R: Render(content, options, assets)

    alt Render fails
        R-->>W: error
        W->>B: Delete content blob (jobId)
        W->>S: status = Failed (+ error)
        W->>N: POST errorCallbackUrl
        N->>C: ErrorCallbackPayload
    else Render succeeds
        R-->>W: PDF bytes
        W->>B: Delete content blob (jobId)
        W->>S: status = Rendered (hold bytes in flight)
        W->>N: POST successCallbackUrl
        N->>C: SuccessCallbackPayload (PDF + metadata)
        alt Callback delivered
            C-->>N: 2xx
            W->>S: status = Succeeded
        else Callback delivery fails
            C-->>N: error / timeout
            W->>S: status = NotifyFailed
            Note over W,S: Retry up to the limit (re-send held bytes or re-render) then DeadLetter once exhausted
        end
    end
```

---

## 4. Job status state machine

```mermaid
stateDiagram-v2
    [*] --> Pending: job created + enqueued
    Pending --> Processing: dequeued by Worker
    Processing --> Failed: render error
    Processing --> Rendered: PDF produced

    Rendered --> Succeeded: callback delivered
    Rendered --> NotifyFailed: callback delivery failed

    NotifyFailed --> Succeeded: retry delivered
    NotifyFailed --> DeadLettered: attempts exhausted

    Succeeded --> [*]
    Failed --> [*]
    DeadLettered --> [*]

    note right of Rendered
        PDF bytes held in flight
        so a retry need not re-render
    end note
```

---

## 5. Component dependencies (hexagonal)

Arrows point in the direction of the dependency — everything points inward toward `Domain`.

```mermaid
flowchart TD
    Api[PdfService.Api<br/>driving adapter + composition root]
    Worker[PdfService.Worker<br/>driving adapter + composition root<br/>triggered by blob-created event]
    Infra[PdfService.Infrastructure<br/>adapters:<br/>AzureBlobContentStore · AzureTableJobStore<br/>IronPdfRenderer · HttpCallbackNotifier]
    App[PdfService.Application<br/>use cases + ports:<br/>IContentStore · IJobStore<br/>IDocumentRenderer · ICallbackNotifier]
    Domain[PdfService.Domain<br/>entities + value objects]
    Contracts[PdfService.Contracts<br/>public wire DTOs]

    Api --> App
    Api --> Infra
    Api --> Contracts
    Worker --> App
    Worker --> Infra
    Worker --> Contracts
    Infra --> App
    Infra --> Domain
    App --> Domain
```
