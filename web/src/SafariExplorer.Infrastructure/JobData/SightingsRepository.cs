using Microsoft.Extensions.Caching.Memory;
using SafariExplorer.Application.Abstractions;
using SafariExplorer.Domain.Sightings;

namespace SafariExplorer.Infrastructure.JobData;

/// <summary>
/// Sightings runs from <c>sightings/</c> in the job store. A tile is read only when the run's manifest lists it,
/// which also keeps every key inside the run's folder.
/// </summary>
public sealed class SightingsRepository(IJobStore store, IMemoryCache cache) : ISightingsRepository
{
    private const string Job = "sightings";

    /// <summary>
    /// How long a parsed manifest is kept. A manifest is written last and not changed afterwards (ADR-0002), and
    /// every tile request needs it, so this saves one read (one blob download) per tile. The limit only matters if a
    /// run of the same date is written again.
    /// </summary>
    public static readonly TimeSpan ManifestCacheTime = TimeSpan.FromMinutes(10);

    public async Task<SightingsRun?> GetLatestRunAsync(CancellationToken cancellationToken)
    {
        // latest.json isn't cached, so a new run shows up without a restart.
        var latest = await JobFiles.ReadLatestAsync(store, Job, cancellationToken);
        return latest is null ? null : await GetRunAsync(latest.Value, cancellationToken);
    }

    public async Task<SightingsTile?> GetTileAsync(DateOnly runDate, string species, int sigmaM, int tx, int ty,
        CancellationToken cancellationToken)
    {
        var run = await GetRunAsync(runDate, cancellationToken);
        if (run is null || !JobFiles.Slug().IsMatch(species) || !run.HasTile(species, sigmaM, tx, ty))
        {
            return null;
        }

        var key = $"{Job}/{JobFiles.RunId(runDate)}/{species}/{sigmaM}m/{tx}_{ty}.json";
        var tile = await store.ReadJsonAsync<TileFile>(key, cancellationToken);
        return tile is null ? null : ToTile(tile);
    }

    private async Task<SightingsRun?> GetRunAsync(DateOnly runDate, CancellationToken cancellationToken)
    {
        var cacheKey = (Job, runDate);
        if (cache.TryGetValue(cacheKey, out SightingsRun? cached))
        {
            return cached;
        }

        var manifest = await store.ReadJsonAsync<SightingsManifest>(JobFiles.ManifestKey(Job, runDate), cancellationToken);
        if (manifest is null)
        {
            // Not cached: the run may still be being written.
            return null;
        }

        return cache.Set(cacheKey, ToRun(manifest), ManifestCacheTime);
    }

    private static SightingsRun ToRun(SightingsManifest m) => new(
        Date: DateOnly.Parse(m.Date),
        StartYear: m.Years[0],
        EndYear: m.Years[1],
        Records: m.Records,
        Layers: m.Layers.Select(ToLayer).ToList(),
        Species: m.Species
            .Select(kv => new SpeciesSummary(kv.Key, kv.Value.SpeciesKey, kv.Value.Records, kv.Value.P0, kv.Value.Vmax))
            .ToList(),
        Meaning: m.Meaning,
        WrittenAt: m.WrittenAt);

    private static SightingsLayer ToLayer(LayerEntry l) => new(
        SigmaM: l.SigmaM,
        CellM: l.CellM,
        TileCells: l.TileCells,
        MaxUncertaintyM: l.MaxUncertaintyM,
        Records: l.Records,
        SpeciesRecords: l.SpeciesRecords,
        SpeciesP0: l.SpeciesP0,
        Grid: new LayerGrid(l.Grid.X0, l.Grid.Y0, l.Grid.Nx, l.Grid.Ny),
        Tiles: l.Tiles.Select(t => new TileRef(t.Tx, t.Ty, t.BoundsWgs84)).ToList());

    private static SightingsTile ToTile(TileFile t) => new(
        t.Species, t.Crs, t.SigmaM, t.CellM, t.Tx, t.Ty, t.X0, t.Y0, t.Nx, t.Ny, t.BoundsWgs84, t.P0,
        t.ConfidenceRecords, t.Share, t.N, t.Lo, t.Hi);
}
