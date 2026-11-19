using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;

namespace SafariExplorer.Infrastructure.AzureStorage;

/// <summary>
/// Builds the storage clients: from the connection string when one is set (Azurite, quick tests), otherwise from the
/// account URL signed in with <c>DefaultAzureCredential</c> (<c>az login</c> locally, managed identity in Azure), so
/// no key is needed in Azure. Building a client doesn't connect.
/// </summary>
internal static class AzureStorageClients
{
    private static readonly Lazy<TokenCredential> Credential = new(() => new DefaultAzureCredential());

    public static BlobContainerClient Container(StorageOptions options) =>
        options.ConnectionString is { Length: > 0 } connectionString
            ? new BlobContainerClient(connectionString, options.Container)
            : new BlobServiceClient(BlobUri(options), Credential.Value).GetBlobContainerClient(options.Container);

    public static TableClient Table(StorageOptions options) =>
        options.ConnectionString is { Length: > 0 } connectionString
            ? new TableClient(connectionString, options.UserPathsTable)
            : new TableClient(TableUri(options), options.UserPathsTable, Credential.Value);

    private static Uri BlobUri(StorageOptions options) =>
        new(options.AccountUrl ?? throw new InvalidOperationException(options.AzureSettingsProblem()));

    /// <summary>The table endpoint of the same account: <c>&lt;account&gt;.table.core.windows.net</c>.</summary>
    internal static Uri TableUri(StorageOptions options)
    {
        var blob = BlobUri(options);
        if (!blob.Host.Contains(".blob.", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"STORAGE_ACCOUNT_URL must be the blob endpoint (https://<account>.blob.core.windows.net), got {blob}.");
        }

        return new UriBuilder(blob) { Host = blob.Host.Replace(".blob.", ".table.", StringComparison.OrdinalIgnoreCase) }.Uri;
    }
}
