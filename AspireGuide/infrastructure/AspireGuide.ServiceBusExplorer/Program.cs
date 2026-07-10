using AspireGuide.ServiceBusExplorer.Components;
using AspireGuide.ServiceBusExplorer.Models;
using AspireGuide.ServiceBusExplorer.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Registers a ServiceBusClient for the "AzureServiceBus" connection injected by the AppHost.
builder.AddAzureServiceBusClient("AzureServiceBus");

builder.Services.Configure<EntityCatalogOptions>(
    builder.Configuration.GetSection(EntityCatalogOptions.SectionName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ServiceBusBrowser>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
