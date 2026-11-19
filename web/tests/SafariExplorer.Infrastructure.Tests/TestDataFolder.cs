using System.Text.Json;

namespace SafariExplorer.Tests;

public sealed class TestDataFolder : IDisposable
{
    public const string Place = "test-park";

    public TestDataFolder()
    {
        Root = Path.Combine(Path.GetTempPath(), "safari-explorer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public TestDataFolder WithRoadRun(string date, bool withManifest = true, string place = Place)
    {
        var dir = Path.Combine(Root, "road-risk", place, date);
        Write(Path.Combine(dir, "edges.geojson"), new
        {
            type = "FeatureCollection",
            features = new object[]
            {
                new
                {
                    id = "1-2-0",
                    type = "Feature",
                    properties = new { road_type = "track", length_m = 120.5, travel_time_m = 0.4, rain_mm = 12.5, surface_risk = 0.45 },
                    geometry = new { type = "LineString", coordinates = new[] { new[] { 36.0, -4.0 }, new[] { 36.1, -4.05 } } },
                },
                new
                {
                    id = "2-3-0",
                    type = "Feature",
                    properties = new { road_type = "primary", length_m = 80.0, travel_time_m = 0.2, rain_mm = (double?)null, surface_risk = 0.1 },
                    geometry = new { type = "LineString", coordinates = new[] { new[] { 36.1, -4.05 }, new[] { 36.2, -4.0 } } },
                },
            },
        });
        if (withManifest)
        {
            Write(Path.Combine(dir, "manifest.json"), new
            {
                job = "road-risk-refresh",
                place = "Test Park, Tanzania",
                date,
                provider = "IMERG",
                lookback_days = 5,
                edge_count = 2,
                complete = false,
                granules_per_day = 48,
                granule_coverage = new Dictionary<string, int> { [date] = 8 },
                note = "rain_mm is the decayed rain index",
                files = new[] { "edges.geojson" },
                written_at = "2026-11-15T19:30:33+00:00",
            });
        }

        return this;
    }

    public TestDataFolder WithLatest(string job, string run)
    {
        Write(Path.Combine(Root, job, "latest.json"),
            new { run, manifest = $"{run}/manifest.json", updated_at = "2026-11-15T19:30:33+00:00" });
        return this;
    }

    public TestDataFolder WithSightingsRun(string date)
    {
        var dir = Path.Combine(Root, "sightings", date);
        var tileBounds = new[] { 34.0, -3.6, 34.5, -3.1 };
        Write(Path.Combine(dir, "lion", "5000m", "0_0.json"), new
        {
            species = "lion",
            crs = "EPSG:32736",
            sigma_m = 5000,
            cell_m = 2500.0,
            tx = 0,
            ty = 0,
            x0 = 666250.0,
            y0 = 9601250.0,
            nx = 2,
            ny = 1,
            bounds_wgs84 = tileBounds,
            p0 = 0.097,
            confidence_records = 10,
            share = new double?[] { 0.12, null },
            n = new[] { 25.5, 1.2 },
            lo = new double?[] { 0.05, null },
            hi = new double?[] { 0.2, null },
        });
        Write(Path.Combine(dir, "manifest.json"), new
        {
            job = "sightings-refresh",
            date,
            years = new[] { 2017, 2026 },
            bounds_wgs84 = new[] { 34.0, -3.6, 36.0, -1.6 },
            crs = "EPSG:32736",
            records = 19441,
            settings = new { sigmas_m = new[] { 5000, 250 }, tile_cells = 256, min_records = 5 },
            layers = new object[]
            {
                new
                {
                    sigma_m = 5000,
                    cell_m = 2500.0,
                    tile_cells = 256,
                    max_uncertainty_m = (double?)null,
                    records = 19441,
                    species_records = new Dictionary<string, int> { ["lion"] = 1900, ["buffalo"] = 870 },
                    grid = new { x0 = 666250.0, y0 = 9601250.0, nx = 2, ny = 1 },
                    species_p0 = new Dictionary<string, double> { ["lion"] = 0.097, ["buffalo"] = 0.045 },
                    tiles = new[] { new { tx = 0, ty = 0, bounds_wgs84 = tileBounds } },
                },
                new
                {
                    sigma_m = 250,
                    cell_m = 125.0,
                    tile_cells = 256,
                    max_uncertainty_m = (double?)250,
                    records = 6000,
                    species_records = new Dictionary<string, int> { ["lion"] = 21, ["buffalo"] = 49 },
                    grid = new { x0 = 611062.5, y0 = 9601562.5, nx = 1784, ny = 1774 },
                    species_p0 = new Dictionary<string, double> { ["lion"] = 0.003, ["buffalo"] = 0.008 },
                    tiles = Array.Empty<object>(),
                },
            },
            species = new Dictionary<string, object>
            {
                ["lion"] = new { species_key = 5219404, records = 1900, p0 = 0.097, vmax = 0.251 },
                ["buffalo"] = new { species_key = 2441034, records = 870, p0 = 0.045, vmax = 0.146 },
            },
            meaning = "Where visitors see the animal, not everywhere it lives.",
            files = new[] { "lion/5000m/0_0.json" },
            written_at = "2026-11-15T19:31:00+00:00",
        });
        return this;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private static void Write(string path, object content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(content));
    }
}
