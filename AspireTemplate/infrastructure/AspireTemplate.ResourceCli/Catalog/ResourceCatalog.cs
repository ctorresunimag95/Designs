namespace AspireTemplate.ResourceCli.Catalog;

public static class ResourceCatalog
{
    // Deterministic insertion order used when editing AppHost.cs
    public static readonly IReadOnlyList<string> InsertionOrder =
    [
        "servicebus",
        "blobstorage",
        "appconfiguration",
        "keycloak",
        "sbexplorer",
    ];

    // Maps resource key → legacy #region name for backward-compatible detection
    public static readonly IReadOnlyDictionary<string, string> LegacyRegionNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["keycloak"] = "Identity",
        };

    public static readonly IReadOnlyList<ResourceDefinition> All =
    [
        new ResourceDefinition(
            Key: "servicebus",
            DisplayName: "Service Bus",
            Description: "Azure Service Bus emulator with queues, a topic + subscription, and the interactive 'Send Test Message' command. Pulls in the ServiceBusExtensions helper.",
            Type: ResourceType.Infrastructure,
            RegionTitle: "Service Bus",
            Dependencies: [],
            Packages:
            [
                new("Aspire.Hosting.Azure.ServiceBus", "13.4.6"),
                new("Azure.Messaging.ServiceBus", "7.20.1"),
            ],
            CompanionFiles:
            [
                new("Extensions/ServiceBusExtensions.cs", "Extensions/ServiceBusExtensions.cs", Required: true),
            ]),

        new ResourceDefinition(
            Key: "blobstorage",
            DisplayName: "Blob Storage",
            Description: "Azure Storage via Azurite (blob/queue/table ports, persistent volume) with blobs, queues, tables, and the 'Seed Sample Data' load command. Pulls in BlobExtensions and Content/SampleFiles.",
            Type: ResourceType.Infrastructure,
            RegionTitle: "Blob Storage",
            Dependencies: [],
            Packages:
            [
                new("Aspire.Hosting.Azure.Storage", "13.4.6"),
                new("Azure.Storage.Blobs", "12.29.1"),
            ],
            CompanionFiles:
            [
                new("Extensions/BlobExtensions.cs", "Extensions/BlobExtensions.cs", Required: true),
                new("Content/SampleFiles/Sample.txt", "Content/SampleFiles/Sample.txt", Required: false),
            ]),

        new ResourceDefinition(
            Key: "appconfiguration",
            DisplayName: "App Configuration",
            Description: "Azure App Configuration emulator (auth disabled, host port 28000, persistent data volume).",
            Type: ResourceType.Infrastructure,
            RegionTitle: "App Configuration",
            Dependencies: [],
            Packages:
            [
                new("Aspire.Hosting.Azure.AppConfiguration", "13.4.6"),
            ],
            CompanionFiles: []),

        new ResourceDefinition(
            Key: "keycloak",
            DisplayName: "Keycloak",
            Description: "Local Keycloak on port 8080 with the pre-seeded aspire-guide realm. Pulls in KeycloakExtensions and the realm-export.json companion file.",
            Type: ResourceType.Infrastructure,
            RegionTitle: "Identity",  // legacy region name — see LegacyRegionNames
            Dependencies: [],
            Packages:
            [
                new("Aspire.Hosting.Keycloak", "13.4.6-preview.1.26319.6"),
            ],
            CompanionFiles:
            [
                new("Keycloak/realm-export.json", "Keycloak/realm-export.json", Required: true),
                new("Extensions/KeycloakExtensions.cs", "Extensions/KeycloakExtensions.cs", Required: true),
            ]),

        new ResourceDefinition(
            Key: "sbexplorer",
            DisplayName: "Service Bus Explorer",
            Description: "Blazor UI for browsing Service Bus queues/topics, peeking and resubmitting dead-letter messages against the local emulator. Requires the servicebus resource and the AspireTemplate.ServiceBusExplorer project.",
            Type: ResourceType.Integration,
            RegionTitle: "Service Bus Explorer",
            Dependencies: ["servicebus"],
            Packages: [],
            CompanionFiles: [],
            RequiredProject: new ProjectRequirement(
                ProjectName: "AspireTemplate_ServiceBusExplorer",
                RelativePath: "infrastructure/AspireTemplate.ServiceBusExplorer/AspireTemplate.ServiceBusExplorer.csproj",
                ScaffoldFiles:
                [
                    "AspireTemplate.ServiceBusExplorer.csproj",
                    "appsettings.json",
                    "appsettings.Development.json",
                    "Program.cs",
                    "Components/App.razor",
                    "Components/Layout/MainLayout.razor",
                    "Components/Pages/Browse.razor",
                    "Components/Pages/Error.razor",
                    "Components/Pages/Publish.razor",
                    "Components/Routes.razor",
                    "Components/_Imports.razor",
                    "Models/EntityCatalogOptions.cs",
                    "Models/MessageView.cs",
                    "Properties/launchSettings.json",
                    "Services/ServiceBusBrowser.cs",
                    "wwwroot/app.css",
                ])),
    ];

    public static ResourceDefinition? Find(string key) =>
        All.FirstOrDefault(r => string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase));
}
