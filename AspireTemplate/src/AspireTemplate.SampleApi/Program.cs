using AspireTemplate.SampleApi.AppConfiguration;
using AspireTemplate.SampleApi.BlobStorage;
using AspireTemplate.SampleApi.KeyVault;
using AspireTemplate.SampleApi.ServiceBus;
using Azure.Core;
using Microsoft.Extensions.Azure;
using Microsoft.FeatureManagement;

var builder = WebApplication.CreateBuilder(args);

builder.UseAzureKeyVault();

builder.Configuration.AddAzureAppConfiguration(options =>
{
    options.Connect(builder.Configuration.GetConnectionString("appConfiguration"));
    options.ConfigureClientOptions(clientOptions => clientOptions.AddPolicy(new RemoveAuthorizationHeaderPolicy(), HttpPipelinePosition.PerRetry));
    // Reload configuration if any key-values have changed.
    options.ConfigureRefresh(refreshOptions =>
        refreshOptions.RegisterAll());
    options.UseFeatureFlags(featureFlagOptions =>
    {
        // When no parameter is passed to the UseFeatureFlags method, it loads all feature flags with no label in your App Configuration store. The default refresh interval of feature flags is 30 seconds.
        featureFlagOptions.SetRefreshInterval(TimeSpan.FromSeconds(10));
    });
});
// Add Azure App Configuration middleware to the container of services.
builder.Services.AddAzureAppConfiguration();
// Add feature management to the container of services.
builder.Services.AddFeatureManagement();

builder.AddServiceDefaults();
builder.AddApiAuthentication();

builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("Database")!, name: "database", tags: ["ready"]);

builder.Services.AddServiceBusProcessors();

builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(builder.Configuration.GetConnectionString("AzureBlobStorage"));
});
builder.Services.AddBlobStorageService();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.UseAzureAppConfiguration();

app.MapDefaultEndpoints();

app.MapGet("/api/files", async (IBlobStorageService blobStorageService, CancellationToken cancellationToken) =>
{
    var fileNames = await blobStorageService.GetFileNamesAsync("sample-data", null, cancellationToken);
    return Results.Ok(fileNames);
});

app.MapGet("/api/read-config", (IConfiguration configuration) =>
{
    var secretValue = configuration["Test"];
    return Results.Ok($"Secret value from Key Vault: {secretValue}");
});

// Protected endpoints — require a valid JWT issued by the configured Authority.
// Locally: get a token from Keycloak. In production: get a token from Azure AD.
// The same Authorization header format works for both.

app.MapGet("/api/secure/files", async (IBlobStorageService blobStorageService, CancellationToken cancellationToken) =>
{
    var fileNames = await blobStorageService.GetFileNamesAsync("sample-data", null, cancellationToken);
    return Results.Ok(fileNames);
}).RequireAuthorization("ApiReader");

app.MapGet("/api/secure/whoami", (HttpContext context) =>
{
    var claims = context.User.Claims.Select(c => new { c.Type, c.Value });
    return Results.Ok(claims);
}).RequireAuthorization();

app.MapGet("/api/flag", (IFeatureManager featureManager) =>
{
    var isEnabled = featureManager.IsEnabledAsync("Beta");
    return Results.Ok($"Feature flag 'Beta' is enabled: {isEnabled.Result}");
});

await app.Services.StartServiceBusProcessorsAsync();

app.Run();

