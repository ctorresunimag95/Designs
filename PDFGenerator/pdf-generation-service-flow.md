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
    Store[(IJobStore<br/>Azure Table)]
    Queue{{IJobQueue<br/>Channel / Service Bus}}
    subgraph Worker[Worker host]
        Process[ProcessDocumentJob]
        Renderer[IDocumentRenderer<br/>IronPDF]
    end

    Caller -- "POST /document/generate" --> Submit
    Submit -- "lookup idempotentKey" --> Store
    Submit -- "create Pending" --> Store
    Submit -- "202 + jobId" --> Caller
    Submit -- "publish JobQueuedMessage" --> Queue
    Queue --> Process
    Process -- "status writes" --> Store
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
    participant Q as IJobQueue

    C->>A: POST /api/v1/document/generate (idempotentKey, payload)
    A->>A: Validate request
    A->>S: Lookup by idempotentKey
    alt Key not seen
        A->>S: Create job (Pending)
        A->>Q: Publish JobQueuedMessage(jobId)
        A-->>C: 202 Accepted + jobId + status link
    else Key seen, still in flight (Pending/Processing/Rendered/NotifyFailed)
        A-->>C: 202 Accepted + same jobId (no re-enqueue)
    else Key seen, terminal (Succeeded/Failed/DeadLettered)
        A-->>C: 200 OK + same jobId + final status
    end
```

---

## 3. Processing + callback delivery (Worker)

```mermaid
sequenceDiagram
    autonumber
    participant Q as IJobQueue
    participant W as Worker (ProcessDocumentJob)
    participant S as IJobStore
    participant R as IDocumentRenderer
    participant N as ICallbackNotifier
    actor C as Caller (callback URL)

    Q->>W: Dequeue JobQueuedMessage(jobId)
    W->>S: status = Processing (attemptCount++)
    W->>R: Render(content, options, assets)

    alt Render fails
        R-->>W: error
        W->>S: status = Failed (+ error)
        W->>N: POST errorCallbackUrl
        N->>C: ErrorCallbackPayload
    else Render succeeds
        R-->>W: PDF bytes
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
    Worker[PdfService.Worker<br/>driving adapter + composition root]
    Infra[PdfService.Infrastructure<br/>adapters]
    App[PdfService.Application<br/>use cases + ports]
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
