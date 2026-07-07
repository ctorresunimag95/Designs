using AspireGuide.SampleApi.BlobStorage;
using AspireGuide.SampleApi.KeyVault;
using AspireGuide.SampleApi.ServiceBus;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);

builder.UseAzureKeyVault();

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

await app.Services.StartServiceBusProcessorsAsync();

app.Run();

