using SafariExplorer.Application.Abstractions;
using SafariExplorer.Domain.RoadRisk;

namespace SafariExplorer.Infrastructure.JobData;

/// <summary>
/// Road-risk runs from <c>road-risk/&lt;place&gt;/</c> in the job store (a folder or a blob container). Place names
/// must be slugs, which keeps every key inside the job's folder.
/// </summary>
public sealed class RoadRiskRepository(IJobStore store) : IRoadRiskRepository
{
    private const string Job = "road-risk";
    private const string EdgesFile = "edges.geojson";

    public async Task<IReadOnlyList<string>> ListPlacesAsync(CancellationToken cancellationToken)
    {
        // One listing finds every place's latest.json; then each one is checked to point at a written run.
        var candidates = new SortedSet<string>(StringComparer.Ordinal);
        await foreach (var key in store.ListKeysAsync(Job, cancellationToken))
        {
            if (key[(Job.Length + 1)..].Split('/') is [var place, JobFiles.Latest] && JobFiles.Slug().IsMatch(place))
            {
                candidates.Add(place);
            }
        }

        var places = new List<string>();
        foreach (var place in candidates)
        {
            if (await JobFiles.ReadLatestAsync(store, PlaceFolder(place)!, cancellationToken) is not null)
            {
                places.Add(place);
            }
        }

        return places;
    }

    public async Task<IReadOnlyList<DateOnly>> ListDatesAsync(string place, CancellationToken cancellationToken) =>
        PlaceFolder(place) is { } folder ? await JobFiles.ListRunsAsync(store, folder, cancellationToken) : [];

    public async Task<DateOnly?> GetLatestDateAsync(string place, CancellationToken cancellationToken) =>
        PlaceFolder(place) is { } folder ? await JobFiles.ReadLatestAsync(store, folder, cancellationToken) : null;

    public async Task<RoadRiskSnapshot?> GetAsync(string place, DateOnly date, CancellationToken cancellationToken)
    {
        if (PlaceFolder(place) is not { } folder)
        {
            return null;
        }

        var manifest = await store.ReadJsonAsync<RoadRiskManifest>(JobFiles.ManifestKey(folder, date), cancellationToken);
        if (manifest is null)
        {
            return null;
        }

        var edgesKey = $"{folder}/{JobFiles.RunId(date)}/{EdgesFile}";
        var edges = await store.ReadJsonAsync<EdgeCollection>(edgesKey, cancellationToken);
        return edges is null ? null : new RoadRiskSnapshot(ToRun(place, manifest), edges.Features.Select(ToSegment).ToList());
    }

    private static string? PlaceFolder(string place) => JobFiles.Slug().IsMatch(place) ? $"{Job}/{place}" : null;

    private static RoadRiskRun ToRun(string place, RoadRiskManifest m) => new(
        Place: place,
        Date: DateOnly.Parse(m.Date),
        Provider: m.Provider,
        LookbackDays: m.LookbackDays,
        EdgeCount: m.EdgeCount,
        Complete: m.Complete,
        GranulesPerDay: m.GranulesPerDay,
        GranuleCoverage: (m.GranuleCoverage ?? []).ToDictionary(kv => DateOnly.Parse(kv.Key), kv => kv.Value),
        Note: m.Note,
        WrittenAt: m.WrittenAt);

    private static RoadSegmentRisk ToSegment(EdgeFeature f) => new(
        Id: f.Id,
        RoadType: f.Properties.RoadType,
        LengthM: f.Properties.LengthM,
        TravelTimeMin: f.Properties.TravelTimeM,
        RainIndexMm: f.Properties.RainMm,
        SurfaceRisk: f.Properties.SurfaceRisk,
        Line: ToLine(f.Geometry));

    private static List<GeoPoint> ToLine(EdgeGeometry geometry)
    {
        if (geometry.Type != "LineString")
        {
            throw new InvalidDataException($"expected LineString edges, got {geometry.Type}");
        }

        return geometry.Coordinates.EnumerateArray()
            .Select(p => new GeoPoint(p[0].GetDouble(), p[1].GetDouble()))
            .ToList();
    }
}
