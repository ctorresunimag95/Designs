# App Configuration Setup Guide

Use this guide to add configuration values and feature flags to the local App Configuration emulator and consume them from the sample API.

## 1. Start the emulator

```csharp
var appConfiguration = builder.AddAzureAppConfiguration("appConfiguration")
    .RunAsEmulator(emulator => emulator
        .WithEnvironment(
            "AZURE_APP_CONFIGURATION_EMULATOR_AUTHENTICATION_ENABLED",
            "false")
        .WithHostPort(28000)
        .WithDataVolume("appconfig-data")
        .WithLifetime(ContainerLifetime.Persistent));
```

Reference it from the API:

```csharp
api.WithReference(appConfiguration)
   .WaitFor(appConfiguration);
```

## 2. Create a configuration value

After starting the AppHost, add a key-value entry with the emulator data-plane REST API:

```bash
curl -X PUT "http://localhost:28000/kv/Test?api-version=1.0" \
  -H "Content-Type: application/json" \
  --data-raw '{"value":"Hello from App Configuration"}'
```

The sample API reads `Test` from `IConfiguration` at `/api/read-config`:

```bash
curl http://localhost:<api-port>/api/read-config
```

## 3. Create a feature flag

Store feature flags with the reserved key prefix and content type:

```bash
curl -X PUT "http://localhost:28000/kv/.appconfig.featureflag/Beta?api-version=1.0" \
  -H "Content-Type: application/json" \
  --data-raw '{"value":"{\"id\":\"Beta\",\"description\":\"Enable the beta experience\",\"enabled\":true,\"conditions\":{\"client_filters\":[]}}","content_type":"application/vnd.microsoft.appconfig.ff+json;charset=utf-8"}'
```

Use `"enabled":false` to disable it. Verify the value through the sample endpoint:

```bash
curl http://localhost:<api-port>/api/flag
```

## 4. Enable refresh in the API

```csharp
builder.Configuration.AddAzureAppConfiguration(options =>
{
    options.Connect(builder.Configuration.GetConnectionString("appConfiguration"));
    options.ConfigureRefresh(refresh => refresh.RegisterAll());
    options.UseFeatureFlags(flags =>
        flags.SetRefreshInterval(TimeSpan.FromSeconds(10)));
});

builder.Services.AddAzureAppConfiguration();
// After builder.Build():
app.UseAzureAppConfiguration();
```

After changing a key or flag, wait ten seconds and call the endpoint again. Refresh occurs during requests through the middleware.

## 5. Use labels deliberately

The sample loads unlabelled values. If environments need labels, add explicit selections:

```csharp
options.Select("MySetting", LabelFilter.Null)
       .Select("MySetting", "Development");
```

Use labels for environment or version selection, not as a replacement for access control.

## 6. Prepare for Azure

The emulator's disabled authentication is local-only. In Azure, use an authenticated App Configuration store, Microsoft Entra RBAC, explicit key and label selection, and a refresh interval appropriate for the application.
