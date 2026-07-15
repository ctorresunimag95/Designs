using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace AspireTemplate.SampleApi.BlobStorage;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;

    public BlobStorageService(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }

    public async Task<IReadOnlyList<string>> GetFileNamesAsync(string containerName, string? prefix = null, CancellationToken cancellationToken = default)
    {
        var fileNames = new List<string>();

        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var traits = BlobTraits.Metadata | BlobTraits.Tags;
        var states = BlobStates.Deleted | BlobStates.Snapshots;
        await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(traits, states, prefix: prefix, cancellationToken: cancellationToken))
        {
            fileNames.Add(blobItem.Name);
        }

        return fileNames.AsReadOnly();
    }
}