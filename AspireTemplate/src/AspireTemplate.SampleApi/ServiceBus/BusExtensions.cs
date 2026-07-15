namespace AspireTemplate.SampleApi.ServiceBus;

internal static class ServiceBusExtensions
{
    public static IServiceCollection AddServiceBusProcessors(this IServiceCollection services)
    {
        services.AddSingleton<IBaseProcessor, SampleMessageProcessor>();

        return services;
    }

    public static async Task StartServiceBusProcessorsAsync(this IServiceProvider serviceProvider)
    {
        var processors = serviceProvider.GetServices<IBaseProcessor>();

        foreach (var processor in processors)
        {
            await processor.StartProcessingAsync();
        }
    }
}