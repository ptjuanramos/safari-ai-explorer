namespace SafariExplorer.Domain.RoadRisk;

public readonly record struct GeoPoint(double Longitude, double Latitude);

public sealed record RoadSegmentRisk(
    string Id,
    string RoadType,
    double LengthM,
    double TravelTimeMin,
    double? RainIndexMm,
    double SurfaceRisk,
    IReadOnlyList<GeoPoint> Line);
