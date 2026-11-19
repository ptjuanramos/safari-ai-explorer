using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SafariExplorer.Infrastructure.JobData;

namespace SafariExplorer.Infrastructure.AzureStorage;

/// <summary>
/// The job files in a blob container, one blob per file under the same keys as the folders (ADR-0004). The API only
/// reads them, so it needs no more than the Storage Blob Data Reader role. A missing container is an error (wrong
/// settings), not "no data", so it isn't caught.
/// </summary>
public sealed class BlobJobStore(BlobContainerClient container) : IJobStore
{
    public async Task<T?> ReadJsonAsync<T>(string key, CancellationToken cancellationToken)
    {
        JobFiles.Segments(key);
        try
        {
            var download = await container.GetBlobClient(key).DownloadStreamingAsync(cancellationToken: cancellationToken);
            await using var stream = download.Value.Content;
            return await JsonSerializer.DeserializeAsync<T>(stream, JobFiles.Json, cancellationToken);
        }
        catch (RequestFailedException e) when (e.ErrorCode == BlobErrorCode.BlobNotFound)
        {
            return default;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
    {
        JobFiles.Segments(key);
        return await container.GetBlobClient(key).ExistsAsync(cancellationToken);
    }

    public async IAsyncEnumerable<string> ListKeysAsync(string folder,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        JobFiles.Segments(folder);
        var blobs = container.GetBlobsAsync(BlobTraits.None, BlobStates.None, $"{folder}/", cancellationToken);
        await foreach (var blob in blobs)
        {
            yield return blob.Name;
        }
    }
}
