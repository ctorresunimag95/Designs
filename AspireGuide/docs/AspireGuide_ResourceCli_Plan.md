# AspireGuide Resource CLI — implementation plan

**Status:** Planned

## Purpose

`aspireguide` is a .NET `dotnet tool` that **adds preconfigured Aspire resources to an AppHost**. It:

1. **Discovers** an existing Aspire AppHost in the current repository.
2. If none is found, **prompts the user for a location** and **creates a new AppHost** there.
3. Lets the user **select one or more resources** to add (interactive multi-select, powered by [Spectre.Console](https://spectreconsole.net/)) or accept them as arguments for scripted/CI use.
4. **Configures** each selected resource by inserting the AppHost code block, adding required NuGet packages and MSBuild metadata, and copying companion files (realm JSON, sample data, extension helpers).

The tool is idempotent (re-running adds nothing already present), supports `--dry-run`, and is transactional (all-or-nothing — never leaves the AppHost half-edited).

The resource templates are **embedded snapshots** of this repository's Aspire `13.4.6` implementation (see [AppHost.cs](../infrastructure/AspireGuide.AppHost/AppHost.cs)).

## Available resources

The user can select any combination of these five:

| Key | Display name | Type | What it adds |
|---|---|---|---|
| `servicebus` | Service Bus | Infrastructure | `AddAzureServiceBus` with emulator, queues, topic + subscription, and the interactive "Send Test Message" command. Pulls in the `ServiceBusExtensions` helper. |
| `appconfiguration` | App Configuration | Infrastructure | `AddAzureAppConfiguration` running as an emulator (auth disabled, host port 28000, persistent data volume). |
| `keycloak` | Keycloak | Infrastructure | `AddLocalKeycloak()` on port 8080 with the pre-seeded `aspire-guide` realm. Pulls in `KeycloakExtensions` and the `realm-export.json` companion file. |
| `blobstorage` | Blob Storage | Infrastructure | `AddAzureStorage` via Azurite (blob/queue/table ports, persistent volume) plus blobs/queues/tables and the "Seed Sample Data" load command. Pulls in `BlobExtensions` and the `Content/SampleFiles` sample content. |
| `sbexplorer` | Service Bus Explorer | Integration | Adds the `AspireGuide.ServiceBusExplorer` Blazor project wired to the Service Bus emulator. **Requires `servicebus`** and the Explorer project. |

Selection rules:

- **Service Bus Explorer depends on Service Bus.** If the user selects `sbexplorer` without `servicebus`, the tool either auto-adds `servicebus` (with confirmation) or fails with clear guidance in non-interactive mode.
- Resources are inserted in a **deterministic order**: `servicebus`, `blobstorage`, `appconfiguration`, `keycloak`, `sbexplorer`. This keeps repeat runs and diffs stable.

## Commands

```text
aspireguide add [--resource <key>]... [--all] [--yes] [--dry-run] [--root <path>]
aspireguide list
aspireguide init [--directory <path>] [--solution <path>]
```

- `add` with **no** `--resource`: launches the Spectre.Console interactive multi-select.
- `add --resource servicebus --resource keycloak`: non-interactive, CI-friendly.
- `--all`: selects all compatible resources.
- `--yes`: skips confirmation prompts.
- `--dry-run`: prints the planned changes without writing anything.
- `--root <path>`: point at a specific repository / AppHost instead of discovering one.
- `list`: prints the resource catalog with descriptions and dependencies.
- `init`: explicitly scaffold a new AppHost, then continue the `add` flow.

## User experience (Spectre.Console)

The interactive flow should feel guided and safe:

1. **Discovery status** — a status spinner while scanning, then a rule/panel showing the AppHost that was found (path + Aspire SDK version), or a prompt to create one.
2. **Create prompt** (only if no AppHost found) — `TextPrompt` for the target directory, defaulting to a sensible location; a `SelectionPrompt` to pick the solution to attach to when several exist.
3. **Resource multi-select** — a `MultiSelectionPrompt<T>` listing the five resources with their descriptions; already-installed resources are shown checked and disabled ("already present"). Dependencies are surfaced inline (e.g. selecting Explorer auto-checks Service Bus).
4. **Confirmation** — a `Table` summarizing the `ChangeSet` (files edited, packages added, companion files copied), then a `ConfirmationPrompt` unless `--yes`.
5. **Apply** — a `Progress`/`Status` display while writing, then a success panel; on failure, a red panel with the rollback result.

Keep Spectre.Console interaction **thin** — a single `IConsoleUx` abstraction — so discovery, editing, and orchestration are testable without prompts.

## Planned project layout

```text
infrastructure/AspireGuide.ResourceCli/
├── AspireGuide.ResourceCli.csproj
├── README.md
├── Program.cs
├── Ux/{IConsoleUx,SpectreConsoleUx}.cs
├── Commands/{AddCommand,ListCommand,InitCommand}.cs
├── Catalog/{ResourceCatalog,ResourceDefinition,CompanionFile,PackageRequirement,ProjectRequirement}.cs
├── Discovery/{RepositoryLocator,AppHostLocator,SolutionLocator}.cs
├── Editing/{ChangeSet,AppHostEditor,CsprojEditor,CentralPackageEditor,FileWriter,SolutionEditor}.cs
├── Validation/{PreflightValidator,ValidationIssue}.cs
└── Templates/{regions,files,apphost}/
```

Add the CLI to [AspireGuide.slnx](../AspireGuide.slnx); do **not** reference it from runtime projects.

## Design requirements

### Catalog and idempotency

Each resource definition needs a stable `Key`, display name, region title, marker ID, template, package requirements, companion files, project/resource/file prerequisites, optional integration actions, and notes. Classify each entry as `Infrastructure` or `Integration`.

Generated blocks must be wrapped in **stable markers** (not just the human region name), so re-runs can detect and skip them safely:

```csharp
// <aspireguide resource="keycloak">
# region Identity
...
# endregion
// </aspireguide>
```

> Compatibility note: in the current AppHost the Keycloak block is a region named **`Identity`**, and regions use the `# region` form (with a space). Matching only a `Keycloak` region would miss it. Maintain a **compatibility map** (`keycloak → Identity`) and recognize both `#region` and `# region`; emit stable markers for all new output.

### Discovery

Resolution order:

1. Explicit `--root` / `--apphost`.
2. `aspire.config.json` in the current directory or an ancestor (resolve `appHost.path` relative to the config file).
3. Current repository solution (`.sln` / `.slnx`).
4. Aspire SDK markers — `Aspire.AppHost.Sdk` in the csproj or `<IsAspireHost>true</IsAspireHost>`.
5. Recursive project search, excluding `bin`, `obj`, `.git`, and unrelated roots.
6. Interactive selection when multiple candidates are found.

If nothing is found, fall through to the **create** path (`init`). Locate the build anchor — `builder.Build().Run();` or `await builder.Build().RunAsync();` — and fail with guidance if no anchor exists. Preserve encoding, newline style (LF/CRLF), and the final newline.

### Validation and transaction safety

Before writing, validate the complete selection:

- AppHost and build anchor exist.
- Selected resources plus existing resources satisfy dependencies (e.g. Explorer needs Service Bus).
- Required projects exist (Explorer project for `sbexplorer`).
- Companion-file conflicts are known and classified.
- Package versions do not conflict with existing references.
- Solution and target files are writable.

Build a `ChangeSet` covering C# edits, packages, MSBuild metadata, files, and solution edits. `--dry-run` prints it. Apply only after validation passes, using temporary files / atomic replacement. Back up changed files and **restore them on any failure**. Never report a successful partial bundle.

Required companion files (realm JSON, extension helpers) **fail** on conflict; optional sample files (`Content/SampleFiles`) may skip with a warning. State the policy per file.

### AppHost and project editing

Insert regions immediately **before** the build anchor, in deterministic catalog order. Detect stable markers and legacy `#region`/`# region` blocks; preserve the final build statement; avoid duplicate `using` directives; reject unresolved `Projects.*` references (a minimal/new AppHost must not emit an Explorer reference unless that project exists).

Edit project XML without disturbing unrelated content. Match packages by `Include`, never downgrade an existing version, and report conflicts. Support both direct `PackageReference` and central package management via `Directory.Packages.props`. Add `Content`/`None` copy metadata for Keycloak (`Keycloak\**`) and Blob (`Content\**`) files so `AppContext.BaseDirectory` lookups resolve at runtime.

### Init (create AppHost when none exists)

1. Prompt for or accept a target directory.
2. Prefer `dotnet new aspire-apphost`.
3. Verify the generated Aspire SDK marker.
4. Fall back to embedded templates only if necessary.
5. Add the project to a selected/discovered `.sln`/`.slnx`.
6. Write `aspire.config.json` beside the project.
7. Continue the `add` flow against the new AppHost.

## Package reference (snapshot of Aspire `13.4.6`)

Pin versions in one catalog source of truth. Do not add packages merely because they appear in comments.

| Resource | Packages |
|---|---|
| `servicebus` | `Aspire.Hosting.Azure.ServiceBus` 13.4.6, `Azure.Messaging.ServiceBus` 7.20.1 |
| `appconfiguration` | `Aspire.Hosting.Azure.AppConfiguration` 13.4.6 |
| `keycloak` | `Aspire.Hosting.Keycloak` 13.4.6-preview.1.26319.6 |
| `blobstorage` | `Aspire.Hosting.Azure.Storage` 13.4.6, `Azure.Storage.Blobs` 12.29.1 |
| `sbexplorer` | project reference only (`AspireGuide.ServiceBusExplorer`) |

---

# Implementation phases

Each phase is independently buildable and testable. An agent should complete phases **in order**, run the phase's verification, then move on. Steps within a phase can be done as separate commits.

## Phase 0 — Project bootstrap
**Goal:** an installable, runnable (no-op) CLI skeleton.

- [X] Create `infrastructure/AspireGuide.ResourceCli/AspireGuide.ResourceCli.csproj` as a `dotnet tool` (`<PackAsTool>true</PackAsTool>`, `<ToolCommandName>aspireguide</ToolCommandName>`, `net10.0`).
- [X] Add `Spectre.Console` and `Spectre.Console.Cli` package references.
- [X] Add `Program.cs` wiring the Spectre.Console `CommandApp` with stub `add`, `list`, `init` commands.
- [X] Add the project to [AspireGuide.slnx](../AspireGuide.slnx).
- [X] **Verify:** `dotnet build`, then `dotnet run -- list` prints an empty catalog.

## Phase 1 — Catalog models and definitions
**Goal:** the five resources described as data.

- [X] Define `ResourceDefinition`, `CompanionFile`, `PackageRequirement`, `ProjectRequirement`, and the `ResourceType` enum in `Catalog/`.
- [X] Populate `ResourceCatalog` with the five resources, their packages (table above), companion files, dependencies (`sbexplorer → servicebus`), and the deterministic insert order.
- [X] Implement the legacy-region compatibility map (`keycloak → Identity`).
- [X] Implement `list` to render the catalog as a Spectre.Console `Table`.
- [X] **Verify:** `dotnet run -- list` shows all five resources with dependencies and descriptions.

## Phase 2 — Templates with stable markers
**Goal:** embedded, marker-wrapped code + companion files.

- [X] For each resource, extract its region from [AppHost.cs](../infrastructure/AspireGuide.AppHost/AppHost.cs) into `Templates/regions/<key>.txt`, wrapped in `// <aspireguide resource="...">` markers.
- [X] Embed companion files: `Keycloak/realm-export.json`, `Content/SampleFiles/*`, and the `Extensions/*.cs` helpers (`ServiceBusExtensions`, `BlobExtensions`, `KeycloakExtensions`) under `Templates/files/`.
- [X] Mark all template assets as `EmbeddedResource` and add a loader.
- [X] **Verify:** a unit test asserts each template loads and contains its expected marker.

## Phase 3 — Discovery
**Goal:** find an AppHost (or decide none exists).

- [X] Implement `RepositoryLocator`, `AppHostLocator`, `SolutionLocator` following the resolution order above.
- [X] Recognize `Aspire.AppHost.Sdk` and `<IsAspireHost>true</IsAspireHost>`; locate the build anchor.
- [X] Return a result that distinguishes: single AppHost found, multiple candidates (needs selection), none found (needs `init`).
- [X] **Verify:** unit tests over fixture folders — config-based, SDK-marker, multiple candidates, and none.

## Phase 4 — Pure editors and ChangeSet
**Goal:** compute edits without touching disk.

- [X] `AppHostEditor`: insert region before the anchor, skip if the stable marker (or legacy region) already exists, dedupe usings, reject unresolved `Projects.*`.
- [X] `CsprojEditor` + `CentralPackageEditor`: add packages by `Include`, never downgrade, add `Content`/`None` metadata; support `Directory.Packages.props`.
- [X] `SolutionEditor`: add project to `.sln`/`.slnx`.
- [X] `ChangeSet`: aggregate all planned C# edits, packages, MSBuild metadata, file copies, and solution edits. Preserve encoding/newlines/final newline.
- [X] **Verify:** editors are pure (input string → output string); golden-file tests for add, idempotent re-add, and legacy-region detection.

## Phase 5 — Validation and dry-run
**Goal:** fail before writing; show the plan.

- [X] `PreflightValidator` producing `ValidationIssue`s for the checks listed under *Validation and transaction safety*.
- [X] Wire `--dry-run` to print the `ChangeSet` as a Spectre.Console table and exit without writing.
- [X] **Verify:** dry-run against a fixture produces the expected plan and **zero** file changes; missing-dependency and missing-project cases fail cleanly.

## Phase 6 — Transactional apply
**Goal:** all-or-nothing writes.

- [ ] `FileWriter` with temp-file + atomic replace and per-file backup/restore.
- [ ] Apply the `ChangeSet` only after validation; roll back everything on any failure.
- [ ] Copy companion files honoring the per-file conflict policy (required = fail, optional = warn/skip).
- [ ] **Verify:** simulated mid-apply failure leaves the AppHost byte-identical to its pre-run state.

## Phase 7 — Interactive `add` flow (Spectre.Console)
**Goal:** the full guided experience.

- [ ] Implement `IConsoleUx` / `SpectreConsoleUx` (status, multi-select, confirmation table, progress, result panels).
- [ ] `AddCommand`: discovery → (create if needed) → multi-select → dependency resolution → validate → confirm → apply.
- [ ] Show already-installed resources as checked/disabled; auto-resolve `sbexplorer → servicebus`.
- [ ] Honor `--resource`, `--all`, `--yes`, `--dry-run`, `--root`.
- [ ] **Verify:** end-to-end against a throwaway AppHost — interactive and non-interactive; repeat add produces no changes.

## Phase 8 — `init` (create AppHost)
**Goal:** scaffold when discovery finds nothing.

- [ ] `InitCommand` following the Init steps; prefer `dotnet new aspire-apphost`, verify SDK marker, fall back to template.
- [ ] Prompt for target directory and solution; write `aspire.config.json`; then continue into `add`.
- [ ] **Verify:** init in an empty directory builds, and a subsequent `add servicebus` builds and runs.

## Phase 9 — Tests and fixtures
**Goal:** confidence across the surface.

- [ ] Fixture AppHosts: minimal, legacy-region (`Identity`), central-package-management, CRLF/LF, and multi-candidate.
- [ ] Cover the **Testing checklist** below.
- [ ] **Verify:** full test suite green on CI.

## Phase 10 — Docs and tool packaging
**Goal:** shippable tool.

- [ ] Tool `README.md` (overview, command reference, interactive/non-interactive/dry-run examples).
- [ ] Local pack/install/update/uninstall and tool-manifest workflow.
- [ ] Aspire-version compatibility and template-snapshot maintenance guidance.
- [ ] Troubleshooting: PATH, stale versions, restore, missing templates, package conflicts, prerequisites.
- [ ] **Verify:** `dotnet pack`, install as a local tool, and run `aspireguide add` against a fresh AppHost.

---

## Testing checklist

- [ ] Discovery: `aspire.config.json`, SDK-marker, multiple candidates, and none-found → init.
- [ ] Legacy region syntax and `Identity`/`keycloak` compatibility.
- [ ] Stable-marker idempotency (repeat add = no changes).
- [ ] Build-anchor variants and missing-anchor failure.
- [ ] LF/CRLF, encoding, final-newline, and Windows path preservation.
- [ ] Direct and central package management; existing-package preservation and conflict reporting.
- [ ] Companion collisions and MSBuild copy metadata for Keycloak and Blob files.
- [ ] Service Bus Explorer dependency handling (auto-add / reject when Service Bus or its project is absent).
- [ ] Missing resource/project prerequisites.
- [ ] Transaction rollback after a simulated failure.
- [ ] Multi-select of several resources at once builds and runs.
- [ ] Dry-run produces no changes; `list` output.
- [ ] Init in an empty directory, then add, then build.
- [ ] Keycloak realm sample is valid JSON.

## Maintenance

Templates are snapshots of Aspire `13.4.6`. When AppHost code or Aspire packages change, update the catalog, templates, compatibility mappings, package versions, and fixtures together. Warn when the target Aspire SDK differs from the supported version.
