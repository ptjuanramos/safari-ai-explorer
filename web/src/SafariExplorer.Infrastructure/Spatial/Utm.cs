using NetTopologySuite.Geometries;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using SafariExplorer.Domain.RoadRisk;

namespace SafariExplorer.Infrastructure.Spatial;

/// <summary>Projects lon/lat to EPSG:32736 (UTM zone 36S, meters), where all distances are measured (ADR-0001).</summary>
internal static class Utm
{
    private static readonly MathTransform ToUtm = new CoordinateTransformationFactory()
        .CreateFromCoordinateSystems(GeographicCoordinateSystem.WGS84, ProjectedCoordinateSystem.WGS84_UTM(36, false))
        .MathTransform;

    private static readonly GeometryFactory Factory = new();

    /// <summary>The point in UTM meters (x = easting, y = northing).</summary>
    public static Coordinate Project(GeoPoint p)
    {
        var (x, y) = ToUtm.Transform(p.Longitude, p.Latitude);
        return new Coordinate(x, y);
    }

    /// <summary>The line in UTM meters, or null when it has fewer than two distinct points.</summary>
    public static LineString? Line(IEnumerable<GeoPoint> points)
    {
        var coordinates = points.Select(Project).ToArray();
        return coordinates.Distinct().Count() < 2 ? null : Factory.CreateLineString(coordinates);
    }
}
