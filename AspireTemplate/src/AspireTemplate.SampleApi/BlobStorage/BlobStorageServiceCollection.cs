namespace AspireTemplate.SampleApi.BlobStorage;

internal static class BlobStorageServiceCollection
{
    public static IServiceCollection AddBlobStorageService(this IServiceCollection services)
    {
        services.AddScoped<IBlobStorageService, BlobStorageService>();
        return services;
    }
}