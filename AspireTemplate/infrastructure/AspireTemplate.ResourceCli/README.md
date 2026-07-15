# AspireTemplate

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
AspireTemplate add [--resource <key>]... [--all] [--yes] [--dry-run] [--root <path>]
AspireTemplate list
AspireTemplate init [--directory <path>] [--solution <path>] [--yes]
```

### `add`

Adds one or more resources to an AppHost. Without `--resource` or `--all`, launches an interactive multi-select.

```bash
# Interactive
AspireTemplate add

# Non-interactive / CI
AspireTemplate add --resource servicebus --resource keycloak

# Add everything
AspireTemplate add --all --yes

# Preview without writing
AspireTemplate add --resource blobstorage --dry-run

# Target a specific repo root
AspireTemplate add --resource appconfiguration --root ./src/MyAppHost
```

### `list`

Prints the resource catalog with descriptions, packages, and dependencies.

```bash
AspireTemplate list
```

### `init`

Scaffolds a new AppHost via `dotnet new aspire-apphost`, adds it to a solution, writes `aspire.config.json`, then continues to the `add` flow.

```bash
AspireTemplate init
AspireTemplate init --directory src/MyAppHost --solution MyApp.sln --yes
```

## Installation

Since AspireTemplate.ResourceCli is not published to NuGet.org, you must build the NuGet package first.

### Build the NuGet package

```bash
cd infrastructure/AspireTemplate.ResourceCli
dotnet pack -o ./nupkg
```

This creates a `.nupkg` file in the `./nupkg` directory. Use the path to this package when installing.

---

Choose the installation approach that fits your workflow:

### Local tool installation (recommended for teams)

Install the tool in your repository only. The tool is scoped to this project and managed through version control.

**Benefits:**
- All team members use the same version (specified in `.dotnet/tools/dotnet-tools.json`)
- No global system clutter
- Each project can use different tool versions
- Easy to enforce consistency across the team

**Installation:**

```bash
# Step 1: Create a tool manifest (run once per repository)
dotnet new tool-manifest

# Step 2: Install AspireTemplate.ResourceCli locally from the nupkg
dotnet tool install --local AspireTemplate.ResourceCli --add-source ./infrastructure/AspireTemplate.ResourceCli/nupkg

# Step 3: Verify installation
AspireTemplate list
```

The tool is now installed in `.dotnet/tools/` within your repository. Commit `.dotnet/tools/dotnet-tools.json` to version control.

**For team members:**

```bash
# Restore all tools from the manifest
dotnet tool restore

# Use the tool
AspireTemplate list
```

**Update:**

```bash
dotnet tool update --local AspireTemplate.ResourceCli
```

**Uninstall:**

```bash
dotnet tool uninstall --local AspireTemplate.ResourceCli
```

### Global tool installation (for personal use)

Install the tool on your machine. It works from any directory.

**Benefits:**
- Available across all projects on your machine
- No per-project configuration needed
- Quick one-time setup

**Installation:**

```bash
# Install globally from the nupkg
dotnet tool install --global AspireTemplate.ResourceCli --add-source ./infrastructure/AspireTemplate.ResourceCli/nupkg

# Verify installation
AspireTemplate list
```

The tool is installed in your `$PATH` and available from any directory.

**Update:**

```bash
dotnet tool update --global AspireTemplate.ResourceCli
```

**Uninstall:**

```bash
dotnet tool uninstall --global AspireTemplate.ResourceCli
```

**Troubleshooting:**

If you get "command not found" after installation, add the global tools directory to your `$PATH`:

- **Linux/macOS:** `~/.dotnet/tools`
- **Windows:** `%USERPROFILE%\.dotnet\tools`

## Local development / pack

```bash
cd infrastructure/AspireTemplate.ResourceCli
dotnet pack -o ./nupkg
dotnet tool install --global --add-source ./nupkg AspireTemplate.ResourceCli
```

To test against a fresh AppHost:

```bash
mkdir /tmp/test-apphost && cd /tmp/test-apphost
AspireTemplate init --directory . --yes
AspireTemplate add --resource servicebus --dry-run
```

## How it works

1. **Discovery** — finds the AppHost using `aspire.config.json`, Aspire SDK markers, or a recursive scan. Pass `--root` to skip discovery.
2. **Validation** — checks dependencies, package conflicts, file writability, and companion-file conflicts before writing anything.
3. **ChangeSet** — all edits (C# regions, packages, MSBuild metadata, files, solution entries) are computed in memory first.
4. **Apply** — atomic temp-file writes + per-file backup/restore. Any failure rolls back all changes, leaving the AppHost byte-identical to its pre-run state.

## Idempotency

Re-running `add` for already-installed resources is safe — stable markers (`// <AspireTemplate resource="...">`) and legacy `#region` blocks are detected and skipped. No duplicate packages or files are added.

## Aspire version compatibility

Templates are snapshots of **Aspire 13.4.6**. If your AppHost targets a different version, packages and extension helpers may need manual adjustment. The tool warns when the detected Aspire SDK version differs from the supported version.

## Troubleshooting

| Symptom | Fix |
|---|---|
| `AspireTemplate: command not found` | Run `dotnet tool restore` (local) or check `$PATH` for the global tools directory |
| Stale version after update | `dotnet tool uninstall` then reinstall |
| `MISSING_BUILD_ANCHOR` | Ensure `Program.cs` contains `builder.Build().Run()` or `builder.Build().RunAsync()` |
| `REQUIRED_COMPANION_CONFLICT` | The companion file exists with different content; resolve manually then re-run |
| `PACKAGE_CONFLICT` | A newer package version is already installed; the tool will not downgrade it |
| `MISSING_REQUIRED_PROJECT` | For `sbexplorer`, the `AspireTemplate.ServiceBusExplorer` project must exist in the repository |
