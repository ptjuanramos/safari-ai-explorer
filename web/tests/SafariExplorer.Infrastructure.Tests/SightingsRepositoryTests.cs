using Microsoft.Extensions.Caching.Memory;
using SafariExplorer.Infrastructure.FileSystem;
using SafariExplorer.Infrastructure.JobData;
using SafariExplorer.Tests;

namespace SafariExplorer.Infrastructure.Tests;

public sealed class SightingsRepositoryTests : IDisposable
{
    private static readonly DateOnly RunDate = new(2026, 11, 15);

    private readonly TestDataFolder _data = new();

    private SightingsRepository Repository => new(new LocalJobStore(_data.Root), new MemoryCache(new MemoryCacheOptions()));

    public void Dispose() => _data.Dispose();

    [Fact]
    public async Task Latest_run_reads_the_manifest_layers_and_tile_index()
    {
        _data.WithSightingsRun("2026-11-15").WithLatest("sightings", "2026-11-15");

        var run = await Repository.GetLatestRunAsync(CancellationToken.None);

        Assert.NotNull(run);
        Assert.Equal((RunDate, 2017, 2026, 19441), (run.Date, run.StartYear, run.EndYear, run.Records));
        Assert.Contains(run.Species, s => s is { Name: "lion", SpeciesKey: 5219404 });
        Assert.Equal([5000, 250], run.Layers.Select(l => l.SigmaM));

        var coarse = run.Layers[0];
        Assert.Null(coarse.MaxUncertaintyM);
        Assert.Equal(1900, coarse.SpeciesRecords["lion"]);
        Assert.Equal((2, 1), (coarse.Grid.Nx, coarse.Grid.Ny));
        Assert.Equal([(0, 0)], coarse.Tiles.Select(t => (t.Tx, t.Ty)));

        var fine = run.Layers[1];
        Assert.Equal(250, fine.MaxUncertaintyM);
        Assert.Empty(fine.Tiles);
    }

    [Fact]
    public async Task No_latest_means_no_run()
    {
        _data.WithSightingsRun("2026-11-15");

        Assert.Null(await Repository.GetLatestRunAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Tile_keeps_blank_cells_as_null()
    {
        _data.WithSightingsRun("2026-11-15");

        var tile = await Repository.GetTileAsync(RunDate, "lion", 5000, 0, 0, CancellationToken.None);

        Assert.NotNull(tile);
        Assert.Equal(("EPSG:32736", 0, 0, 2, 1, 2500.0), (tile.Crs, tile.Tx, tile.Ty, tile.Nx, tile.Ny, tile.CellM));
        Assert.Equal([0.12, null], tile.Share);
        Assert.Equal([25.5, 1.2], tile.N);
    }

    [Theory]
    [InlineData("zebra", 5000, 0, 0)]
    [InlineData("lion", 3000, 0, 0)]
    [InlineData("lion", 5000, 1, 0)]
    [InlineData("lion", 250, 0, 0)]
    [InlineData("../lion", 5000, 0, 0)]
    [InlineData("buffalo", 5000, 0, 0)]
    public async Task Unknown_species_width_tile_or_missing_file_is_null(string species, int sigmaM, int tx, int ty)
    {
        _data.WithSightingsRun("2026-11-15");

        Assert.Null(await Repository.GetTileAsync(RunDate, species, sigmaM, tx, ty, CancellationToken.None));
    }
}
