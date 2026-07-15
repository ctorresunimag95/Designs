namespace AspireTemplate.SampleApi.BlobStorage;

public interface IBlobStorageService
{
    Task<IReadOnlyList<string>> GetFileNamesAsync(string containerName, string? prefix = null, CancellationToken cancellationToken = default);
}