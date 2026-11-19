using SafariExplorer.Domain.Directions;

namespace SafariExplorer.Api.Contracts;

/// <summary>Body of <c>POST /api/directions</c>.</summary>
public sealed record DirectionsRequestDto(LatLngDto Origin, LatLngDto Destination, IReadOnlyList<LatLngDto>? Intermediates)
{
    public const int MaxIntermediates = 25;
}

public sealed record DrivingRouteDto(string EncodedPolyline, double DistanceKm, int DurationMin)
{
    public static DrivingRouteDto From(DrivingRoute route) => new(route.EncodedPolyline, route.DistanceKm, route.DurationMin);
}
