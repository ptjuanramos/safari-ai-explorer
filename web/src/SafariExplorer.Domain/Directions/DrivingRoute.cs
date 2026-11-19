namespace SafariExplorer.Domain.Directions;

/// <summary>A driving route between two points, with its line as a Google encoded polyline.</summary>
public sealed record DrivingRoute(string EncodedPolyline, double DistanceKm, int DurationMin);
