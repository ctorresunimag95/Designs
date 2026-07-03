using AspireGuide.SampleApi.BlobStorage;
using AspireGuide.SampleApi.KeyVault;
using AspireGuide.SampleApi.ServiceBus;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);

builder.UseAzureKeyVault();

builder.AddServiceDefaults();

builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("Database")!, name: "database", tags: ["ready"]);

builder.Services.AddServiceBusProcessors();

builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(builder.Configuration.GetConnectionString("AzureBlobStorage"));
});
builder.Services.AddBlobStorageService();

var app = builder.Build();

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

await app.Services.StartServiceBusProcessorsAsync();

app.Run();

