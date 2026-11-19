using System.Net.Sockets;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SafariExplorer.Domain.RoadRisk;
using SafariExplorer.Domain.UserPaths;
using SafariExplorer.Infrastructure.AzureStorage;
using SafariExplorer.Infrastructure.FileSystem;
using SafariExplorer.Infrastructure.JobData;
using SafariExplorer.Tests;

namespace SafariExplorer.Infrastructure.Tests;

/// <summary>
/// The storage-account adapters against Azurite, the local emulator. They're skipped when it isn't running, so
/// <c>dotnet test web</c> still needs no network. Start it with <c>docker compose --profile azurite up -d azurite</c>.
/// Each test uses its own container or table and removes it afterwards.
/// </summary>
[Trait("Category", "Azurite")]
public sealed class AzuriteTests : IAsyncLifetime
{
    private const string ConnectionString = "UseDevelopmentStorage=true";

    private static readonly Lazy<bool> AzuriteRunning = new(() => Listening(10000) && Listening(10002));

    private readonly string _name = $"t{Guid.NewGuid():N}";
    private readonly TestDataFolder _data = new();
    private BlobContainerClient? _container;
    private TableClient? _table;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _data.Dispose();
        if (_container is not null)
        {
            await _container.DeleteIfExistsAsync();
        }

        if (_table is not null)
        {
            try
            {
                await _table.DeleteAsync();
            }
            catch (Azure.RequestFailedException)
            {
                // The test skipped or failed before the table was created.
            }
        }
    }

    [SkippableFact]
    public async Task Repositories_read_the_same_runs_from_blobs_as_from_folders()
    {
        _data.WithRoadRun("2026-11-14").WithRoadRun("2026-11-15").WithLatest("road-risk/test-park", "2026-11-15")
            .WithRoadRun("2026-11-15", place: "no-latest-yet")
            .WithSightingsRun("2026-11-15").WithLatest("sightings", "2026-11-15");
        var blobs = new BlobJobStore(await UploadDataFolderAsync());
        var files = new LocalJobStore(_data.Root);

        foreach (var store in new IJobStore[] { files, blobs })
        {
            var roads = new RoadRiskRepository(store);
            Assert.Equal(["test-park"], await roads.ListPlacesAsync(CancellationToken.None));
            Assert.Equal([new DateOnly(2026, 11, 14), new DateOnly(2026, 11, 15)],
                await roads.ListDatesAsync("test-park", CancellationToken.None));
            Assert.Equal(new DateOnly(2026, 11, 15), await roads.GetLatestDateAsync("test-park", CancellationToken.None));
            var snapshot = await roads.GetAsync("test-park", new DateOnly(2026, 11, 15), CancellationToken.None);
            Assert.Equal(2, snapshot!.Segments.Count);
            Assert.Null(await roads.GetAsync("test-park", new DateOnly(2000, 1, 1), CancellationToken.None));

            var sightings = new SightingsRepository(store, new MemoryCache(new MemoryCacheOptions()));
            var run = await sightings.GetLatestRunAsync(CancellationToken.None);
            Assert.Equal(19441, run!.Records);
            var tile = await sightings.GetTileAsync(run.Date, "lion", 5000, 0, 0, CancellationToken.None);
            Assert.Equal([0.12, null], tile!.Share);
        }
    }

    [SkippableFact]
    public async Task A_missing_blob_or_folder_is_empty_and_bad_keys_are_refused()
    {
        var store = new BlobJobStore(await NewContainerAsync());

        Assert.Null(await store.ReadJsonAsync<object>("sightings/latest.json", CancellationToken.None));
        Assert.False(await store.ExistsAsync("sightings/latest.json", CancellationToken.None));
        Assert.Empty(await store.ListKeysAsync("road-risk", CancellationToken.None).ToListAsync());
        await Assert.ThrowsAsync<ArgumentException>(() => store.ReadJsonAsync<object>("../x.json", CancellationToken.None));
    }

    [SkippableFact]
    public async Task A_saved_path_reads_back_the_same_from_its_row()
    {
        var repository = new TableUserPathRepository(NewTable());

        Assert.Empty(await repository.ListAsync(CancellationToken.None));
        await repository.SaveAsync(Path1(), CancellationToken.None);

        var read = Assert.Single(await repository.ListAsync(CancellationToken.None));
        Assert.Equal(Path1() with { Stops = read.Stops }, read);
        Assert.Equal(Path1().Stops, read.Stops);

        var row = await _table!.GetEntityAsync<TableEntity>(TableUserPathRepository.Trip, "0123abcd");
        Assert.Equal(1, row.Value.GetInt32("Version"));
        Assert.Contains("\"lat\":-3.7167", row.Value.GetString("Stops"));
    }

    [SkippableFact]
    public async Task Saving_the_same_id_replaces_the_row_and_delete_removes_it_once()
    {
        var repository = new TableUserPathRepository(NewTable());

        await repository.SaveAsync(Path1(), CancellationToken.None);
        await repository.SaveAsync(Path1(origin: "Kuro"), CancellationToken.None);
        Assert.Equal("Kuro", Assert.Single(await repository.ListAsync(CancellationToken.None)).Origin);

        Assert.True(await repository.DeleteAsync("0123abcd", CancellationToken.None));
        Assert.False(await repository.DeleteAsync("0123abcd", CancellationToken.None));
        Assert.Empty(await repository.ListAsync(CancellationToken.None));
    }

    [SkippableFact]
    public async Task Broken_rows_and_ids_that_are_not_slugs_are_left_alone()
    {
        var repository = new TableUserPathRepository(NewTable());
        await repository.SaveAsync(Path1(), CancellationToken.None);
        await _table!.AddEntityAsync(new TableEntity(TableUserPathRepository.Trip, "broken") { ["Stops"] = "{ not json" });
        await _table.AddEntityAsync(new TableEntity(TableUserPathRepository.Trip, "half") { ["Origin"] = "Kuro" });

        Assert.Equal("0123abcd", Assert.Single(await repository.ListAsync(CancellationToken.None)).Id);
        Assert.False(await repository.DeleteAsync("A1", CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(Path1("a.b"), CancellationToken.None));
    }

    [SkippableFact]
    public async Task Import_copies_the_saved_files_to_the_table()
    {
        var files = new FileUserPathRepository(Options.Create(new DataOptions { Root = _data.Root }));
        await files.SaveAsync(Path1(), CancellationToken.None);
        await files.SaveAsync(Path1("4567cdef", "Kuro"), CancellationToken.None);
        var table = NewTable();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AZURE_STORAGE_CONNECTION_STRING"] = ConnectionString,
                ["Storage:UserPathsTable"] = table.Name,
            })
            .Build();

        Assert.Equal(2, await DependencyInjection.ImportUserPathsAsync(configuration, _data.Root, CancellationToken.None));
        Assert.Equal(2, await DependencyInjection.ImportUserPathsAsync(configuration, _data.Root, CancellationToken.None));

        var rows = await new TableUserPathRepository(table).ListAsync(CancellationToken.None);
        Assert.Equal(["0123abcd", "4567cdef"], rows.Select(p => p.Id).Order(StringComparer.Ordinal));
    }

    private static UserPath Path1(string id = "0123abcd", string origin = "Tarangire Gate") => new(
        id, 2, origin, "Silale Swamp", [new GeoPoint(36.0167, -3.7167), new GeoPoint(36.08, -4.0)], "track", 42.5, 95,
        new DateTimeOffset(2026, 11, 18, 9, 30, 0, TimeSpan.Zero));

    private async Task<BlobContainerClient> NewContainerAsync()
    {
        Skip.IfNot(AzuriteRunning.Value, "Azurite isn't running (docker compose --profile azurite up -d azurite).");
        _container = new BlobContainerClient(ConnectionString, _name);
        await _container.CreateAsync();
        return _container;
    }

    /// <summary>A new container holding the test data folder, each file under its relative path, as azcopy uploads it.</summary>
    private async Task<BlobContainerClient> UploadDataFolderAsync()
    {
        var container = await NewContainerAsync();
        foreach (var file in Directory.EnumerateFiles(_data.Root, "*", SearchOption.AllDirectories))
        {
            var key = Path.GetRelativePath(_data.Root, file).Replace(Path.DirectorySeparatorChar, '/');
            await container.UploadBlobAsync(key, BinaryData.FromBytes(await File.ReadAllBytesAsync(file)));
        }

        return container;
    }

    private TableClient NewTable()
    {
        Skip.IfNot(AzuriteRunning.Value, "Azurite isn't running (docker compose --profile azurite up -d azurite).");
        _table = new TableClient(ConnectionString, _name);
        return _table;
    }

    private static bool Listening(int port)
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync("127.0.0.1", port).Wait(TimeSpan.FromMilliseconds(300)) && client.Connected;
        }
        catch (Exception e) when (e is SocketException or AggregateException)
        {
            return false;
        }
    }
}
