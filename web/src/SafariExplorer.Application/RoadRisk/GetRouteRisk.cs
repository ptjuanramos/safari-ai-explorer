using SafariExplorer.Application.Abstractions;
using SafariExplorer.Domain.RoadRisk;

namespace SafariExplorer.Application.RoadRisk;

public sealed class GetRouteRisk(IRoadRiskRepository repository, IRouteMatcher matcher)
{
    /// <summary>For a past date, a run older than this before it says nothing about that day's roads.</summary>
    public const int MaxRunAgeDays = 7;

    /// <summary>
    /// The risk of each route on <paramref name="date"/>, from <paramref name="place"/> only, or from every place
    /// (park) when it's null, since a trip usually crosses several. Null when no place has a recent enough run.
    /// </summary>
    public async Task<RouteRiskReport?> ExecuteAsync(string? place, DateOnly date, IReadOnlyList<TripRoute> routes,
        CancellationToken cancellationToken = default)
    {
        var places = place is null ? await repository.ListPlacesAsync(cancellationToken) : [place];
        var snapshots = new List<RoadRiskSnapshot>();
        foreach (var p in places)
        {
            if (await RunDateFor(p, date, cancellationToken) is { } runDate
                && await repository.GetAsync(p, runDate, cancellationToken) is { } snapshot)
            {
                snapshots.Add(snapshot);
            }
        }

        if (snapshots.Count == 0)
        {
            return null;
        }

        // Neighbouring parks can share a border road (Serengeti and Ngorongoro). An edge id is a pair of OSM nodes,
        // the same in both, so each edge is matched once and isn't counted twice.
        var network = snapshots.SelectMany(s => s.Segments).DistinctBy(s => s.Id).ToList();
        var risks = matcher.Match(network, routes);
        var placesByRoute = PlacesByRoute(snapshots, risks);
        return new RouteRiskReport(
            snapshots.Select(s => s.Run).ToList(),
            date,
            risks.Select(r => r with { Places = placesByRoute.GetValueOrDefault(r.Id) ?? [] }).ToList());
    }

    /// <summary>Per route id, the places whose roads it touches, so the UI can tell which run's rain day applies.</summary>
    /// <remarks>
    /// With one place that's every matched route. With several, a route touches a place when one of its matched edges
    /// is that place's: from the one match over every park, since matching each park again was most of the time.
    /// </remarks>
    private static Dictionary<string, List<string>> PlacesByRoute(
        List<RoadRiskSnapshot> snapshots, IReadOnlyList<RouteRisk> risks)
    {
        // A border road shared by two parks has the same id in both, so it belongs to both.
        var placesByEdge = new Dictionary<string, HashSet<string>>();
        foreach (var snapshot in snapshots)
        {
            foreach (var segment in snapshot.Segments)
            {
                if (!placesByEdge.TryGetValue(segment.Id, out var set))
                {
                    placesByEdge[segment.Id] = set = [];
                }

                set.Add(snapshot.Run.Place);
            }
        }

        var placesByRoute = new Dictionary<string, List<string>>();
        foreach (var risk in risks.Where(r => r.MatchedEdges > 0))
        {
            var touched = risk.MatchedEdgeIds.SelectMany(id => placesByEdge.GetValueOrDefault(id) ?? []).ToHashSet();
            placesByRoute[risk.Id] = snapshots.Count == 1
                ? [snapshots[0].Run.Place]
                : snapshots.Select(s => s.Run.Place).Where(touched.Contains).ToList();
        }

        return placesByRoute;
    }

    /// <summary>
    /// The run to use for a date: the latest run for that date or later (rain isn't known ahead, so a future trip
    /// gets today's conditions), otherwise the newest run on or before it and at most <see cref="MaxRunAgeDays"/>
    /// older. Null when there is no such run.
    /// </summary>
    private async Task<DateOnly?> RunDateFor(string place, DateOnly date, CancellationToken cancellationToken)
    {
        var latest = await repository.GetLatestDateAsync(place, cancellationToken);
        if (latest is null || date >= latest)
        {
            return latest;
        }

        var oldest = date.AddDays(-MaxRunAgeDays);
        var earlier = (await repository.ListDatesAsync(place, cancellationToken))
            .Where(d => d <= date && d >= oldest)
            .ToList();
        return earlier.Count > 0 ? earlier.Max() : null;
    }
}
