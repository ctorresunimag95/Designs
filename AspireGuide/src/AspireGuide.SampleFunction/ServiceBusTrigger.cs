using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace AspireGuide.SampleFunction;

public class ServiceBusTrigger
{
    private readonly ILogger<ServiceBusTrigger> _logger;

    public ServiceBusTrigger(ILogger<ServiceBusTrigger> logger)
    {
        _logger = logger;
    }

    [Function(nameof(ServiceBusTrigger))]
    public void Run(
        [ServiceBusTrigger(topicName: "my.topic.v1", subscriptionName: "sample-function-subscription", Connection = "SERVICE_BUS_CONNECTION_STRING")]
        ServiceBusReceivedMessage message,
        ServiceBusMessageActions messageActions)
    {
        _logger.LogInformation("C# ServiceBus queue trigger function processed message: {MessageBody}", message.Body.ToString());
    }
}