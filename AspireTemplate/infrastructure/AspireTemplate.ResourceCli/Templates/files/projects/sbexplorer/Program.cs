using AspireTemplate.ServiceBusExplorer.Components;
using AspireTemplate.ServiceBusExplorer.Models;
using AspireTemplate.ServiceBusExplorer.Services;

var builder = WebApplication.CreateBuilder(args);

// Registers a ServiceBusClient for the "AzureServiceBus" connection injected by the AppHost.
builder.AddAzureServiceBusClient("AzureServiceBus");

builder.Services.Configure<EntityCatalogOptions>(
    builder.Configuration.GetSection(EntityCatalogOptions.SectionName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ServiceBusBrowser>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
