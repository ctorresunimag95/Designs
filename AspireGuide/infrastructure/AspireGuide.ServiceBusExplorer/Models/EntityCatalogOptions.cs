namespace AspireGuide.ServiceBusExplorer.Models;

/// <summary>
/// Static list of Service Bus entities shown in the UI dropdowns. The Service Bus emulator does
/// not support management operations, so entities cannot be discovered at runtime — this catalog
/// mirrors what is declared in the AppHost. Users can also type a free-text entity name in the UI.
/// Bound from configuration section "ServiceBus:Entities".
/// </summary>
public sealed class EntityCatalogOptions
{
    public const string SectionName = "ServiceBus:Entities";

    public List<string> Queues { get; set; } = [];

    public List<TopicCatalog> Topics { get; set; } = [];
}

public sealed class TopicCatalog
{
    public string Name { get; set; } = string.Empty;

    public List<string> Subscriptions { get; set; } = [];
}
