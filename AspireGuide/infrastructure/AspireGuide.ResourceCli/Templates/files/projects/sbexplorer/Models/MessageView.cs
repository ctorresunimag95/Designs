namespace AspireGuide.ServiceBusExplorer.Models;

public sealed class MessageView
{
    public long SequenceNumber { get; init; }

    public string MessageId { get; init; } = string.Empty;

    public string? Subject { get; init; }

    public string? CorrelationId { get; init; }

    public string? ContentType { get; init; }

    public string Body { get; init; } = string.Empty;

    public int DeliveryCount { get; init; }

    public DateTimeOffset EnqueuedTime { get; init; }

    public string? DeadLetterReason { get; init; }

    public string? DeadLetterErrorDescription { get; init; }

    public IReadOnlyDictionary<string, string> ApplicationProperties { get; init; } =
        new Dictionary<string, string>();
}
