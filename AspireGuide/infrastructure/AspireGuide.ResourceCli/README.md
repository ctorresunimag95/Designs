# aspireguide

A .NET global/local tool that adds preconfigured Aspire resources to an existing AppHost — or scaffolds a new one.

## Available resources

| Key | Display name | Type | What it adds |
|---|---|---|---|
| `servicebus` | Service Bus | Infrastructure | `AddAzureServiceBus` with emulator, queues, topic + subscription |
| `appconfiguration` | App Configuration | Infrastructure | `AddAzureAppConfiguration` emulator on port 28000 |
| `keycloak` | Keycloak | Infrastructure | `AddLocalKeycloak()` on port 8080 with the `aspire-guide` realm |
| `blobstorage` | Blob Storage | Infrastructure | `AddAzureStorage` via Azurite with the "Seed Sample Data" command |
| `sbexplorer` | Service Bus Explorer | Integration | Blazor Service Bus Explorer (requires `servicebus`) |

## Commands

```
aspireguide add [--resource <key>]... [--all] [--yes] [--dry-run] [--root <path>]
aspireguide list
aspireguide init [--directory <path>] [--solution <path>] [--yes]
```

### `add`

Adds one or more resources to an AppHost. Without `--resource` or `--all`, launches an interactive multi-select.

```bash
# Interactive
aspireguide add

# Non-interactive / CI
aspireguide add --resource servicebus --resource keycloak

# Add everything
aspireguide add --all --yes

# Preview without writing
aspireguide add --resource blobstorage --dry-run

# Target a specific repo root
aspireguide add --resource appconfiguration --root ./src/MyAppHost
```

### `list`

Prints the resource catalog with descriptions, packages, and dependencies.

```bash
aspireguide list
```

### `init`

Scaffolds a new AppHost via `dotnet new aspire-apphost`, adds it to a solution, writes `aspire.config.json`, then continues to the `add` flow.

```bash
aspireguide init
aspireguide init --directory src/MyAppHost --solution MyApp.sln --yes
```

## Installation

### As a local tool (recommended)

```bash
dotnet new tool-manifest   # if not already present
dotnet tool install --local AspireGuide.ResourceCli
aspireguide list
```

### As a global tool

```bash
dotnet tool install --global AspireGuide.ResourceCli
aspireguide list
```

### Update

```bash
dotnet tool update --local AspireGuide.ResourceCli
# or
dotnet tool update --global AspireGuide.ResourceCli
```

### Uninstall

```bash
dotnet tool uninstall --local AspireGuide.ResourceCli
```

## Local development / pack

```bash
cd infrastructure/AspireGuide.ResourceCli
dotnet pack -o ./nupkg
dotnet tool install --global --add-source ./nupkg AspireGuide.ResourceCli
```

To test against a fresh AppHost:

```bash
mkdir /tmp/test-apphost && cd /tmp/test-apphost
aspireguide init --directory . --yes
aspireguide add --resource servicebus --dry-run
```

## How it works

1. **Discovery** — finds the AppHost using `aspire.config.json`, Aspire SDK markers, or a recursive scan. Pass `--root` to skip discovery.
2. **Validation** — checks dependencies, package conflicts, file writability, and companion-file conflicts before writing anything.
3. **ChangeSet** — all edits (C# regions, packages, MSBuild metadata, files, solution entries) are computed in memory first.
4. **Apply** — atomic temp-file writes + per-file backup/restore. Any failure rolls back all changes, leaving the AppHost byte-identical to its pre-run state.

## Idempotency

Re-running `add` for already-installed resources is safe — stable markers (`// <aspireguide resource="...">`) and legacy `#region` blocks are detected and skipped. No duplicate packages or files are added.

## Aspire version compatibility

Templates are snapshots of **Aspire 13.4.6**. If your AppHost targets a different version, packages and extension helpers may need manual adjustment. The tool warns when the detected Aspire SDK version differs from the supported version.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `aspireguide: command not found` | Run `dotnet tool restore` (local) or check `$PATH` for the global tools directory |
| Stale version after update | `dotnet tool uninstall` then reinstall |
| `MISSING_BUILD_ANCHOR` | Ensure `Program.cs` contains `builder.Build().Run()` or `builder.Build().RunAsync()` |
| `REQUIRED_COMPANION_CONFLICT` | The companion file exists with different content; resolve manually then re-run |
| `PACKAGE_CONFLICT` | A newer package version is already installed; the tool will not downgrade it |
| `MISSING_REQUIRED_PROJECT` | For `sbexplorer`, the `AspireGuide.ServiceBusExplorer` project must exist in the repository |
