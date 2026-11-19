using Microsoft.Extensions.Caching.Memory;
using SafariExplorer.Infrastructure.FileSystem;
using SafariExplorer.Infrastructure.JobData;
using SafariExplorer.Tests;

namespace SafariExplorer.Infrastructure.Tests;

public sealed class LocalJobStoreTests : IDisposable
{
    private readonly TestDataFolder _data = new();

    private LocalJobStore Store => new(_data.Root);

    public void Dispose() => _data.Dispose();

    [Fact]
    public async Task Keys_use_slashes_like_blob_names()
    {
        _data.WithRoadRun("2026-11-15").WithLatest("road-risk/test-park", "2026-11-15");

        var keys = await Store.ListKeysAsync("road-risk", CancellationToken.None).ToListAsync();

        Assert.Equal(
            ["road-risk/test-park/2026-11-15/edges.geojson", "road-risk/test-park/2026-11-15/manifest.json",
             "road-risk/test-park/latest.json"],
            keys.Order(StringComparer.Ordinal));
        Assert.True(await Store.ExistsAsync("road-risk/test-park/latest.json", CancellationToken.None));
    }

    [Fact]
    public async Task Missing_files_and_folders_are_empty_not_errors()
    {
        Assert.Null(await Store.ReadJsonAsync<object>("sightings/latest.json", CancellationToken.None));
        Assert.False(await Store.ExistsAsync("sightings/latest.json", CancellationToken.None));
        Assert.Empty(await Store.ListKeysAsync("sightings", CancellationToken.None).ToListAsync());
    }

    [Theory]
    [InlineData("../outside.json")]
    [InlineData("road-risk/../../outside.json")]
    [InlineData("road-risk//latest.json")]
    [InlineData("road-risk\\latest.json")]
    [InlineData("./latest.json")]
    public async Task Keys_that_could_leave_the_root_are_refused(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => Store.ReadJsonAsync<object>(key, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => Store.ExistsAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task A_second_tile_request_reads_the_manifest_once()
    {
        _data.WithSightingsRun("2026-11-15");
        var store = new CountingStore(Store);
        var repository = new SightingsRepository(store, new MemoryCache(new MemoryCacheOptions()));

        await repository.GetTileAsync(new DateOnly(2026, 11, 15), "lion", 5000, 0, 0, CancellationToken.None);
        await repository.GetTileAsync(new DateOnly(2026, 11, 15), "lion", 5000, 0, 0, CancellationToken.None);

        Assert.Equal(1, store.Reads["sightings/2026-11-15/manifest.json"]);
        Assert.Equal(2, store.Reads["sightings/2026-11-15/lion/5000m/0_0.json"]);
    }

    [Fact]
    public async Task A_missing_manifest_is_not_cached()
    {
        var store = new CountingStore(Store);
        var repository = new SightingsRepository(store, new MemoryCache(new MemoryCacheOptions()));

        Assert.Null(await repository.GetTileAsync(new DateOnly(2026, 11, 15), "lion", 5000, 0, 0, CancellationToken.None));
        _data.WithSightingsRun("2026-11-15");

        Assert.NotNull(await repository.GetTileAsync(new DateOnly(2026, 11, 15), "lion", 5000, 0, 0, CancellationToken.None));
    }

    /// <summary>Counts the reads of each key, to see what the repository asks the store for.</summary>
    private sealed class CountingStore(IJobStore inner) : IJobStore
    {
        public Dictionary<string, int> Reads { get; } = [];

        public Task<T?> ReadJsonAsync<T>(string key, CancellationToken cancellationToken)
        {
            Reads[key] = Reads.GetValueOrDefault(key) + 1;
            return inner.ReadJsonAsync<T>(key, cancellationToken);
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken) =>
            inner.ExistsAsync(key, cancellationToken);

        public IAsyncEnumerable<string> ListKeysAsync(string folder, CancellationToken cancellationToken) =>
            inner.ListKeysAsync(folder, cancellationToken);
    }
}
