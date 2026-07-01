using AspireGuide.SampleApi.BlobStorage;
using AspireGuide.SampleApi.ServiceBus;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddServiceBusProcessors();

builder.Services.AddAzureClients(clientBuilder =>
{
    clientBuilder.AddBlobServiceClient(builder.Configuration.GetConnectionString("AzureBlobStorage"));
});
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

var app = builder.Build();

app.MapGet("/api/files", async (IBlobStorageService blobStorageService, CancellationToken cancellationToken) =>
{
    var fileNames = await blobStorageService.GetFileNamesAsync("sample-data", null, cancellationToken);
    return Results.Ok(fileNames);
});

await app.Services.StartServiceBusProcessorsAsync();

app.Run();

