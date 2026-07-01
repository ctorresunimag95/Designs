using Azure.Messaging.ServiceBus;

namespace AspireGuide.SampleApi.ServiceBus;

internal abstract class BaseProcessor : IBaseProcessor, IAsyncDisposable
{
    private readonly ServiceBusClient _serviceBusClient;
    private ServiceBusProcessor? _processor;
    protected readonly ILogger _logger;

    protected BaseProcessor(ILogger logger, IConfiguration configuration, IServiceProvider serviceProvider)
    {
        _logger = logger;

        var connectionString = configuration.GetConnectionString("AzureServiceBus");
        _serviceBusClient = new ServiceBusClient(connectionString);
    }

    public abstract ServiceBusProcessor CreateProcessor(ServiceBusClient serviceBusClient);

    public abstract Task MessageHandler(ProcessMessageEventArgs args);

    public abstract Task ErrorHandler(ProcessErrorEventArgs args);

    public async Task StartProcessingAsync()
    {
        _processor = CreateProcessor(_serviceBusClient);
        _logger.LogInformation("Starting Service Bus processor for Queue/Topic: {EntityPath}", _processor.EntityPath);

        _processor.ProcessMessageAsync += MessageHandler;
        _processor.ProcessErrorAsync += ErrorHandler;

        await _processor.StartProcessingAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor != null)
        {
            _logger.LogInformation("Stopping Service Bus processor for Queue/Topic: {EntityPath}", _processor.EntityPath);
            await _processor.StopProcessingAsync();
            await _processor.DisposeAsync();
        }

        await _serviceBusClient.DisposeAsync();
    }
}

