using SafariExplorer.Domain.RoadRisk;

namespace SafariExplorer.Application.UserPaths;

/// <summary>A path as the user sends it, before it has an id. <see cref="Validate"/> before saving it.</summary>
public sealed record NewUserPath(
    int RouteNumber,
    string? Origin,
    string? Destination,
    IReadOnlyList<GeoPoint>? Stops,
    string? SurfaceType,
    double DistanceKm,
    int DurationMin)
{
    public const int MaxRouteNumber = 60;
    public const int MaxNameLength = 120;

    /// <summary>Origin + destination + up to 25 stops between them, the most Google Routes takes.</summary>
    public const int MaxStops = 27;

    public static readonly IReadOnlySet<string> SurfaceTypes = new HashSet<string> { "paved", "gravel", "track" };

    /// <summary>Why the path can't be saved, or null when it can.</summary>
    public string? Validate() =>
        RouteNumber is < 1 or > MaxRouteNumber ? $"routeNumber must be between 1 and {MaxRouteNumber}"
        : !IsName(Origin) || !IsName(Destination) ? $"origin and destination need 1 to {MaxNameLength} characters"
        : Stops is not { Count: >= 2 and <= MaxStops } ? $"send between 2 and {MaxStops} stops"
        : Stops.Any(p => !IsOnEarth(p)) ? "every stop needs a latitude in [-90, 90] and a longitude in [-180, 180]"
        : SurfaceType is null || !SurfaceTypes.Contains(SurfaceType) ? "surfaceType must be paved, gravel or track"
        : !double.IsFinite(DistanceKm) || DistanceKm is < 0 or > 5000 ? "distanceKm must be between 0 and 5000"
        : DurationMin is < 0 or > 10_000 ? "durationMin must be between 0 and 10000"
        : null;

    private static bool IsName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Length <= MaxNameLength;

    private static bool IsOnEarth(GeoPoint p) =>
        double.IsFinite(p.Latitude) && double.IsFinite(p.Longitude)
        && p.Latitude is >= -90 and <= 90 && p.Longitude is >= -180 and <= 180;
}
