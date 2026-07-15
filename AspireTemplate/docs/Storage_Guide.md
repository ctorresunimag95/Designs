# Azure Storage Configuration Guide

Use this guide to add Blob, Queue, and Table Storage resources to an Aspire AppHost and seed local blob data.

## 1. Add Storage and Azurite

```csharp
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite => azurite
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume("azurite-data")
        .WithBlobPort(10000)
        .WithQueuePort(10001)
        .WithTablePort(10002));
```

The ports are optional. Set them explicitly when local tools or tests need stable endpoints.

## 2. Add storage services

```csharp
var blobs = storage.AddBlobs("blobs");
var queues = storage.AddQueues("queues");
var tables = storage.AddTables("tables");
```

Add only the services required by the application.

## 3. Add a blob seed command

```csharp
var blobs = storage.AddBlobs("blobs")
    .WithDataLoadCommand(
        "seed-sample-data",
        "Seed Sample Data",
        containerName: "sample-data",
        filePath: Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "SampleFiles"));
```

The repository implementation preserves directory structure and infers content types in `infrastructure/AspireTemplate.AppHost/Extensions/BlobExtensions.cs`. Use the optional `blobOptionsModifier` to set options such as access tier.

## 4. Reference Storage from a project

```csharp
var api = builder.AddProject<Projects.AspireTemplate_SampleApi>("sample-api")
    .WithReference(blobs, connectionName: "AzureBlobStorage");
```

Use the matching configuration key in the application when registering `BlobServiceClient`.

## 5. Verify the sample API

Start the AppHost, run **Seed Sample Data** in the Aspire dashboard, then call:

```bash
curl http://localhost:<api-port>/api/files
```

The protected endpoint requires a valid token with the `ApiReader` policy:

```bash
curl http://localhost:<api-port>/api/secure/files \
  -H "Authorization: Bearer <token>"
```

## 6. Prepare for Azure

Replace Azurite with an Azure Storage account, use private endpoints or appropriate network rules, prefer Microsoft Entra authentication, and disable anonymous access for application data. Persist emulator data in a volume only for local development.
