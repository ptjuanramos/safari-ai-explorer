using SafariExplorer.Application.Abstractions;
using SafariExplorer.Domain.Directions;
using SafariExplorer.Domain.RoadRisk;

namespace SafariExplorer.Application.Directions;

public sealed class GetDrivingRoute(IDirectionsService directions)
{
    public bool IsAvailable => directions.IsConfigured;

    public Task<DrivingRoute?> ExecuteAsync(GeoPoint origin, GeoPoint destination, IReadOnlyList<GeoPoint> via,
        CancellationToken cancellationToken = default) =>
        directions.GetRouteAsync(origin, destination, via, cancellationToken);
}
