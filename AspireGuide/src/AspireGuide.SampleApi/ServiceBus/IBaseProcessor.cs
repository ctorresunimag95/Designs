namespace AspireGuide.SampleApi.ServiceBus;

public interface IBaseProcessor
{
    Task StartProcessingAsync();
}