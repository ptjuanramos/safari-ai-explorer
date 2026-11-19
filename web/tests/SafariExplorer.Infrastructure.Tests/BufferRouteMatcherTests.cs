using SafariExplorer.Domain.RoadRisk;
using SafariExplorer.Infrastructure.Spatial;

namespace SafariExplorer.Infrastructure.Tests;

public sealed class BufferRouteMatcherTests
{
    // Two edges along the route (about 1.1 km each), one road crossing it, and one far away.
    private static readonly RoadSegmentRisk[] Network =
    [
        Edge("along-track", "track", 0.6, 20, (36.00, -4.00), (36.01, -4.00)),
        Edge("along-primary", "primary", 0.2, null, (36.01, -4.00), (36.02, -4.00)),
        Edge("crossing", "track", 1.0, 50, (36.015, -4.005), (36.015, -3.995)),
        Edge("far", "track", 1.0, 50, (36.50, -4.50), (36.51, -4.50)),
    ];

    [Fact]
    public void Projection_matches_pyproj_for_epsg_32736()
    {
        var p = Utm.Project(new GeoPoint(36.0, -3.8));

        Assert.Equal(833247.845, p.X, tolerance: 0.01);
        Assert.Equal(9579401.044, p.Y, tolerance: 0.01);
    }

    [Fact]
    public void Edges_along_the_route_match_and_a_crossing_road_does_not()
    {
        // About 11 m south of the edges: inside the 30 m tolerance.
        var route = new TripRoute("day-1", [new GeoPoint(36.00, -4.0001), new GeoPoint(36.02, -4.0001)]);

        var risk = Assert.Single(new BufferRouteMatcher().Match(Network, [route]));

        Assert.Equal(2, risk.MatchedEdges);
        Assert.Equal(["primary", "track"], risk.RoadTypes);
        Assert.Equal(0.4, risk.MeanSurfaceRisk!.Value, tolerance: 0.005);
        Assert.Equal(0.6, risk.MaxSurfaceRisk);
        Assert.Equal(20, risk.MaxRainIndexMm);
        Assert.InRange(risk.RouteLengthM, 2_200, 2_240);
        Assert.InRange(risk.Coverage, 0.99, 1.0);
    }

    [Fact]
    public void A_two_way_road_stored_in_both_directions_counts_once()
    {
        RoadSegmentRisk[] network =
        [
            Edge("1-2-0", "track", 0.6, 20, (36.00, -4.00), (36.01, -4.00)),
            Edge("2-1-0", "track", 0.6, 20, (36.01, -4.00), (36.00, -4.00)),
        ];
        var route = new TripRoute("day-1", [new GeoPoint(36.00, -4.0001), new GeoPoint(36.01, -4.0001)]);

        var risk = Assert.Single(new BufferRouteMatcher().Match(network, [route]));

        Assert.Equal(1, risk.MatchedEdges);
        Assert.InRange(risk.MatchedLengthM, 1_100, 1_120);
    }

    [Fact]
    public void A_route_outside_the_network_has_no_risk()
    {
        var route = new TripRoute("elsewhere", [new GeoPoint(35.0, -3.0), new GeoPoint(35.1, -3.0)]);

        var risk = Assert.Single(new BufferRouteMatcher().Match(Network, [route]));

        Assert.Equal((0, 0.0), (risk.MatchedEdges, risk.Coverage));
        Assert.Null(risk.MeanSurfaceRisk);
        Assert.Null(risk.MaxSurfaceRisk);
        Assert.True(risk.RouteLengthM > 10_000);
    }

    [Fact]
    public void A_route_of_one_repeated_point_has_no_length()
    {
        var route = new TripRoute("dot", [new GeoPoint(36.0, -4.0), new GeoPoint(36.0, -4.0)]);

        var risk = Assert.Single(new BufferRouteMatcher().Match(Network, [route]));

        Assert.Equal((0.0, 0), (risk.RouteLengthM, risk.MatchedEdges));
    }

    private static RoadSegmentRisk Edge(string id, string roadType, double risk, double? rain,
        (double Lon, double Lat) a, (double Lon, double Lat) b) =>
        new(id, roadType, 1000, 1, rain, risk, [new GeoPoint(a.Lon, a.Lat), new GeoPoint(b.Lon, b.Lat)]);
}
