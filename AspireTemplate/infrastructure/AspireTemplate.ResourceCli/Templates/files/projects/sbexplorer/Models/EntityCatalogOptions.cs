namespace AspireTemplate.ServiceBusExplorer.Models;

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
