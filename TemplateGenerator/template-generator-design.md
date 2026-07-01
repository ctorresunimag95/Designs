# Template Generator Service — High-Level Design

> The validation engine (rule catalog, applicability matrix, Factory + Strategy implementation, example strategies) is detailed in its own document: [template-generator-validation-design.md](template-generator-validation-design.md).

## Context

A new C# service is needed to let users **author reusable templates** through a drag‑and‑drop UI (UI out of scope here). A template is not a fixed questionnaire — it is a structured document definition that is later exported to some output format (export also out of scope). The backend's job is to **store, version, and validate template definitions**, and to expose a **validation engine** that is extensible enough to grow new rule types without touching existing code.

A template is composed of:

- **Template** — the top-level aggregate (name, version, status, owning tenant).
- **Sections** — ordered groupings inside a template.
- **Entries** (a.k.a. lines) — ordered items inside a section. An entry is one of:
  - **Info** — static descriptive text, no user input.
  - **Input** — a field the end user will fill in later. Input types: `Text`, `Numeric`, `Checkbox`, `SingleSelect` (dropdown), `MultiSelect`.
- **Validation rules** — attached to inputs. Drawn from a **predefined catalog** (`Required`, `Min`, `Max`, `AtLeast`, `AtMost`, `Pattern`, …) plus **custom cross-field rules** (e.g. `RequiredIf`). Which rules are _allowed_ on an input depends on the input's **category** — a `Text` field may carry `Required`/`Min`/`Max` (length) but never `AtLeast`, which only makes sense for a `MultiSelect`.

There are therefore **two distinct validation concerns**, and the design keeps them separate:

1. **Authoring/structural validation** — when a template is saved, is each rule _applicable_ to the input it's attached to, and are its parameters well-formed? (e.g. reject `AtLeast` on a `Text` field; reject `Min > Max`.)
2. **Response/runtime validation** — when an end user fills a template, the UI **dynamically translates the rule set** for client-side validation (the `validations[]` array in the template payload is the schema the UI reads directly). When the form is submitted, the backend **re-validates** the same rules before persisting the `FormSubmission`.

The store is **Azure Cosmos DB** by default (a template is a self-contained JSON aggregate — a natural document fit) but sits behind an `ITemplateRepository` interface so it can be swapped (Azure Table Storage, SQL, in-memory) without touching the core.

---

## Architecture Overview

```
 ┌──────────────────────────────── AUTHORING FLOW ────────────────────────────────────┐
 │                                              ┌─────────────────────────────────┐   │
 │  Author (drag & drop UI, out of scope)       │  ITemplateRepository            │   │
 │    │                                         │  Cosmos DB — templates          │   │
 │    ▼                                         │  (swappable via interface)      │   │
 │  [POST /api/v1/templates]                    └──────────────▲──────────────────┘   │
 │    │  ── SaveTemplate / PublishTemplate ───────────────────►│ write aggregate      │
 │    │        └── ITemplateValidator ──► validates definition (applicability+params) │
 │    │  ◄── 400 validation errors  |  201 template                                   │
 └────────────────────────────────────────────────────────────────────────────────────┘

 ┌──────────────────────────────── SUBMISSION FLOW ───────────────────────────────────┐
 │                                              ┌─────────────────────────────────┐   │
 │  End User (fills form via UI)                │  IFormSubmissionRepository      │   │
 │    │  GET /api/v1/templates/{id}             │  Cosmos DB — submissions        │   │
 │    │  ◄── template + rules                   │  (swappable via interface)      │   │
 │    │     (UI renders validations client-side  └──────────────▲──────────────────┘   │
 │    │      from the validations[] in payload)                 │ write submission     │
 │    ▼                                                         │                      │
 │  [POST /api/v1/templates/{id}/submissions] ─────────────────►│                     │
 │    │  ── SubmitForm ──► ISubmissionValidator                  │                     │
 │    │        └── evaluates answers via same IValidationRule    │                     │
 │    │            strategies (Evaluate path)                    │                     │
 │    │  ◄── 422 field-level errors  |  201 submissionId                               │
 └────────────────────────────────────────────────────────────────────────────────────┘
```

Both validators are **domain services** built on the same **Factory + Strategy** engine — adding a rule means one strategy class plus one registration line. The full design (rule catalog, applicability matrix, `ITemplateValidator`, `ISubmissionValidator`, example strategies) lives in [template-generator-validation-design.md](template-generator-validation-design.md).

---

## Domain Model

```
Template (aggregate root)
 ├─ id, tenantId, name, description
 ├─ version, status (Draft | Published | Archived)
 ├─ createdAt / updatedAt / publishedAt
 └─ Sections[]            (ordered)
      ├─ id, title, order
      └─ Entries[]        (ordered)
           ├─ id, order
           ├─ kind = Info | Input
           ├─ (Info)  text
           └─ (Input) inputType, key, label, placeholder, options[], validations[]
```

### Entry kinds & input types

| `inputType`    | Stores               | Notes                |
| -------------- | -------------------- | -------------------- |
| `Text`         | string               | free text            |
| `Numeric`      | number               | integer or decimal   |
| `Checkbox`     | bool                 | single boolean       |
| `SingleSelect` | one option value     | requires `options[]` |
| `MultiSelect`  | set of option values | requires `options[]` |

`key` is a stable, machine-friendly identifier unique **within a template** — it is the handle cross-field rules (`RequiredIf`) and the future export step refer to. `label` is the human-facing caption.

### Validation rules on inputs

Each input carries a list of validation rules drawn from a **predefined catalog** — `Required`, `Min`, `Max`, `Pattern`, `AtLeast`, `AtMost` — plus **custom cross-field rules** such as `RequiredIf`. Which rules are _allowed_ on an input depends on the input's **category**: a `Text` field may carry `Required`/`Min`/`Max`/`Pattern` but never `AtLeast`, which only makes sense for a `MultiSelect`. This applicability is enforced at authoring time.

The full applicability matrix, rule parameters, and the extensible Factory + Strategy engine that evaluates them are detailed in [template-generator-validation-design.md](template-generator-validation-design.md).

### Form submission

When an end user fills a published template, the resulting answers are stored as a `FormSubmission` aggregate:

```
FormSubmission (aggregate)
 ├─ id, tenantId, templateId, templateVersion
 ├─ submittedAt, status (Submitted | Reviewed)
 └─ Answers (map)
      └─ key → value  (typed: string | number | bool | string[])
            key matches an Input's key within the referenced template version
```

`templateVersion` is locked at submission time so each submission is permanently anchored to the exact rule set it was validated against.

---

## Sample Payload

`POST /api/v1/templates` — create/save a draft template.

```json
{
  "tenantId": "acme-insurance",
  "name": "Auto Insurance Quote Request",
  "description": "Collected to produce an auto policy quote",
  "sections": [
    {
      "title": "Applicant Details",
      "order": 1,
      "entries": [
        {
          "kind": "Info",
          "order": 1,
          "text": "Provide the primary driver's details. All required fields must be completed to receive a quote."
        },
        {
          "kind": "Input",
          "order": 2,
          "inputType": "Text",
          "key": "applicant_name",
          "label": "Full name",
          "placeholder": "Jane Doe",
          "validations": [
            { "rule": "Required", "message": "Name is required." },
            { "rule": "Max", "params": { "value": 100 } }
          ]
        },
        {
          "kind": "Input",
          "order": 3,
          "inputType": "Numeric",
          "key": "driver_age",
          "label": "Primary driver age",
          "validations": [
            { "rule": "Required" },
            { "rule": "Min", "params": { "value": 16 } },
            { "rule": "Max", "params": { "value": 99 } }
          ]
        }
      ]
    },
    {
      "title": "Vehicle & Coverage",
      "order": 2,
      "entries": [
        {
          "kind": "Input",
          "order": 1,
          "inputType": "Numeric",
          "key": "vehicle_value",
          "label": "Estimated vehicle value (USD)",
          "validations": [
            { "rule": "Required" },
            { "rule": "Min", "params": { "value": 1000 } },
            { "rule": "Max", "params": { "value": 500000 } }
          ]
        },
        {
          "kind": "Input",
          "order": 2,
          "inputType": "MultiSelect",
          "key": "coverage_options",
          "label": "Coverage options",
          "options": [
            { "value": "liability", "label": "Liability" },
            { "value": "collision", "label": "Collision" },
            { "value": "comprehensive", "label": "Comprehensive" },
            { "value": "roadside", "label": "Roadside assistance" },
            { "value": "rental", "label": "Rental reimbursement" }
          ],
          "validations": [
            {
              "rule": "AtLeast",
              "params": { "value": 1 },
              "message": "Select at least one coverage option."
            },
            { "rule": "AtMost", "params": { "value": 5 } }
          ]
        },
        {
          "kind": "Input",
          "order": 3,
          "inputType": "Checkbox",
          "key": "has_prior_accidents",
          "label": "Any accidents in the last 5 years?",
          "validations": [{ "rule": "Required" }]
        },
        {
          "kind": "Input",
          "order": 4,
          "inputType": "Text",
          "key": "accident_details",
          "label": "Describe prior accidents",
          "validations": [
            {
              "rule": "RequiredIf",
              "params": {
                "field": "has_prior_accidents",
                "operator": "equals",
                "value": true
              },
              "message": "Please describe the accidents you reported."
            },
            { "rule": "Max", "params": { "value": 500 } }
          ]
        }
      ]
    }
  ]
}
```

**Response — success:** `201 Created` with the persisted template (server-assigned `id`, `version: 1`, `status: "Draft"`).

**Response — structural validation failure:** `400 Bad Request` with a list of pinpointed errors:

```json
{
  "error": "TEMPLATE_INVALID",
  "errors": [
    {
      "path": "sections[1].entries[1]",
      "key": "coverage_options",
      "rule": "AtLeast",
      "message": "AtLeast is not applicable to a MultiSelect with no options defined."
    },
    {
      "path": "sections[0].entries[2]",
      "key": "driver_age",
      "rule": "Min/Max",
      "message": "Min (99) must not exceed Max (16)."
    }
  ]
}
```

### Submission sample

`POST /api/v1/templates/{id}/submissions` — submit filled answers against the auto insurance quote template.

```json
{
  "tenantId": "acme-insurance",
  "answers": {
    "applicant_name": "Jane Doe",
    "driver_age": 28,
    "vehicle_value": 25000,
    "coverage_options": ["liability", "collision"],
    "has_prior_accidents": false,
    "accident_details": null
  }
}
```

**Response — success:** `201 Created` with `submissionId`, `templateId`, `templateVersion`, and `submittedAt`.

**Response — runtime validation failure:** `422 Unprocessable Entity` with field-level errors:

```json
{
  "error": "SUBMISSION_INVALID",
  "errors": [
    { "key": "driver_age",       "rule": "Min",        "message": "Must be at least 16." },
    { "key": "coverage_options", "rule": "AtLeast",    "message": "Select at least one coverage option." },
    { "key": "accident_details", "rule": "RequiredIf", "message": "Please describe the accidents you reported." }
  ]
}
```

---

## Validation Engine (summary)

Validation is a **domain service** (`ITemplateValidator`) built on a **Factory + Strategy** engine:

- **Strategy** — `IValidationRule`, one implementation per rule kind. Each strategy owns its own applicability (`AppliesTo`), validates its parameters at authoring time (`ValidateDefinition`), and evaluates an answer at response time (`Evaluate`).
- **Factory** — `IValidationRuleFactory` resolves a rule's `Kind` to its strategy from the registered set.
- **Orchestrators** — two domain services over the same engine:
  - `ITemplateValidator` — authoring path; walks the template definition, checks applicability + params, accumulates `path`/`key`/`rule`/`message` errors → `400`.
  - `ISubmissionValidator` — runtime path; walks the template's inputs, maps each answer into an `EvaluationContext` (with sibling answers for cross-field rules), calls `Evaluate`, accumulates field-level errors → `422`.

The payoff is **open/closed**: adding a rule is one strategy class plus one registration line — no edits to either validator, the factory, use cases, or the store. Interfaces, example strategies (`MaxRule`, `RequiredIfRule`), both orchestrators, and the testing approach are in [template-generator-validation-design.md](template-generator-validation-design.md).

---

## Template Store & Versioning

A template is a **single aggregate** — read and written as one whole — which is exactly the document-database sweet spot, hence Cosmos DB as the default.

### Cosmos DB mapping

| Concern       | Choice                                                                                   |
| ------------- | ---------------------------------------------------------------------------------------- |
| Container     | `templates`                                                                              |
| Document      | the entire template aggregate (sections, entries, rules) as one JSON doc                 |
| Partition key | `/tenantId` — co-locates a tenant's templates, scopes queries and throughput             |
| Document `id` | `{templateId}:{version}` — every version is its own immutable document                   |
| Indexing      | default; add composite index on `(tenantId, name, version)` for "latest version" lookups |

### Versioning & status

| Status      | Meaning                             | Mutable?                                                         |
| ----------- | ----------------------------------- | ---------------------------------------------------------------- |
| `Draft`     | being authored                      | yes — upserts overwrite in place                                 |
| `Published` | locked for use / export             | no — edits create a **new version** (`version + 1`, new `Draft`) |
| `Archived`  | retired; retained for audit/history | no                                                               |

Publishing is therefore copy-on-write: a `Published` template is never mutated, so any already-collected responses or exports remain anchored to the exact definition they were created against.

> **Why not Azure Table Storage by default?** A template is hierarchical (sections → entries → rules) and is always handled as a unit. Table Storage would force either a JSON blob in one property (losing queryability and risking the 64 KB/property, 1 MB/entity limits for large templates) or an awkward multi-entity decomposition. Cosmos stores the nested aggregate natively and queries into it with SQL. Table Storage remains a valid swap for cost-sensitive, low-query deployments — hence the `ITemplateRepository` seam.

---

## Component Breakdown

Clean / hexagonal architecture. Dependencies point inward only; the host and infrastructure adapters depend on the application core; the core depends on nothing outward. This is what lets the store and the rule set be swapped without touching a use case.

### Domain (`TemplateGenerator.Domain`)

The core. Zero outward dependencies — no framework, no infrastructure. Holds both the model **and the business rules**:

- **Entities & value objects:** `Template`, `Section`, `Entry` (with `Info`/`Input` shapes), `InputType` enum, `OptionDefinition`, `RuleDefinition`, `TemplateStatus` and its allowed transitions.
- **Validation engine — this is domain logic, so it lives here.** The `IValidationRule` strategy interface and every concrete strategy (`Required`, `Min`, `Max`, `AtLeast`, `AtMost`, `Pattern`, `RequiredIf`), the `IValidationRuleFactory` and its implementation, and two **domain services**: `ITemplateValidator` (authoring — validates the template definition) and `ISubmissionValidator` (runtime — evaluates submitted answers against the template's rules). The rules encode template/answer invariants and touch nothing outward, so they belong in the domain — not in Application or Infrastructure.

> The factory is a pure `Kind → strategy` index built from an `IEnumerable<IValidationRule>`; it needs no DI of its own. The host's composition root supplies that collection (see below), but registering domain types in a container is just wiring — it does not make them infrastructure.

### Application (`TemplateGenerator.Application`)

Use-case orchestration and the ports the use cases drive. Depends only on `Domain`.

- **Use cases:** `SaveTemplate` (invoke `ITemplateValidator` → upsert draft), `PublishTemplate` (validate → lock → bump version), `GetTemplate`, `ListTemplates` (authoring); `SubmitForm` (load template → invoke `ISubmissionValidator` → persist `FormSubmission` if valid). Use cases _invoke_ domain validators; they contain no validation logic themselves.
- **Output ports (driven):** `ITemplateRepository`, `IFormSubmissionRepository`.

### Infrastructure (`TemplateGenerator.Infrastructure`) — the adapters

Concrete implementations of the ports. Depends on `Application` + `Domain`:

- **CosmosTemplateRepository** (`ITemplateRepository`) — the only place the Cosmos SDK is referenced. Partition by `tenantId`, document per version. Swappable for Table/SQL by adding another adapter. (`InMemoryTemplateRepository` backs tests.)

No validation code lives here — the engine is entirely in `Domain`.

### Contracts (`TemplateGenerator.Contracts`)

Public, caller-facing wire DTOs — the request/response payloads shown above, plus the structured validation-error shape. Plain DTOs, no behavior, no infra refs. Shippable as a client NuGet. Mapping between these and the `Domain` model happens at the edges (controllers / a thin mapper).

### API host (`TemplateGenerator.Api`) — driving adapter + composition root

Thin ASP.NET Core controllers: map `Contracts` DTOs, invoke use cases, return `201`/`200`/`400`. This is the composition root — it wires DI, binding `ITemplateRepository` to Cosmos and registering every `IValidationRule`. Can equally be an Azure Function (HTTP trigger); only the hosting envelope changes.

### Project structure

```
TemplateGenerator/
├── TemplateGenerator.Domain/         — Template, Section, Entry, InputType, RuleDefinition,
│                                        TemplateStatus (+ transitions) AND the validation engine:
│                                        IValidationRule (+ strategies), IValidationRuleFactory,
│                                        ITemplateValidator, ISubmissionValidator (domain services). ZERO dependencies.
├── TemplateGenerator.Application/     — Use cases (SaveTemplate, PublishTemplate, SubmitForm, …),
│                                        ITemplateRepository, IFormSubmissionRepository ports. → Domain
├── TemplateGenerator.Contracts/       — Public wire DTOs + validation-error shape.
│                                        Shippable as a client NuGet. → (none)
├── TemplateGenerator.Infrastructure/  — CosmosTemplateRepository. → Application, Domain
├── TemplateGenerator.Api/             — Driving adapter + composition root: controllers,
│                                        versioning, DI wiring. → Application, Infrastructure, Contracts
└── TemplateGenerator.Tests/           — Unit & integration (InMemoryTemplateRepository)
```

**Dependency rule:** `Domain` knows nothing (model **and** validation rules); `Application` knows only `Domain`; `Infrastructure` and the host know `Application`. Only `Api` wires DI. `Contracts` stands alone. _Optional merge:_ if use-case logic stays thin, fold `Application` into `Domain` as one `Core` project (4 projects instead of 5).

---

## API Contract

URL-path versioning (`/api/v{n}/...`), matching the PDF service convention.

| Method | Path                                                | Description                                                     |
| ------ | --------------------------------------------------- | --------------------------------------------------------------- |
| `POST` | `/api/v1/templates`                                 | Create/save a draft template (runs structural validation)       |
| `PUT`  | `/api/v1/templates/{id}`                            | Update a draft (structural validation; rejected if `Published`) |
| `POST` | `/api/v1/templates/{id}/publish`                    | Validate + lock; bumps to a new immutable version               |
| `GET`  | `/api/v1/templates/{id}`                            | Fetch a template (optional `?version=`); UI reads `validations[]` from this response for client-side validation |
| `GET`  | `/api/v1/templates?tenantId=`                       | List templates for a tenant                                     |
| `POST` | `/api/v1/templates/{id}/submissions`                | Submit form answers; runs runtime validation, persists on success |
| `GET`  | `/api/v1/templates/{id}/submissions/{submissionId}` | Fetch a submission                                              |

---

## Technology Choices and Swap Paths

| Concern                      | Default (v1)                                                                                   | Future swap                                                           |
| ---------------------------- | ---------------------------------------------------------------------------------------------- | --------------------------------------------------------------------- |
| Template store               | Azure Cosmos DB via `CosmosTemplateRepository`                                                 | Azure Table / SQL / Mongo — reimplement `ITemplateRepository`         |
| Validation rules             | Built-in strategy set (`Required`, `Min`, `Max`, `AtLeast`, `AtMost`, `Pattern`, `RequiredIf`) | Add an `IValidationRule` class + one DI line — no core edits          |
| `Min`/`Max` modeling         | Generic kind, strategy interprets per category                                                 | Split into explicit `MinLength`/`MinValue` kinds — factory absorbs it |
| Rule applicability           | Each strategy owns its `AppliesTo` row                                                         | Same — new rules ship their own applicability                         |
| Hosting                      | ASP.NET Core                                                                                   | Azure Function (HTTP trigger) — same use cases/ports                  |
| Submission store             | Cosmos DB via `CosmosFormSubmissionRepository` (`submissions` container)                        | Same swap paths — `IFormSubmissionRepository` seam                    |
| UI validation translation    | Template payload `validations[]` interpreted client-side (rule catalog is the contract)         | Dedicated `/validation-schema` endpoint mapping rules to JSON Schema  |
| Export                       | Out of scope                                                                                    | New export use case reading submissions by `templateId`/`version`     |
