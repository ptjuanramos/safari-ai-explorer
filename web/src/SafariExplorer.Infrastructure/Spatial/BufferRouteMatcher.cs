using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;
using NetTopologySuite.Index.Strtree;
using SafariExplorer.Application.Abstractions;
using SafariExplorer.Domain.RoadRisk;

namespace SafariExplorer.Infrastructure.Spatial;

/// <summary>
/// Matches routes to road edges in UTM meters: an edge lies along a route when at least
/// <see cref="MinShareInside"/> of its length is within <see cref="ToleranceM"/> of the route.
/// </summary>
/// <remarks>
/// The tolerance absorbs the offset between a Google route and the OSM road (usually 5 to 20 m). The share rule
/// leaves out roads that only cross the route, because a crossing road has a short piece inside the buffer
/// compared with its length. The mean risk weights each edge by the length that lies along the route.
/// </remarks>
public sealed class BufferRouteMatcher : IRouteMatcher
{
    public const double ToleranceM = 30;
    public const double MinShareInside = 0.5;

    public IReadOnlyList<RouteRisk> Match(IReadOnlyList<RoadSegmentRisk> network, IReadOnlyList<TripRoute> routes)
    {
        // The OSMnx graph is directed, so a two-way road is stored twice (u→v and v→u). Keep one per geometry,
        // or every two-way road would count double along the route.
        var edges = network
            .Select(segment => (Segment: segment, Line: Utm.Line(segment.Line)))
            .Where(e => e.Line is not null)
            .DistinctBy(e => GeometryKey(e.Line!))
            .ToList();
        var index = new STRtree<int>();
        for (var i = 0; i < edges.Count; i++)
        {
            index.Insert(edges[i].Line!.EnvelopeInternal, i);
        }

        return routes.Select(route => MatchOne(route, edges, index)).ToList();
    }

    /// <summary>The same key for a line and its reverse: its coordinates in canonical order, to the decimeter.</summary>
    private static string GeometryKey(LineString line)
    {
        var normalized = (LineString)line.Copy();
        normalized.Normalize();
        return string.Join(';', normalized.Coordinates.Select(c => $"{Math.Round(c.X, 1)},{Math.Round(c.Y, 1)}"));
    }

    private static RouteRisk MatchOne(TripRoute route, List<(RoadSegmentRisk Segment, LineString? Line)> edges,
        STRtree<int> index)
    {
        if (Utm.Line(route.Path) is not { } line)
        {
            return new RouteRisk(route.Id, 0, 0, 0, null, null, null, []);
        }

        // Candidates: the edges near each piece of the route, not every edge in the route's bounding box (a long
        // diagonal drive's box covers most of a park).
        var coordinates = line.Coordinates;
        var candidates = new SortedSet<int>();
        for (var i = 1; i < coordinates.Length; i++)
        {
            var near = new Envelope(coordinates[i - 1], coordinates[i]);
            near.ExpandBy(ToleranceM);
            foreach (var candidate in index.Query(near))
            {
                candidates.Add(candidate);
            }
        }

        // The prepared buffer answers "touches?" and "fully inside?" from an index; only the edges that cross its
        // outline need the full intersection.
        var buffer = line.Buffer(ToleranceM);
        var prepared = PreparedGeometryFactory.Prepare(buffer);
        var matched = candidates
            .Select(i => edges[i])
            .Where(e => prepared.Intersects(e.Line!))
            .Select(e => (e.Segment, Inside: prepared.Covers(e.Line!) ? e.Line!.Length : e.Line!.Intersection(buffer).Length,
                Length: e.Line!.Length))
            .Where(m => m.Length > 0 && m.Inside / m.Length >= MinShareInside)
            .ToList();
        if (matched.Count == 0)
        {
            return new RouteRisk(route.Id, line.Length, 0, 0, null, null, null, []);
        }

        var matchedLength = matched.Sum(m => m.Inside);
        var rain = matched.Select(m => m.Segment.RainIndexMm).OfType<double>().ToList();
        return new RouteRisk(
            Id: route.Id,
            RouteLengthM: line.Length,
            MatchedLengthM: matchedLength,
            MatchedEdges: matched.Count,
            MeanSurfaceRisk: matched.Sum(m => m.Segment.SurfaceRisk * m.Inside) / matchedLength,
            MaxSurfaceRisk: matched.Max(m => m.Segment.SurfaceRisk),
            MaxRainIndexMm: rain.Count > 0 ? rain.Max() : null,
            RoadTypes: matched.Select(m => m.Segment.RoadType).Distinct().Order().ToList())
        {
            MatchedEdgeIds = matched.Select(m => m.Segment.Id).ToList(),
        };
    }
}
