# SQL Server Configuration Guide

Use this guide to configure local SQL Server, switch to Azure SQL, and run EF Core migrations through Aspire.

## 1. Add local SQL Server

```csharp
var sqlPassword = builder.AddParameter("SqlPassword", secret: true);

var sql = builder.AddSqlServer("sql", password: sqlPassword)
    .WithEndpoint(targetPort: 1433, port: 1433)
    .WithDataVolume("sql-data-volume")
    .WithLifetime(ContainerLifetime.Persistent);

var db = sql.AddDatabase("AppDB");
```

Store the password in user secrets:

```bash
dotnet user-secrets set "Parameters:SqlPassword" "YourLocalPassword!" --project infrastructure/AspireTemplate.AppHost
```

The named volume persists data across restarts. Remove it only when a clean database is required.

## 2. Connect to an existing Azure SQL database

Replace the local SQL resource with a connection string resource named `Database`, because the API and migration service consume that name:

```csharp
var db = builder.AddConnectionString("Database");
```

Configure the connection string through user secrets or AppHost configuration:

```json
{
  "ConnectionStrings": {
    "Database": "Server=<server>.database.windows.net,1433;Initial Catalog=<database>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default;"
  }
}
```

Authenticate locally before starting the AppHost:

```bash
az login
az account set --subscription "<SUBSCRIPTION_ID_OR_NAME>"
```

`Active Directory Default` can use Azure CLI locally and a managed identity in Azure.

## 3. Register the migration service

```csharp
var migrations = builder.AddProject<Projects.AspireTemplate_MigrationService>("migrations")
    .WithReference(db, "Database")
    .WaitFor(db);

var api = builder.AddProject<Projects.AspireTemplate_SampleApi>("sample-api")
    .WithReference(db, "Database")
    .WaitForCompletion(migrations);
```

The migration worker applies pending migrations and seeds the database before the API starts.

## 4. Create a migration

Run from the repository root:

```bash
dotnet ef migrations add <MigrationName> --project src/AspireTemplate.Data --startup-project src/AspireTemplate.SampleApi
```

The next AppHost startup applies the migration automatically.

## 5. Use SQL initialization scripts when appropriate

Run a creation script once when the database is created:

```csharp
var creationScript = File.ReadAllText("Scripts/init.sql");
var db = sql.AddDatabase("AppDB")
    .WithCreationScript(creationScript);
```

Run idempotent post-initialization scripts on startup:

```csharp
var db = sql.AddDatabase("AppDB")
    .WithPostInitScripts();
```

Do not use both script-based schema creation and EF migrations for the same schema unless the ownership is clearly defined.

## 6. Troubleshoot

- Confirm Docker is running and host port `1433` is available.
- Confirm the connection resource is named `Database` in the AppHost, API, and migration service.
- Run `az account show` when Azure SQL authentication fails.
- Verify the signed-in user or managed identity has database permissions.
- Never commit production passwords or connection strings.
