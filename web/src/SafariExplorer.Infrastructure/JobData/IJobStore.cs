namespace SafariExplorer.Infrastructure.JobData;

/// <summary>
/// Where the jobs' files are read from: a local folder (<c>DATA_DIR</c>) or a blob container. Keys always use
/// <c>/</c>, the same as blob names (<c>road-risk/&lt;place&gt;/&lt;date&gt;/manifest.json</c>), so both stores hold the
/// same layout (docs/data-contract.md). The repositories parse the files; a store only finds and reads them.
/// </summary>
public interface IJobStore
{
    /// <summary>The file's JSON, or null when there is no such file.</summary>
    Task<T?> ReadJsonAsync<T>(string key, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken);

    /// <summary>Every key under the folder, at any depth, in no particular order. Empty when the folder is missing.</summary>
    IAsyncEnumerable<string> ListKeysAsync(string folder, CancellationToken cancellationToken);
}
