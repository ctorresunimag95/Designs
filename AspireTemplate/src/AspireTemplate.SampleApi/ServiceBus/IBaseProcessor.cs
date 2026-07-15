namespace AspireTemplate.SampleApi.ServiceBus;

public interface IBaseProcessor
{
    Task StartProcessingAsync();
}