using Azure.Messaging.ServiceBus;

using AspireGuide.ServiceBusExplorer.Models;

namespace AspireGuide.ServiceBusExplorer.Services;

/// <summary>
/// Thin wrapper over <see cref="ServiceBusClient"/> providing the operations the troubleshooting UI
/// needs: publishing messages, non-destructively peeking active and dead-letter messages, and
/// resubmitting or purging dead-letter messages. Intended for LOCAL troubleshooting only.
/// </summary>
public sealed class ServiceBusBrowser
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<ServiceBusBrowser> _logger;

    public ServiceBusBrowser(ServiceBusClient client, ILogger<ServiceBusBrowser> logger)
    {
        _client = client;
        _logger = logger;
    }

    /// <summary>Publishes a single message to a queue or topic.</summary>
    public async Task SendAsync(
        string entity,
        string body,
        IDictionary<string, string>? applicationProperties = null,
        string? subject = null,
        string? correlationId = null,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);

        await using var sender = _client.CreateSender(entity);

        var message = new ServiceBusMessage(body);
        if (!string.IsNullOrWhiteSpace(subject)) message.Subject = subject;
        if (!string.IsNullOrWhiteSpace(correlationId)) message.CorrelationId = correlationId;
        if (!string.IsNullOrWhiteSpace(contentType)) message.ContentType = contentType;

        if (applicationProperties is not null)
        {
            foreach (var (key, value) in applicationProperties)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    message.ApplicationProperties[key] = value;
                }
            }
        }

        await sender.SendMessageAsync(message, cancellationToken);
        _logger.LogInformation("Published message to {Entity}", entity);
    }

    /// <summary>
    /// Peeks (non-destructively) up to <paramref name="maxMessages"/> messages from the active or
    /// dead-letter view of a queue or topic subscription.
    /// </summary>
    public async Task<IReadOnlyList<MessageView>> PeekAsync(
        string entity,
        string? subscription,
        bool deadLetter,
        int maxMessages,
        CancellationToken cancellationToken = default)
    {
        var options = new ServiceBusReceiverOptions
        {
            SubQueue = deadLetter ? SubQueue.DeadLetter : SubQueue.None,
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
        };

        await using var receiver = CreateReceiver(entity, subscription, options);

        var messages = await receiver.PeekMessagesAsync(maxMessages, cancellationToken: cancellationToken);
        return messages.Select(ToView).ToList();
    }

    /// <summary>
    /// Receives dead-letter messages, re-sends their body and properties to the main entity, then
    /// completes them so they leave the dead-letter queue. Returns the number resubmitted.
    /// </summary>
    public async Task<int> ResubmitDeadLetterAsync(
        string entity,
        string? subscription,
        int maxMessages,
        CancellationToken cancellationToken = default)
    {
        var options = new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter };
        await using var receiver = CreateReceiver(entity, subscription, options);
        await using var sender = _client.CreateSender(entity);

        var received = await receiver.ReceiveMessagesAsync(maxMessages, TimeSpan.FromSeconds(5), cancellationToken);

        var count = 0;
        foreach (var message in received)
        {
            var resubmitted = CloneForResubmit(message);
            await sender.SendMessageAsync(resubmitted, cancellationToken);
            await receiver.CompleteMessageAsync(message, cancellationToken);
            count++;
        }

        _logger.LogInformation("Resubmitted {Count} dead-letter message(s) to {Entity}", count, entity);
        return count;
    }

    /// <summary>
    /// Receives dead-letter messages matching the provided sequence numbers, re-sends selected ones,
    /// completes them, and abandons the rest. Returns the number resubmitted.
    /// </summary>
    public async Task<int> ResubmitDeadLetterBySequenceNumbersAsync(
        string entity,
        string? subscription,
        IReadOnlyList<long> sequenceNumbers,
        int maxMessages,
        CancellationToken cancellationToken = default)
    {
        var options = new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter };
        await using var receiver = CreateReceiver(entity, subscription, options);
        await using var sender = _client.CreateSender(entity);

        var received = await receiver.ReceiveMessagesAsync(maxMessages, TimeSpan.FromSeconds(5), cancellationToken);
        var sequenceSet = new HashSet<long>(sequenceNumbers);

        var count = 0;
        foreach (var message in received)
        {
            if (sequenceSet.Contains(message.SequenceNumber))
            {
                var resubmitted = CloneForResubmit(message);
                await sender.SendMessageAsync(resubmitted, cancellationToken);
                await receiver.CompleteMessageAsync(message, cancellationToken);
                count++;
            }
            else
            {
                await receiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
            }
        }

        _logger.LogInformation("Resubmitted {Count} selected dead-letter message(s) to {Entity}", count, entity);
        return count;
    }

    /// <summary>
    /// Receives and completes dead-letter messages, permanently removing them. Returns the number purged.
    /// </summary>
    public async Task<int> PurgeDeadLetterAsync(
        string entity,
        string? subscription,
        int maxMessages,
        CancellationToken cancellationToken = default)
    {
        var options = new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter };
        await using var receiver = CreateReceiver(entity, subscription, options);

        var received = await receiver.ReceiveMessagesAsync(maxMessages, TimeSpan.FromSeconds(5), cancellationToken);

        var count = 0;
        foreach (var message in received)
        {
            await receiver.CompleteMessageAsync(message, cancellationToken);
            count++;
        }

        _logger.LogWarning("Purged {Count} dead-letter message(s) from {Entity}", count, entity);
        return count;
    }

    private ServiceBusReceiver CreateReceiver(string entity, string? subscription, ServiceBusReceiverOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entity);

        return string.IsNullOrWhiteSpace(subscription)
            ? _client.CreateReceiver(entity, options)
            : _client.CreateReceiver(entity, subscription, options);
    }

    private static ServiceBusMessage CloneForResubmit(ServiceBusReceivedMessage message)
    {
        var clone = new ServiceBusMessage(message.Body)
        {
            Subject = message.Subject,
            CorrelationId = message.CorrelationId,
            ContentType = message.ContentType,
        };

        if (!string.IsNullOrEmpty(message.MessageId))
        {
            clone.MessageId = message.MessageId;
        }

        foreach (var (key, value) in message.ApplicationProperties)
        {
            clone.ApplicationProperties[key] = value;
        }

        return clone;
    }

    private static MessageView ToView(ServiceBusReceivedMessage message) => new()
    {
        SequenceNumber = message.SequenceNumber,
        MessageId = message.MessageId,
        Subject = message.Subject,
        CorrelationId = message.CorrelationId,
        ContentType = message.ContentType,
        Body = message.Body.ToString(),
        DeliveryCount = message.DeliveryCount,
        EnqueuedTime = message.EnqueuedTime,
        DeadLetterReason = message.DeadLetterReason,
        DeadLetterErrorDescription = message.DeadLetterErrorDescription,
        ApplicationProperties = message.ApplicationProperties
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty),
    };
}
