# SQL Server Configuration Guide

This doc covers three topics:

1. Local SQL Server container for development (via Aspire).
2. Connecting to an existing Azure SQL database using Active Directory / Managed Identity.
3. Running database migrations — automated (EF Core migration service) and manual (SQL script).

---

## 1. Local SQL Server (Aspire container resource)

For local development, Aspire spins up a SQL Server container automatically. The relevant configuration lives in `AppHost.cs`:

```csharp
var sqlPassword = builder.AddParameter("SqlPassword", secret: true);

var sql = builder.AddSqlServer("sql", password: sqlPassword)
    .WithEndpoint(targetPort: 1433, port: 1433)
    .WithDataVolume("sql-data-volume")
    .WithLifetime(ContainerLifetime.Persistent);

var db = sql.AddDatabase("AppDB");
```

**Key points**

- `SqlPassword` is read from user secrets or `appsettings.json` — never hard-code it. Set it once with:
  ```bash
  dotnet user-secrets set "Parameters:SqlPassword" "YourLocalPassword!" \
    --project infrastructure/AspireGuide.AppHost
  ```
- `WithEndpoint(targetPort: 1433, port: 1433)` pins the host port so connection strings and tools (SSMS, Azure Data Studio) can always reach the container at `localhost,1433`.
- `WithDataVolume("sql-data-volume")` mounts a named Docker volume so data persists across container restarts.
- `WithLifetime(ContainerLifetime.Persistent)` keeps the container alive between `dotnet run` sessions, avoiding a cold-start on every debug launch.

---

## 2. Connecting to an existing Azure SQL database (Active Directory / Managed Identity)

When you want to point your local AppHost at a real Azure SQL instance instead of the container, replace the `AddSqlServer` block with `AddConnectionString`:

```csharp
// AppHost.cs — replace the local SQL block with this:
var db = builder.AddConnectionString("Database");
```

The connection string is resolved from configuration (user secrets or `appsettings.json`):

```json
// appsettings.json (AppHost project) — do NOT commit real connection strings
"ConnectionStrings": {
  "Database": "Server=<server>.database.windows.net,1433;Initial Catalog=<database>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default;"
}
```

**Authentication options in the connection string**

| Scenario                                           | `Authentication=` value                                     |
| -------------------------------------------------- | ----------------------------------------------------------- |
| Local dev — logged-in user via `az login`          | `Active Directory Default`                                  |
| Interactive browser pop-up                         | `Active Directory Interactive`                              |
| User-Assigned Managed Identity (deployed workload) | `Active Directory Managed Identity` + `User Id=<client-id>` |
| SQL username / password                            | `Sql Password` + `User Id=` + `Password=`                   |

### Steps to authenticate with `az login` (user identity)

1. **Log in** from your terminal (or inside the devcontainer):

   ```bash
   az login
   # or, if a browser is not available:
   az login --use-device-code
   ```

2. **Confirm the correct subscription/tenant is active:**
   ```bash
   az account show
   az account set --subscription "<subscription-id>"
   ```

> **Note:** `Active Directory Default` works for both local (`az login`) and deployed environments (Managed Identity). Prefer it over hard-coding a specific credential type so the same connection string works everywhere.

---

## 3. Running database migrations

The project uses EF Core migrations stored in `src/AspireGuide.Data/Migrations/`. There are two ways to apply them.

### Option A (Preferred) — Automated via the MigrationService (recommended for local and CI)

`infrastructure/AspireGuide.MigrationService` is an Aspire worker project that runs `MigrateAsync` on startup. It is wired into the AppHost as a dependency:

```csharp
var migrations = builder.AddProject<Projects.AspireGuide_MigrationService>("migrations")
    .WithReference(db, "Database")
    .WaitFor(db);

// Services that need the schema must wait for migrations to complete:
var api = builder.AddProject<Projects.AspireGuide_SampleApi>("sample-api")
    ...
    .WaitForCompletion(migrations);
```

**How it works**

1. AppHost starts the SQL Server container (or waits for the external database resource to be ready).
2. The migration service starts, calls `dbContext.Database.MigrateAsync()`, and then seeds initial data if the database is empty.
3. The API and any other dependent services only start after the migration service exits cleanly.

No manual steps are required — just run the AppHost.

**Adding a new migration**

```bash
dotnet ef migrations add <MigrationName> \
  --project src/AspireGuide.Data \
  --startup-project src/AspireGuide.SampleApi
```

The next `dotnet run` on the AppHost will apply it automatically.

### Option B — Manual SQL script

Use this when you need to apply migrations to an environment where running the full AppHost is not practical (e.g. a production database with restricted access, a CI job that only has a connection string).

**run a creation script on first start**

```csharp
var creationScript = File.ReadAllText("Scripts/init.sql");
var db = sql.AddDatabase("AppDB")
    .WithCreationScript(creationScript);
```

The script runs once, immediately after the container starts and the database is created. Use it for schema bootstrapping when you are not using EF Core migrations.

**run post-init scripts on every startup**

```csharp
var db = sql.AddDatabase("AppDB")
    .WithPostInitScripts(); // executes Scripts/PostInit/*.sql in order
```

Useful for re-seeding lookup tables or applying idempotent configuration changes that must be present every time the app starts.
