namespace SafariExplorer.Domain.RoadRisk;

/// <summary>A route the user plans to drive, for example one day of a trip, as a lon/lat line.</summary>
public sealed record TripRoute(string Id, IReadOnlyList<GeoPoint> Path);

/// <summary>
/// The road risk along one <see cref="TripRoute"/>: the park road edges that lie along it, and their risk.
/// The risks are null when no edge matched, which means the route is outside the mapped park roads.
/// </summary>
public sealed record RouteRisk(
    string Id,
    double RouteLengthM,
    double MatchedLengthM,
    int MatchedEdges,
    double? MeanSurfaceRisk,
    double? MaxSurfaceRisk,
    double? MaxRainIndexMm,
    IReadOnlyList<string> RoadTypes)
{
    /// <summary>Share of the route's length that runs along mapped park roads, 0 to 1.</summary>
    public double Coverage => RouteLengthM > 0 ? Math.Min(1, MatchedLengthM / RouteLengthM) : 0;

    /// <summary>Slugs of the places (parks) whose roads the route runs along; empty when none matched.</summary>
    public IReadOnlyList<string> Places { get; init; } = [];

    /// <summary>
    /// Ids of the edges that lie along the route, so the places follow from one match over every park's roads.
    /// Not sent to the UI.
    /// </summary>
    public IReadOnlyList<string> MatchedEdgeIds { get; init; } = [];
}

/// <summary>The risk of several routes, computed from one road-risk run per place (park).</summary>
/// <param name="Runs">
/// The run used for each place that has one recent enough: its latest for <paramref name="RequestedDate"/> or later,
/// else its newest on or before that date. Places differ when a job is behind for one of them.
/// </param>
/// <param name="RequestedDate">The date the user asked for. It can be later than the runs, for example a future trip.</param>
public sealed record RouteRiskReport(IReadOnlyList<RoadRiskRun> Runs, DateOnly RequestedDate, IReadOnlyList<RouteRisk> Routes);

/// <summary>A place (park) with road-risk data, and its newest run.</summary>
public sealed record RoadRiskPlace(string Place, DateOnly LatestDate);
