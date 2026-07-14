using Azure.Messaging.ServiceBus;

using AspireGuide.ServiceBusExplorer.Models;

namespace AspireGuide.ServiceBusExplorer.Services;

public sealed class ServiceBusBrowser
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<ServiceBusBrowser> _logger;

    public ServiceBusBrowser(ServiceBusClient client, ILogger<ServiceBusBrowser> logger)
    {
        _client = client;
        _logger = logger;
    }

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
                if (string.IsNullOrWhiteSpace(key)) continue;

                message.ApplicationProperties[key] = value;
            }
        }

        await sender.SendMessageAsync(message, cancellationToken);
        _logger.LogInformation("Published message to {Entity}", entity);
    }

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
