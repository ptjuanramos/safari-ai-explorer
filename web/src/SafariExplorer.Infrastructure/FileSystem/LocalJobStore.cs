using System.Runtime.CompilerServices;
using System.Text.Json;
using SafariExplorer.Infrastructure.JobData;

namespace SafariExplorer.Infrastructure.FileSystem;

/// <summary>The job files in a local folder (<c>DATA_DIR</c>), the shared Docker volume of ADR-0002.</summary>
public sealed class LocalJobStore(string root) : IJobStore
{
    public async Task<T?> ReadJsonAsync<T>(string key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        if (!File.Exists(path))
        {
            return default;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JobFiles.Json, cancellationToken);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken) =>
        Task.FromResult(File.Exists(PathFor(key)));

    public async IAsyncEnumerable<string> ListKeysAsync(string folder,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var dir = PathFor(folder);
        if (!Directory.Exists(dir))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return $"{folder}/{Path.GetRelativePath(dir, file).Replace(Path.DirectorySeparatorChar, '/')}";
        }

        await Task.CompletedTask;
    }

    private string PathFor(string key) => Path.Combine([root, .. JobFiles.Segments(key)]);
}
