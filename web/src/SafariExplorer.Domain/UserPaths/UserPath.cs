using SafariExplorer.Domain.RoadRisk;

namespace SafariExplorer.Domain.UserPaths;

/// <summary>
/// A drive the user added to the trip, saved by the API. <see cref="Stops"/> are the points the driving route must
/// pass, in order: the origin first, the destination last. The road itself comes from the directions service.
/// </summary>
public sealed record UserPath(
    string Id,
    int RouteNumber,
    string Origin,
    string Destination,
    IReadOnlyList<GeoPoint> Stops,
    string SurfaceType,
    double DistanceKm,
    int DurationMin,
    DateTimeOffset CreatedAt);
