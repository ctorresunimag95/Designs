using Azure.Messaging.ServiceBus;

namespace AspireTemplate.SampleApi.ServiceBus;

internal class SampleMessageProcessor : BaseProcessor
{
    public SampleMessageProcessor(ILogger<SampleMessageProcessor> logger, IConfiguration configuration, IServiceProvider serviceProvider)
        : base(logger, configuration, serviceProvider)
    {
    }

    public override ServiceBusProcessor CreateProcessor(ServiceBusClient serviceBusClient)
    {
        return serviceBusClient.CreateProcessor("my.topic.v1", "my-topic-subscription", new ServiceBusProcessorOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
        });
    }

    public override async Task MessageHandler(ProcessMessageEventArgs args)
    {
        var messageBody = args.Message.Body.ToString();
        _logger.LogInformation("Received message: {MessageBody}", messageBody);

        // Complete the message after processing
        await args.CompleteMessageAsync(args.Message);
    }

    public override Task ErrorHandler(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception, "Error processing message: {ErrorSource}", args.ErrorSource);
        return Task.CompletedTask;
    }
}