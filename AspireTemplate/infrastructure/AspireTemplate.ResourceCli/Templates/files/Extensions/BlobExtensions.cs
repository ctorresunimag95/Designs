using System.Collections.Concurrent;
using Aspire.Hosting.Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace {{Namespace}};

/// <summary>
/// Extension methods for the Azure Blob Storage resource.
/// </summary>
internal static class BlobExtensions
{
    // BlobServiceClient is thread-safe and intended to be long-lived, so reuse one
    // instance per connection string across command invocations instead of creating
    // a new client every time the command runs.
    private static readonly ConcurrentDictionary<string, BlobServiceClient> BlobServiceClients = new();

    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    /// <summary>
    /// Add a command to load data into an Azure Blob Storage container from a specified file or directory path.
    /// The command will create the container if it does not exist and upload the files to the container, preserving the directory structure if a directory is specified.
    /// </summary>
    /// <param name="builder">The Azure Blob Storage resource builder.</param>
    /// <param name="commandName">The unique command name.</param>
    /// <param name="displayName">The display name shown in Aspire.</param>
    /// <param name="containerName">The name of the blob container.</param>
    /// <param name="filePath">The path to the file or directory to upload.</param>
    /// <param name="blobOptionsModifier">Optional delegate to modify BlobUploadOptions for each blob (e.g., to set AccessTier).</param>
    /// <returns>A builder for the Azure Blob Storage resource.</returns>
    public static IResourceBuilder<AzureBlobStorageResource> WithDataLoadCommand(
        this IResourceBuilder<AzureBlobStorageResource> builder,
        string commandName,
        string displayName,
        string containerName,
        string filePath,
        Action<BlobUploadOptions>? blobOptionsModifier = null)
    {
        builder.WithCommand(commandName, displayName, async context =>
        {
            var cancellationToken = context.CancellationToken;
            var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();
            try
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(containerName);
                ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

                var connectionString = await builder.Resource.ConnectionStringExpression.GetValueAsync(cancellationToken);
                ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

                var blobServiceClient = BlobServiceClients.GetOrAdd(connectionString, cs => new BlobServiceClient(cs));
                var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

                _ = await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

                await SeedDataFromPathAsync(containerClient, filePath, logger, cancellationToken, blobOptionsModifier);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding blob data from {FilePath}", filePath);
                return CommandResults.Failure(ex.Message);
            }
            return CommandResults.Success("Data load command executed successfully.");
        }, new CommandOptions
        {
            IconName = "ArrowUpload",
        });

        return builder;
    }

    private static async Task SeedDataFromPathAsync(BlobContainerClient containerClient, string filePath, ILogger<Program> logger, CancellationToken cancellationToken, Action<BlobUploadOptions>? blobOptionsModifier = null)
    {
        if (Directory.Exists(filePath))
        {
            var files = Directory.GetFiles(filePath, "*", SearchOption.AllDirectories);
            // NOTE: uploads run sequentially. For large seed sets this could be parallelized
            // with bounded concurrency (e.g. Parallel.ForEachAsync with MaxDegreeOfParallelism).
            foreach (var file in files)
            {
                var blobName = Path.GetRelativePath(filePath, file).Replace(Path.DirectorySeparatorChar, '/');
                await UploadFileAsync(containerClient, file, blobName, logger, blobOptionsModifier, cancellationToken);
            }
            return;
        }

        if (File.Exists(filePath))
        {
            var blobName = Path.GetFileName(filePath);
            await UploadFileAsync(containerClient, filePath, blobName, logger, blobOptionsModifier, cancellationToken);
            return;
        }

        logger.LogWarning("The specified path {FilePath} does not exist.", filePath);
    }

    private static async Task UploadFileAsync(BlobContainerClient containerClient
        , string filePath
        , string blobName
        , ILogger<Program> logger
        , Action<BlobUploadOptions>? blobOptionsModifier = null
        , CancellationToken cancellationToken = default)
    {
        var blobClient = containerClient.GetBlobClient(blobName);

        // Infer the content type from the file extension so blobs are served with a
        // meaningful Content-Type rather than defaulting to application/octet-stream.
        var options = new BlobUploadOptions();
        if (ContentTypeProvider.TryGetContentType(filePath, out var contentType))
        {
            options.HttpHeaders = new BlobHttpHeaders { ContentType = contentType };
        }

        blobOptionsModifier?.Invoke(options);

        await blobClient.UploadAsync(filePath, options, cancellationToken);
        logger.LogInformation("Uploaded file {FilePath} to blob {BlobName}", filePath, blobName);
    }
}
